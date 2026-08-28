namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonRankSnapshot
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid UserId { get; set; }

    public int Rank { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset RecordedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

}
