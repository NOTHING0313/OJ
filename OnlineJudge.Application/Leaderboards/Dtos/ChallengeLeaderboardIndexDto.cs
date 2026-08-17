namespace OnlineJudge.Application.Leaderboards.Dtos;

public class ChallengeLeaderboardIndexDto
{
    public IReadOnlyList<ChallengeLeaderboardSummaryDto> Challenges { get; set; } = [];
}
