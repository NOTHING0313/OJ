using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Leaderboards.Dtos;

public sealed class SeasonProblemLeaderboardDto
{
    public LeaderboardSeasonDto? Season { get; set; }

    public LeaderboardSeasonProblemDto? Problem { get; set; }

    public IReadOnlyList<SeasonProblemLeaderboardEntryDto> Entries { get; set; } = [];
}

public sealed class SeasonProblemLeaderboardEntryDto
{
    public int Rank { get; set; }

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Alias { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }

    public bool IsCurrentUser { get; set; }

    public int BaseScore { get; set; }

    public int EarnedBaseScore { get; set; }

    public int? TimeRank { get; set; }

    public int TimeBonus { get; set; }

    public JudgeLanguage? PerformanceLanguage { get; set; }

    public int? RuntimeMs { get; set; }

    public int? RuntimeBaselineMs { get; set; }

    public int RuntimeBonus { get; set; }

    public int? MemoryKb { get; set; }

    public int? MemoryBaselineKb { get; set; }

    public int MemoryBonus { get; set; }

    public int PerformanceBonus => RuntimeBonus + MemoryBonus;

    public int TotalProblemScore { get; set; }

    public DateTimeOffset FirstFullScoreAt { get; set; }
}
