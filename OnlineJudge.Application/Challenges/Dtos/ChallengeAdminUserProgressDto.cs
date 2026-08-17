namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeAdminUserProgressDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public int CompletedTaskCount { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }

    public IReadOnlyList<ChallengeAdminUserTaskStatusDto> TaskStatuses { get; set; } = [];
}
