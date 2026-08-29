using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace OnlineJudge.Api.RateLimiting;

public static class RateLimitPolicies
{
    public const string AuthLogin = "AuthLogin";
    public const string AuthRegister = "AuthRegister";
    public const string PasswordReset = "PasswordReset";
    public const string Submission = "Submission";
    public const string TeamChat = "TeamChat";
    public const string TeamGitSync = "TeamGitSync";
    public const string Upload = "Upload";
    public const string HelpMutation = "HelpMutation";
    public const string AdminMutation = "AdminMutation";

    public static IServiceCollection AddRiskBasedRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = CreateGlobalLimiter();
            options.OnRejected = async (context, cancellationToken) =>
            {
                var policies = GetPolicies(context.HttpContext);
                var policyName = policies.FirstOrDefault() ?? "Unknown";
                var retryAfterSeconds = RateLimitResponseWriter.GetRetryAfterSeconds(context.Lease);
                RateLimitResponseWriter.SetRetryAfterHeader(context.HttpContext.Response, retryAfterSeconds);

                var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("OnlineJudge.Api.RateLimiting");
                logger.LogWarning(
                    "Rate limit rejected. Policy={PolicyName} UserId={UserId} RetryAfter={RetryAfter} Path={Path}",
                    string.Join(',', policies),
                    userId,
                    retryAfterSeconds,
                    context.HttpContext.Request.Path.Value);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    RateLimitResponseWriter.CreatePayload(policyName, retryAfterSeconds),
                    cancellationToken);
            };
        });

        return services;
    }

    public static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter() =>
        PartitionedRateLimiter.CreateChained(
            FixedWindow(AuthLogin, 10, TimeSpan.FromMinutes(1), ClientIp),
            FixedWindow(AuthRegister, 5, TimeSpan.FromMinutes(10), ClientIp),
            FixedWindow(PasswordReset, 20, TimeSpan.FromMinutes(10), ClientIp),
            TokenBucket(Submission, 10, 1, TimeSpan.FromSeconds(6), UserId),
            TokenBucket(TeamChat, 10, 1, TimeSpan.FromSeconds(1), UserId),
            FixedWindow(TeamGitSync, 2, TimeSpan.FromMinutes(1), UserId, "user"),
            FixedWindow(TeamGitSync, 1, TimeSpan.FromSeconds(30), ProjectId, "project"),
            FixedWindow(Upload, 10, TimeSpan.FromMinutes(1), UserId),
            Concurrency(Upload, 2, UserId),
            FixedWindow(HelpMutation, 30, TimeSpan.FromMinutes(1), UserId),
            FixedWindow(AdminMutation, 30, TimeSpan.FromMinutes(1), UserId));

    public static IReadOnlyList<string> GetPolicies(HttpContext context) =>
        context.GetEndpoint()?.Metadata
            .GetOrderedMetadata<RiskRateLimitAttribute>()
            .Select(metadata => metadata.PolicyName)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

    private static PartitionedRateLimiter<HttpContext> FixedWindow(
        string policyName,
        int permitLimit,
        TimeSpan window,
        Func<HttpContext, string> partition,
        string dimension = "primary") =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            HasPolicy(context, policyName)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"{policyName}:{dimension}:{partition(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })
                : RateLimitPartition.GetNoLimiter($"none:{policyName}:{dimension}"));

    private static PartitionedRateLimiter<HttpContext> TokenBucket(
        string policyName,
        int tokenLimit,
        int tokensPerPeriod,
        TimeSpan replenishmentPeriod,
        Func<HttpContext, string> partition) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            HasPolicy(context, policyName)
                ? RateLimitPartition.GetTokenBucketLimiter(
                    $"{policyName}:{partition(context)}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = tokenLimit,
                        TokensPerPeriod = tokensPerPeriod,
                        ReplenishmentPeriod = replenishmentPeriod,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    })
                : RateLimitPartition.GetNoLimiter($"none:{policyName}"));

    private static PartitionedRateLimiter<HttpContext> Concurrency(
        string policyName,
        int permitLimit,
        Func<HttpContext, string> partition) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
            HasPolicy(context, policyName)
                ? RateLimitPartition.GetConcurrencyLimiter(
                    $"{policyName}:concurrency:{partition(context)}",
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        QueueLimit = 0
                    })
                : RateLimitPartition.GetNoLimiter($"none:{policyName}:concurrency"));

    private static bool HasPolicy(HttpContext context, string policyName) =>
        GetPolicies(context).Contains(policyName, StringComparer.Ordinal);

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";

    private static string UserId(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    private static string ProjectId(HttpContext context) =>
        context.Request.RouteValues.TryGetValue("projectId", out var projectId) && projectId is not null
            ? projectId.ToString()!
            : "unknown";
}
