using OnlineJudge.Domain.Enums;

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

    public IReadOnlyList<LeaderboardSeasonProblemDto> Problems { get; set; } = [];
}

public class LeaderboardSeasonProblemDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public int BaseScore { get; set; }
}
