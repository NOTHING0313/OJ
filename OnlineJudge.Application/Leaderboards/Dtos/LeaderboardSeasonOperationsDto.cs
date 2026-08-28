namespace OnlineJudge.Application.Leaderboards.Dtos;

public class LeaderboardSeasonHistorySummaryDto
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset FreezeAt { get; set; }
    public DateTimeOffset PublicUntil { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public int ParticipantCount { get; set; }
    public IReadOnlyList<LeaderboardSeasonHistoryTopEntryDto> Top3 { get; set; } = [];
}

public class LeaderboardSeasonHistoryTopEntryDto
{
    public int Rank { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int FinalScore { get; set; }
}

public class LeaderboardSeasonRankPointDto
{
    public DateTimeOffset RecordedAt { get; set; }
    public int Rank { get; set; }
    public int TotalScore { get; set; }
}

public class LeaderboardSeasonPersonalDto
{
    public LeaderboardSeasonDto? Season { get; set; }
    public int? CurrentRank { get; set; }
    public int TotalParticipants { get; set; }
    public int TotalScore { get; set; }
    public int TotalBaseScore { get; set; }
    public int TotalTimeBonus { get; set; }
    public int TotalRuntimeBonus { get; set; }
    public int TotalMemoryBonus { get; set; }
    public int SolvedCount { get; set; }
    public int SeasonProblemCount { get; set; }
    public int Top10ProblemCount { get; set; }
    public int FirstPlaceProblemCount { get; set; }
    public int? BestRank { get; set; }
    public int? RankChange { get; set; }
    public IReadOnlyList<LeaderboardSeasonPersonalProblemDto> Problems { get; set; } = [];
    public IReadOnlyList<LeaderboardSeasonRankPointDto> RankHistory { get; set; } = [];
}

public class LeaderboardSeasonPersonalProblemDto
{
    public Guid ProblemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Score { get; set; }
    public int? TimeRank { get; set; }
    public int TimeBonus { get; set; }
    public int PerformanceBonus { get; set; }
}

public class LeaderboardSeasonPersonalHistoryDto
{
    public Guid SeasonId { get; set; }
    public string SeasonName { get; set; } = string.Empty;
    public int FinalRank { get; set; }
    public int FinalScore { get; set; }
    public int SolvedCount { get; set; }
    public int TimeBonus { get; set; }
    public int PerformanceBonus { get; set; }
    public IReadOnlyList<LeaderboardSeasonArchiveProblemScoreDto> Problems { get; set; } = [];
}
