namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTaskAnswerDto
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid ChallengeTaskId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public int Score { get; set; }
}
