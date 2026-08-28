namespace OnlineJudge.Application.Leaderboards.Dtos;

public class LeaderboardSeasonArchiveDto
{
    public Guid SeasonId { get; set; }

    public string SeasonName { get; set; } = string.Empty;

    public IReadOnlyList<LeaderboardSeasonArchiveEntryDto> Entries { get; set; } = [];
}

public class LeaderboardSeasonArchiveEntryDto
{
    public Guid UserId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public bool WasAnonymous { get; set; }

    public int FinalRank { get; set; }

    public int FinalScore { get; set; }

    public int FinalBaseScore { get; set; }

    public int SolvedCount { get; set; }

    public DateTimeOffset LastScoreImprovedAt { get; set; }

    public IReadOnlyList<LeaderboardSeasonArchiveProblemScoreDto> ProblemScores { get; set; } = [];
}

public class LeaderboardSeasonArchiveProblemScoreDto
{
    public Guid ProblemId { get; set; }

    public string ProblemTitleSnapshot { get; set; } = string.Empty;

    public int BaseScore { get; set; }

    public int EarnedBaseScore { get; set; }

    public int TimeBonus { get; set; }

    public int RuntimeBonus { get; set; }

    public int MemoryBonus { get; set; }

    public int FinalProblemScore { get; set; }
}
