namespace OnlineJudge.Application.Auth.Services;

public sealed record LoginAbuseCheckResult(bool IsAllowed, int? RetryAfterSeconds = null, bool IsDegraded = false);

public interface ILoginAbuseProtection
{
    Task<LoginAbuseCheckResult> CheckAsync(string account, CancellationToken cancellationToken = default);

    Task RecordFailedPasswordAsync(string account, CancellationToken cancellationToken = default);

    Task ResetAsync(string account, CancellationToken cancellationToken = default);
}
