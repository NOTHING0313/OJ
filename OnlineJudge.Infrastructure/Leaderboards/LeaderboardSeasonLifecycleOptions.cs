using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardSeasonLifecycleOptions
{
    public const string SectionName = "LeaderboardSeasonLifecycle";

    public bool Enabled { get; init; } = true;

    public int PollIntervalSeconds { get; init; } = 30;

    public int RankSnapshotIntervalMinutes { get; init; } = 60;

    public static LeaderboardSeasonLifecycleOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = !bool.TryParse(configuration[$"{SectionName}:Enabled"], out var configuredEnabled) || configuredEnabled;
        var poll = int.TryParse(configuration[$"{SectionName}:PollIntervalSeconds"], out var configuredPoll) ? configuredPoll : 30;
        var snapshot = int.TryParse(configuration[$"{SectionName}:RankSnapshotIntervalMinutes"], out var configuredSnapshot) ? configuredSnapshot : 60;
        return new LeaderboardSeasonLifecycleOptions
        {
            Enabled = enabled,
            PollIntervalSeconds = Math.Max(10, poll),
            RankSnapshotIntervalMinutes = Math.Max(1, snapshot)
        };
    }
}
