namespace OnlineJudge.Application.Leaderboards.Dtos;

public class SeasonLeaderboardDto
{
    public LeaderboardSeasonDto? Season { get; set; }

    public IReadOnlyList<SeasonLeaderboardEntryDto> Entries { get; set; } = [];
}

public class SeasonLeaderboardEntryDto
{
    public int Rank { get; set; }

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }

    public bool IsCurrentUser { get; set; }

    public int TotalScore { get; set; }

    public int BaseScore { get; set; }

    public int SolvedCount { get; set; }

    public int TimeBonus { get; set; }

    public int RuntimeBonus { get; set; }

    public int MemoryBonus { get; set; }

    public int PerformanceBonus => RuntimeBonus + MemoryBonus;

    public DateTimeOffset LastScoreImprovedAt { get; set; }
}
