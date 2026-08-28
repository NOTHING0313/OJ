namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeLeaderboardEntryDto
{
    public int Rank { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? Alias { get; set; }

    public bool IsAnonymous { get; set; }

    public string? AvatarUrl { get; set; }

    public int CompletedTaskCount { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }

    public bool IsCurrentUser { get; set; }
}
