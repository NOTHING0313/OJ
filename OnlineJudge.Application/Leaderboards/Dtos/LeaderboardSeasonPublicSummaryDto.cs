using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Leaderboards.Dtos;

public class LeaderboardSeasonPublicSummaryResponseDto
{
    public LeaderboardSeasonPublicSummaryDto? Season { get; set; }
}

public class LeaderboardSeasonPublicSummaryDto
{
    public string Name { get; set; } = string.Empty;

    public LeaderboardSeasonStatus Status { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset FreezeAt { get; set; }

    public DateTimeOffset PublicUntil { get; set; }

    public IReadOnlyList<LeaderboardSeasonPublicBoardDto> Boards { get; set; } = [];
}

public class LeaderboardSeasonPublicBoardDto
{
    public LeaderboardSeasonBoardType BoardType { get; set; }

    public Guid? ChallengeId { get; set; }

    public string? ChallengeTitle { get; set; }
}
