namespace OnlineJudge.Application.Leaderboards.Requests;

public class CreateLeaderboardSeasonRequest
{
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset FreezeAt { get; set; }

    public DateTimeOffset PublicUntil { get; set; }
}
