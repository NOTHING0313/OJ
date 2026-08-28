using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public sealed class LeaderboardSeasonProblemBenchmark
{
    public Guid Id { get; set; }

    public Guid SeasonProblemId { get; set; }

    public JudgeLanguage Language { get; set; }

    public int RuntimeBaselineMs { get; set; }

    public int MemoryBaselineKb { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public LeaderboardSeasonProblem? SeasonProblem { get; set; }
}
