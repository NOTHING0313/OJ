namespace OnlineJudge.Application.Challenges.Dtos;

using OnlineJudge.Domain.Enums;

public class ChallengeListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public bool IsPublished { get; set; }

    public ChallengeParticipationMode ParticipationMode { get; set; }

    public int TeamCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int TotalTaskCount { get; set; }

    public int CompletedTaskCount { get; set; }

    public bool CanManage { get; set; }
}
