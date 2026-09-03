using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Infrastructure.Email;
using OnlineJudge.Infrastructure.Sms;
using OnlineJudge.Infrastructure.Verification;
using StackExchange.Redis;

namespace OnlineJudge.Tests.Auth;

public sealed class VerificationCodeRedisTests
{
    [RedisIntegrationFact]
    public async Task ConcurrentCorrectCode_IsConsumedAtMostOnce()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();
        var status = await fixture.IssueAsync(target, "issuance", "correct-hash");

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => RedisVerificationCodeStore.TryConsumeAsync(
                fixture.Database, fixture.Channel, fixture.Scene, target, "correct-hash", 5)));

        Assert.Equal(VerificationCodeIssueStatus.Issued, status);
        Assert.Single(results, result => result);
    }

    [RedisIntegrationFact]
    public async Task ConcurrentWrongCodes_ReachAttemptLimitWithoutLostUpdates()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();
        await fixture.IssueAsync(target, "issuance", "correct-hash");

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => RedisVerificationCodeStore.TryConsumeAsync(
                fixture.Database, fixture.Channel, fixture.Scene, target, "wrong-hash", 5)));
        var correctAfterFailures = await RedisVerificationCodeStore.TryConsumeAsync(
            fixture.Database, fixture.Channel, fixture.Scene, target, "correct-hash", 5);

        Assert.DoesNotContain(true, results);
        Assert.False(correctAfterFailures);
    }

    [RedisIntegrationFact]
    public async Task ConcurrentIssuance_OnlyOneRequestReservesTheTarget()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();

        var results = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(index => fixture.IssueAsync(target, $"issuance-{index}", $"hash-{index}")));

        Assert.Single(results, status => status == VerificationCodeIssueStatus.Issued);
        Assert.Equal(49, results.Count(status => status == VerificationCodeIssueStatus.Cooldown));
    }

    [RedisIntegrationFact]
    public async Task WrongAttempt_PreservesTheOriginalCodeExpiry()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();
        await fixture.IssueAsync(target, "issuance", "correct-hash");
        var key = RedisVerificationCodeStore.CodeKey(fixture.Channel, fixture.Scene, target);
        var expiryBefore = await fixture.Database.KeyTimeToLiveAsync(key);

        await RedisVerificationCodeStore.TryConsumeAsync(
            fixture.Database, fixture.Channel, fixture.Scene, target, "wrong-hash", 5);
        var expiryAfter = await fixture.Database.KeyTimeToLiveAsync(key);

        Assert.NotNull(expiryBefore);
        Assert.NotNull(expiryAfter);
        Assert.InRange(expiryAfter.Value, TimeSpan.FromMinutes(4.9), expiryBefore.Value);
    }

    [RedisIntegrationFact]
    public async Task RepeatedIssuance_EnforcesTheDailyLimitAtomically()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();

        for (var index = 0; index < 10; index++)
        {
            var issuanceId = $"issuance-{index}";
            var status = await fixture.IssueAsync(target, issuanceId, $"hash-{index}");
            Assert.Equal(VerificationCodeIssueStatus.Issued, status);
            await RedisVerificationCodeStore.CleanupIssuanceAsync(
                fixture.Database, fixture.Channel, fixture.Scene, target, issuanceId);
        }

        var rejected = await fixture.IssueAsync(target, "issuance-over-limit", "hash-over-limit");

        Assert.Equal(VerificationCodeIssueStatus.DailyLimitExceeded, rejected);
    }

    [RedisIntegrationFact]
    public async Task CleanupIssuance_DeletesOnlyTheMatchingReservation()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var target = fixture.UniqueTarget();
        await fixture.IssueAsync(target, "current", "correct-hash");

        await RedisVerificationCodeStore.CleanupIssuanceAsync(
            fixture.Database, fixture.Channel, fixture.Scene, target, "stale");
        var stillConsumable = await RedisVerificationCodeStore.TryConsumeAsync(
            fixture.Database, fixture.Channel, fixture.Scene, target, "correct-hash", 5);

        Assert.True(stillConsumable);
    }

    [RedisIntegrationFact]
    public async Task SenderFailure_CleansCodeAndCooldownButKeepsDailyAttempt()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var email = $"{fixture.UniqueTarget()}@example.test";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Jwt:Secret"] = new string('x', 64)
        }).Build();
        var failing = new EmailVerificationService(
            fixture.Connection,
            new ThrowingEmailSender(),
            configuration,
            NullLogger<EmailVerificationService>.Instance);
        var succeeding = new EmailVerificationService(
            fixture.Connection,
            new CapturingEmailSender(),
            configuration,
            NullLogger<EmailVerificationService>.Instance);

        var failed = await failing.SendCodeAsync(fixture.Scene, email);
        var retried = await succeeding.SendCodeAsync(fixture.Scene, email);

        Assert.True(failed.IsFailure);
        Assert.True(retried.IsSuccess);
        var dailyCount = await fixture.Database.StringGetAsync(
            RedisVerificationCodeStore.DailyKey("email", fixture.Scene, email));
        Assert.Equal(2, (int)dailyCount);
    }

    [RedisIntegrationFact]
    public async Task SmsSenderFailure_IsControlledAndReservationCanBeRetried()
    {
        await using var fixture = await RedisFixture.CreateAsync();
        var phone = $"+1555{Random.Shared.Next(1000000, 9999999)}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["Jwt:Secret"] = new string('x', 64)
        }).Build();
        var failing = new SmsVerificationService(fixture.Connection, new ThrowingSmsSender(), configuration);
        var succeeding = new SmsVerificationService(fixture.Connection, new CapturingSmsSender(), configuration);

        var failed = await failing.SendCodeAsync(fixture.Scene, phone);
        var retried = await succeeding.SendCodeAsync(fixture.Scene, phone);

        Assert.True(failed.IsFailure);
        Assert.True(retried.IsSuccess);
    }

    private sealed class RedisFixture : IAsyncDisposable
    {
        private RedisFixture(ConnectionMultiplexer connection)
        {
            Connection = connection;
            Database = connection.GetDatabase();
        }

        public string Channel { get; } = $"verification-test-{Guid.NewGuid():N}";
        public string Scene { get; } = "concurrency";
        public ConnectionMultiplexer Connection { get; }
        public IDatabase Database { get; }

        public static async Task<RedisFixture> CreateAsync()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("ONLINEJUDGE_REDIS_INTEGRATION"), "1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Redis integration gate was invoked without ONLINEJUDGE_REDIS_INTEGRATION=1.");
            }

            var connectionString = Environment.GetEnvironmentVariable("ONLINEJUDGE_REDIS_CONNECTION") ?? "localhost:6379";
            return new RedisFixture(await ConnectionMultiplexer.ConnectAsync(connectionString));
        }

        public string UniqueTarget() => Guid.NewGuid().ToString("N");

        public Task<VerificationCodeIssueStatus> IssueAsync(string target, string issuanceId, string codeHash) =>
            RedisVerificationCodeStore.TryIssueAsync(
                Database,
                Channel,
                Scene,
                target,
                issuanceId,
                codeHash,
                10,
                TimeSpan.FromMinutes(30),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(1));

        public async ValueTask DisposeAsync()
        {
            await Connection.CloseAsync();
            Connection.Dispose();
        }
    }

    private sealed class ThrowingEmailSender : OnlineJudge.Application.Email.Services.IEmailSender
    {
        public Task SendVerificationCodeAsync(string toEmail, string code, string scene, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated email failure");
    }

    private sealed class CapturingEmailSender : OnlineJudge.Application.Email.Services.IEmailSender
    {
        public Task SendVerificationCodeAsync(string toEmail, string code, string scene, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingSmsSender : OnlineJudge.Application.Sms.Services.ISmsSender
    {
        public Task SendVerificationCodeAsync(string phoneNumber, string code, string scene, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated SMS failure");
    }

    private sealed class CapturingSmsSender : OnlineJudge.Application.Sms.Services.ISmsSender
    {
        public Task SendVerificationCodeAsync(string phoneNumber, string code, string scene, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public sealed class RedisIntegrationFactAttribute : FactAttribute
{
    public RedisIntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ONLINEJUDGE_REDIS_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            Skip = "Real Redis integration is disabled. Set ONLINEJUDGE_REDIS_INTEGRATION=1 to run this gate.";
        }
    }
}
