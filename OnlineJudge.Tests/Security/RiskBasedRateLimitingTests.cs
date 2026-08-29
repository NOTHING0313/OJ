using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Infrastructure.Auth;

namespace OnlineJudge.Tests.Security;

public class RiskBasedRateLimitingTests
{
    [Theory]
    [InlineData(RateLimitPolicies.AuthLogin, 10)]
    [InlineData(RateLimitPolicies.AuthRegister, 5)]
    [InlineData(RateLimitPolicies.PasswordReset, 20)]
    [InlineData(RateLimitPolicies.Submission, 10)]
    [InlineData(RateLimitPolicies.TeamChat, 10)]
    [InlineData(RateLimitPolicies.HelpMutation, 30)]
    [InlineData(RateLimitPolicies.AdminMutation, 30)]
    public async Task Policies_RejectImmediatelyAfterConfiguredLimit(string policy, int permitLimit)
    {
        await using var limiter = RateLimitPolicies.CreateGlobalLimiter();
        var context = CreateContext(policy);

        for (var index = 0; index < permitLimit; index++)
        {
            using var lease = limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }

        using var rejected = limiter.AttemptAcquire(context);
        Assert.False(rejected.IsAcquired);
        Assert.True(rejected.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task GitSync_UsesBothUserAndProjectPartitions()
    {
        await using var projectLimiter = RateLimitPolicies.CreateGlobalLimiter();
        var sameProject = CreateContext(RateLimitPolicies.TeamGitSync, projectId: "project-a");
        using var first = projectLimiter.AttemptAcquire(sameProject);
        using var second = projectLimiter.AttemptAcquire(sameProject);
        Assert.True(first.IsAcquired);
        Assert.False(second.IsAcquired);

        await using var userLimiter = RateLimitPolicies.CreateGlobalLimiter();
        using var userFirst = userLimiter.AttemptAcquire(CreateContext(RateLimitPolicies.TeamGitSync, projectId: "project-a"));
        using var userSecond = userLimiter.AttemptAcquire(CreateContext(RateLimitPolicies.TeamGitSync, projectId: "project-b"));
        using var userThird = userLimiter.AttemptAcquire(CreateContext(RateLimitPolicies.TeamGitSync, projectId: "project-c"));
        Assert.True(userFirst.IsAcquired);
        Assert.True(userSecond.IsAcquired);
        Assert.False(userThird.IsAcquired);
    }

    [Fact]
    public async Task Upload_UsesRequestRateAndConcurrencyLimitsWithoutQueueing()
    {
        await using var limiter = RateLimitPolicies.CreateGlobalLimiter();
        var context = CreateContext(RateLimitPolicies.Upload);
        using var first = limiter.AttemptAcquire(context);
        using var second = limiter.AttemptAcquire(context);
        using var rejected = limiter.AttemptAcquire(context);
        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.False(rejected.IsAcquired);

        first.Dispose();
        using var recovered = limiter.AttemptAcquire(context);
        Assert.True(recovered.IsAcquired);
    }

    [Fact]
    public async Task ClientIpPartition_IgnoresSpoofedForwardedHeader()
    {
        await using var limiter = RateLimitPolicies.CreateGlobalLimiter();

        for (var index = 0; index < 10; index++)
        {
            var context = CreateContext(RateLimitPolicies.AuthLogin, remoteIp: "203.0.113.10");
            context.Request.Headers["X-Forwarded-For"] = $"198.51.100.{index + 1}";
            using var lease = limiter.AttemptAcquire(context);
            Assert.True(lease.IsAcquired);
        }

        var rejectedContext = CreateContext(RateLimitPolicies.AuthLogin, remoteIp: "203.0.113.10");
        rejectedContext.Request.Headers["X-Forwarded-For"] = "192.0.2.99";
        using var rejected = limiter.AttemptAcquire(rejectedContext);
        Assert.False(rejected.IsAcquired);
    }

    [Fact]
    public void ResponseContract_UsesUnifiedCodeMessageAndRetryHeader()
    {
        var payload = RateLimitResponseWriter.CreatePayload(RateLimitPolicies.Submission, 6);
        var context = new DefaultHttpContext();
        RateLimitResponseWriter.SetRetryAfterHeader(context.Response, 6);

        Assert.Equal("RATE_LIMITED", payload.ErrorCode);
        Assert.Equal(6, payload.RetryAfterSeconds);
        Assert.Contains("提交", payload.Message, StringComparison.Ordinal);
        Assert.Equal("6", context.Response.Headers.RetryAfter);
    }

    [Fact]
    public void AccountHash_IsNormalizedAndDoesNotExposeAccount()
    {
        var first = LoginAbuseProtection.HashAccount("  Example.Account  ");
        var second = LoginAbuseProtection.HashAccount("example.account");

        Assert.Equal(second, first);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain("example", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginProtection_EnforcesStoreResultAndResetsState()
    {
        var store = new FakeLoginAbuseStore { RetryAfterSeconds = 30 };
        var protection = new LoginAbuseProtection(store, NullLogger<LoginAbuseProtection>.Instance);

        var result = await protection.CheckAsync("account");
        await protection.RecordFailedPasswordAsync("account");
        await protection.ResetAsync("account");

        Assert.False(result.IsAllowed);
        Assert.Equal(30, result.RetryAfterSeconds);
        Assert.Equal(1, store.FailureCalls);
        Assert.Equal(1, store.ResetCalls);
    }

    [Fact]
    public async Task LoginProtection_FailsDegradedWhenRedisIsUnavailable()
    {
        var store = new FakeLoginAbuseStore { ThrowUnavailable = true };
        var protection = new LoginAbuseProtection(store, NullLogger<LoginAbuseProtection>.Instance);

        var result = await protection.CheckAsync("account");
        await protection.RecordFailedPasswordAsync("account");
        await protection.ResetAsync("account");

        Assert.True(result.IsAllowed);
        Assert.True(result.IsDegraded);
    }

    [Fact]
    public void SourceContract_RateLimitsBeforeControllerWorkAndKeepsSensitiveValuesOutOfLogs()
    {
        var root = ProjectRoot();
        var program = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Program.cs"));
        var policies = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "RateLimiting", "RateLimitPolicies.cs"));
        var loginProtection = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Auth", "LoginAbuseProtection.cs"));

        Assert.True(program.IndexOf("app.UseRateLimiter();", StringComparison.Ordinal) < program.IndexOf("app.UseAuthorization();", StringComparison.Ordinal));
        Assert.Contains("KnownProxies.Add(System.Net.IPAddress.Loopback)", program, StringComparison.Ordinal);
        Assert.Contains("QueueLimit = 0", policies, StringComparison.Ordinal);
        Assert.Contains("Policy={PolicyName} UserId={UserId}", policies, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", policies, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", loginProtection, StringComparison.Ordinal);
        Assert.DoesNotContain("Account={", loginProtection, StringComparison.Ordinal);
    }

    private static DefaultHttpContext CreateContext(
        string policy,
        string userId = "user-1",
        string? projectId = null,
        string remoteIp = "203.0.113.10")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId)
        ], "test"));
        if (projectId is not null)
        {
            context.Request.RouteValues["projectId"] = projectId;
        }

        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RiskRateLimitAttribute(policy)),
            "rate-limit-test"));
        return context;
    }

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class FakeLoginAbuseStore : ILoginAbuseStore
    {
        public int RetryAfterSeconds { get; init; }

        public bool ThrowUnavailable { get; init; }

        public int FailureCalls { get; private set; }

        public int ResetCalls { get; private set; }

        public Task<int> CheckAsync(string accountHash)
        {
            ThrowIfUnavailable();
            return Task.FromResult(RetryAfterSeconds);
        }

        public Task RecordFailedPasswordAsync(string accountHash)
        {
            ThrowIfUnavailable();
            FailureCalls++;
            return Task.CompletedTask;
        }

        public Task ResetAsync(string accountHash)
        {
            ThrowIfUnavailable();
            ResetCalls++;
            return Task.CompletedTask;
        }

        private void ThrowIfUnavailable()
        {
            if (ThrowUnavailable)
            {
                throw new LoginAbuseStoreUnavailableException(new InvalidOperationException("test outage"));
            }
        }
    }
}
