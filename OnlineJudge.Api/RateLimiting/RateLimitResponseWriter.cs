using System.Globalization;
using System.Threading.RateLimiting;

namespace OnlineJudge.Api.RateLimiting;

public sealed record RateLimitErrorResponse(string ErrorCode, string Message, int? RetryAfterSeconds);

public static class RateLimitResponseWriter
{
    public const string ErrorCode = "RATE_LIMITED";

    public static RateLimitErrorResponse CreatePayload(string policyName, int? retryAfterSeconds) =>
        new(ErrorCode, MessageFor(policyName), retryAfterSeconds);

    public static int? GetRetryAfterSeconds(RateLimitLease lease)
    {
        if (!lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return null;
        }

        return Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
    }

    public static void SetRetryAfterHeader(HttpResponse response, int? retryAfterSeconds)
    {
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string MessageFor(string policyName) => policyName switch
    {
        RateLimitPolicies.AuthLogin => "登录尝试过于频繁，请稍后重试。",
        RateLimitPolicies.Submission => "提交过于频繁，请稍后再试。",
        RateLimitPolicies.TeamGitSync => "同步过于频繁，请稍后再试。",
        _ => "请求过于频繁，请稍后重试。"
    };
}
