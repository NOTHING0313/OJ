using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonBoard
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public LeaderboardSeasonBoardType BoardType { get; set; }

    public Guid? ChallengeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public Challenge? Challenge { get; set; }
}
