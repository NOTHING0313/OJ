namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeLeaderboardProgressDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public IReadOnlyList<ChallengeLeaderboardProgressTaskDto> Tasks { get; set; } = [];

    public IReadOnlyList<ChallengeLeaderboardProgressUserDto> Users { get; set; } = [];
}

public class ChallengeLeaderboardProgressTaskDto
{
    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int Score { get; set; }
}

public class ChallengeLeaderboardProgressUserDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int? Rank { get; set; }

    public int CompletedTaskCount { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }

    public bool IsCurrentUser { get; set; }

    public IReadOnlyList<Guid> CompletedTaskIds { get; set; } = [];
}
