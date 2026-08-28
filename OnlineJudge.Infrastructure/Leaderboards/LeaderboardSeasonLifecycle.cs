using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Leaderboards;

internal static class LeaderboardSeasonLifecycle
{
    public static LeaderboardSeasonStatus GetEffectiveStatus(LeaderboardSeason season, DateTimeOffset now)
    {
        if (!season.IsCurrent || season.Status == LeaderboardSeasonStatus.Archived) return LeaderboardSeasonStatus.Archived;
        if (season.Status == LeaderboardSeasonStatus.Public) return LeaderboardSeasonStatus.Public;
        if (season.Status == LeaderboardSeasonStatus.Frozen) return LeaderboardSeasonStatus.Frozen;
        if (now < season.StartAt) return LeaderboardSeasonStatus.Scheduled;
        return now < season.FreezeAt ? LeaderboardSeasonStatus.Active : LeaderboardSeasonStatus.Frozen;
    }
}
