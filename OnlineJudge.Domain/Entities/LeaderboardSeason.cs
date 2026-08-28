using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeason
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset FreezeAt { get; set; }

    public DateTimeOffset PublicUntil { get; set; }

    public LeaderboardSeasonStatus Status { get; set; }

    public bool IsCurrent { get; set; }

    public string ScoringRulesJson { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }

    public List<LeaderboardSeasonProblem> Problems { get; set; } = [];

    public List<LeaderboardUserProblemScore> UserProblemScores { get; set; } = [];

    public List<LeaderboardSeasonAlias> Aliases { get; set; } = [];

    public List<LeaderboardSeasonArchiveEntry> ArchiveEntries { get; set; } = [];
}
