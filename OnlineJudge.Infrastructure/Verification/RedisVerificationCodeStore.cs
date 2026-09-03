using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Verification;

internal enum VerificationCodeIssueStatus
{
    Issued = 1,
    Cooldown = 0,
    DailyLimitExceeded = -1
}

internal static class RedisVerificationCodeStore
{
    private const string IssueScript = """
        local cooldownTtl = redis.call('TTL', KEYS[2])
        if cooldownTtl > 0 then return 0 end

        local dailyCount = tonumber(redis.call('GET', KEYS[3]) or '0')
        if dailyCount >= tonumber(ARGV[1]) then return -1 end

        dailyCount = redis.call('INCR', KEYS[3])
        if dailyCount == 1 then redis.call('EXPIRE', KEYS[3], ARGV[2]) end

        redis.call('DEL', KEYS[1])
        redis.call('HSET', KEYS[1], 'issuance', ARGV[3], 'hash', ARGV[4], 'attempts', '0')
        redis.call('EXPIRE', KEYS[1], ARGV[5])
        redis.call('SET', KEYS[2], ARGV[3], 'EX', ARGV[6])
        return 1
        """;

    private const string ConsumeScript = """
        local keyType = redis.call('TYPE', KEYS[1]).ok
        if keyType == 'none' then return 0 end
        if keyType ~= 'hash' then
            redis.call('DEL', KEYS[1])
            return 0
        end

        local expectedHash = redis.call('HGET', KEYS[1], 'hash')
        if not expectedHash then
            redis.call('DEL', KEYS[1])
            return 0
        end

        if expectedHash == ARGV[1] then
            redis.call('DEL', KEYS[1])
            return 1
        end

        local attempts = redis.call('HINCRBY', KEYS[1], 'attempts', 1)
        if attempts >= tonumber(ARGV[2]) then redis.call('DEL', KEYS[1]) end
        return 0
        """;

    private const string CleanupScript = """
        if redis.call('TYPE', KEYS[1]).ok == 'hash'
            and redis.call('HGET', KEYS[1], 'issuance') == ARGV[1] then
            redis.call('DEL', KEYS[1])
        end
        if redis.call('GET', KEYS[2]) == ARGV[1] then redis.call('DEL', KEYS[2]) end
        return 1
        """;

    public static async Task<VerificationCodeIssueStatus> TryIssueAsync(
        IDatabase database,
        string channel,
        string scene,
        string target,
        string issuanceId,
        string codeHash,
        int maxDailySendCount,
        TimeSpan dailyTtl,
        TimeSpan codeTtl,
        TimeSpan cooldownTtl)
    {
        var result = await database.ScriptEvaluateAsync(
            IssueScript,
            [CodeKey(channel, scene, target), CooldownKey(channel, scene, target), DailyKey(channel, scene, target)],
            [
                maxDailySendCount,
                ToSeconds(dailyTtl),
                issuanceId,
                codeHash,
                ToSeconds(codeTtl),
                ToSeconds(cooldownTtl)
            ]);

        return (VerificationCodeIssueStatus)(int)result;
    }

    public static async Task<bool> TryConsumeAsync(
        IDatabase database,
        string channel,
        string scene,
        string target,
        string codeHash,
        int maxAttemptCount)
    {
        var result = await database.ScriptEvaluateAsync(
            ConsumeScript,
            [CodeKey(channel, scene, target)],
            [codeHash, maxAttemptCount]);
        return (int)result == 1;
    }

    public static Task CleanupIssuanceAsync(
        IDatabase database,
        string channel,
        string scene,
        string target,
        string issuanceId) =>
        database.ScriptEvaluateAsync(
            CleanupScript,
            [CodeKey(channel, scene, target), CooldownKey(channel, scene, target)],
            [issuanceId]);

    internal static RedisKey CodeKey(string channel, string scene, string target) =>
        $"{channel}:code:{scene}:{target}";

    internal static RedisKey CooldownKey(string channel, string scene, string target) =>
        $"{channel}:cooldown:{scene}:{target}";

    internal static RedisKey DailyKey(string channel, string scene, string target) =>
        $"{channel}:daily:{scene}:{target}:{DateTimeOffset.UtcNow:yyyyMMdd}";

    private static long ToSeconds(TimeSpan value) => Math.Max(1, (long)Math.Ceiling(value.TotalSeconds));
}
