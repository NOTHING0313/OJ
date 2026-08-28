namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonArchiveProblemScore
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid ArchiveEntryId { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitleSnapshot { get; set; } = string.Empty;

    public int BaseScore { get; set; }

    public int EarnedBaseScore { get; set; }

    public int? TimeRank { get; set; }

    public DateTimeOffset FirstFullScoreAt { get; set; }

    public int TimeBonus { get; set; }

    public OnlineJudge.Domain.Enums.JudgeLanguage? PerformanceLanguage { get; set; }

    public int? RuntimeMs { get; set; }

    public int? RuntimeBaselineMs { get; set; }

    public int RuntimeBonus { get; set; }

    public int? MemoryKb { get; set; }

    public int? MemoryBaselineKb { get; set; }

    public int MemoryBonus { get; set; }

    public int FinalProblemScore { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public LeaderboardSeasonArchiveEntry? ArchiveEntry { get; set; }
}
