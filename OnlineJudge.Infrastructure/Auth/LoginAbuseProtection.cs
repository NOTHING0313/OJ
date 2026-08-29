using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Auth.Services;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Auth;

internal interface ILoginAbuseStore
{
    Task<int> CheckAsync(string accountHash);

    Task RecordFailedPasswordAsync(string accountHash);

    Task ResetAsync(string accountHash);
}

internal sealed class LoginAbuseStoreUnavailableException(Exception innerException) : Exception("Login abuse store is unavailable.", innerException);

internal sealed class RedisLoginAbuseStore(IConnectionMultiplexer redis) : ILoginAbuseStore
{
    private const int AccountRequestLimit = 5;
    private const int RequestWindowSeconds = 60;
    private const int FailureStateSeconds = 900;

    private const string CheckScript = """
        local requestCount = redis.call('INCR', KEYS[1])
        if requestCount == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]) end
        local requestTtl = redis.call('TTL', KEYS[1])
        local cooldownTtl = redis.call('TTL', KEYS[2])
        if requestCount > tonumber(ARGV[2]) then return math.max(requestTtl, cooldownTtl, 1) end
        if cooldownTtl > 0 then return cooldownTtl end
        return 0
        """;

    private const string FailureScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]) end
        local cooldown = 0
        if count == 5 then cooldown = 30
        elseif count == 6 then cooldown = 60
        elseif count == 7 then cooldown = 120
        elseif count == 8 then cooldown = 300
        elseif count >= 9 then cooldown = 900 end
        if cooldown > 0 then redis.call('SET', KEYS[2], '1', 'EX', cooldown) end
        return cooldown
        """;

    public async Task<int> CheckAsync(string accountHash)
    {
        try
        {
            var result = await redis.GetDatabase().ScriptEvaluateAsync(
                CheckScript,
                [RequestKey(accountHash), CooldownKey(accountHash)],
                [RequestWindowSeconds, AccountRequestLimit]);
            return (int)result;
        }
        catch (RedisException ex)
        {
            throw new LoginAbuseStoreUnavailableException(ex);
        }
    }

    public async Task RecordFailedPasswordAsync(string accountHash)
    {
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                FailureScript,
                [FailureKey(accountHash), CooldownKey(accountHash)],
                [FailureStateSeconds]);
        }
        catch (RedisException ex)
        {
            throw new LoginAbuseStoreUnavailableException(ex);
        }
    }

    public async Task ResetAsync(string accountHash)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync([
                RequestKey(accountHash),
                FailureKey(accountHash),
                CooldownKey(accountHash)
            ]);
        }
        catch (RedisException ex)
        {
            throw new LoginAbuseStoreUnavailableException(ex);
        }
    }

    private static RedisKey RequestKey(string hash) => $"auth:login:req:{hash}";

    private static RedisKey FailureKey(string hash) => $"auth:login:fail:{hash}";

    private static RedisKey CooldownKey(string hash) => $"auth:login:cooldown:{hash}";
}

internal sealed class LoginAbuseProtection(ILoginAbuseStore store, ILogger<LoginAbuseProtection> logger) : ILoginAbuseProtection
{
    public async Task<LoginAbuseCheckResult> CheckAsync(string account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var retryAfter = await store.CheckAsync(HashAccount(account));
            return retryAfter > 0
                ? new LoginAbuseCheckResult(false, retryAfter)
                : new LoginAbuseCheckResult(true);
        }
        catch (LoginAbuseStoreUnavailableException ex)
        {
            logger.LogWarning(ex, "Login abuse protection is degraded; the request IP limiter remains active.");
            return new LoginAbuseCheckResult(true, IsDegraded: true);
        }
    }

    public async Task RecordFailedPasswordAsync(string account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await store.RecordFailedPasswordAsync(HashAccount(account));
        }
        catch (LoginAbuseStoreUnavailableException ex)
        {
            logger.LogWarning(ex, "Login failure backoff is degraded; the request IP limiter remains active.");
        }
    }

    public async Task ResetAsync(string account, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await store.ResetAsync(HashAccount(account));
        }
        catch (LoginAbuseStoreUnavailableException ex)
        {
            logger.LogWarning(ex, "Login failure state reset is degraded; the request IP limiter remains active.");
        }
    }

    internal static string HashAccount(string account)
    {
        var normalized = account.Trim().ToLowerInvariant();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
