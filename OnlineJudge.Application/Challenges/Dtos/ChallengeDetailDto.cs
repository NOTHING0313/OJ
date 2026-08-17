namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeDetailDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int TotalTaskCount { get; set; }

    public int CompletedTaskCount { get; set; }

    public bool CanManage { get; set; }

    public IReadOnlyList<ChallengeTaskDto> Tasks { get; set; } = [];
}
