using OnlineJudge.Domain.Enums;
using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Application.Leaderboards.Dtos;

public class LeaderboardSeasonDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset FreezeAt { get; set; }

    public DateTimeOffset PublicUntil { get; set; }

    public LeaderboardSeasonStatus Status { get; set; }

    public LeaderboardSeasonStatus EffectiveStatus { get; set; }

    public bool IsCurrent { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? FrozenAt { get; set; }

    public DateTimeOffset? FinalizedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset? ManuallyFrozenAt { get; set; }

    public LeaderboardScoringRules ScoringRules { get; set; } = new();

    public IReadOnlyList<LeaderboardSeasonProblemDto> Problems { get; set; } = [];
}

public class LeaderboardSeasonProblemDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public int BaseScore { get; set; }

    public int AllowedLanguagesMask { get; set; }

    public IReadOnlyList<LeaderboardSeasonProblemBenchmarkDto> Benchmarks { get; set; } = [];
}

public class LeaderboardSeasonProblemBenchmarkDto
{
    public JudgeLanguage Language { get; set; }

    public int RuntimeBaselineMs { get; set; }

    public int MemoryBaselineKb { get; set; }
}
