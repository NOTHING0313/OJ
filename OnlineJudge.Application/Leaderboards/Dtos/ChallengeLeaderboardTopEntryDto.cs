namespace OnlineJudge.Application.Leaderboards.Dtos;

public class ChallengeLeaderboardTopEntryDto
{
    public int Rank { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int CompletedTaskCount { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }
}
