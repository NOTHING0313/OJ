using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Judging;

public sealed class JudgeJobOptions
{
    public const string SectionName = "JudgeJobs";

    public int LeaseDurationSeconds { get; init; } = 120;

    public int HeartbeatIntervalSeconds { get; init; } = 30;

    public int PollIntervalMilliseconds { get; init; } = 1000;

    public int MaxAttempts { get; init; } = 3;

    public int RetryBaseDelaySeconds { get; init; } = 5;

    public int RetryMaxDelaySeconds { get; init; } = 60;

    public int RedisSignalTimeoutMilliseconds { get; init; } = 1000;

    public TimeSpan LeaseDuration => TimeSpan.FromSeconds(LeaseDurationSeconds);

    public TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

    public TimeSpan PollInterval => TimeSpan.FromMilliseconds(PollIntervalMilliseconds);

    public TimeSpan RedisSignalTimeout => TimeSpan.FromMilliseconds(RedisSignalTimeoutMilliseconds);

    public static JudgeJobOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var options = new JudgeJobOptions
        {
            LeaseDurationSeconds = ReadPositive(section, nameof(LeaseDurationSeconds), 120),
            HeartbeatIntervalSeconds = ReadPositive(section, nameof(HeartbeatIntervalSeconds), 30),
            PollIntervalMilliseconds = ReadPositive(section, nameof(PollIntervalMilliseconds), 1000),
            MaxAttempts = Math.Min(ReadPositive(section, nameof(MaxAttempts), 3), 10),
            RetryBaseDelaySeconds = ReadPositive(section, nameof(RetryBaseDelaySeconds), 5),
            RetryMaxDelaySeconds = ReadPositive(section, nameof(RetryMaxDelaySeconds), 60),
            RedisSignalTimeoutMilliseconds = ReadPositive(section, nameof(RedisSignalTimeoutMilliseconds), 1000)
        };

        if (options.HeartbeatIntervalSeconds * 3 > options.LeaseDurationSeconds)
        {
            throw new InvalidOperationException("JudgeJobs heartbeat interval must not exceed one third of the lease duration.");
        }

        if (options.RetryBaseDelaySeconds > options.RetryMaxDelaySeconds)
        {
            throw new InvalidOperationException("JudgeJobs retry base delay must not exceed the retry maximum delay.");
        }

        return options;
    }

    public TimeSpan GetRetryDelay(int attemptNumber)
    {
        var exponent = Math.Max(0, Math.Min(attemptNumber - 1, 30));
        var seconds = RetryBaseDelaySeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(seconds, RetryMaxDelaySeconds));
    }

    private static int ReadPositive(IConfiguration section, string key, int fallback) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : fallback;
}
