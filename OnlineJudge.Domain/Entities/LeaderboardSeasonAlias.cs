namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonAlias
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid UserId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public User? User { get; set; }
}
