namespace OnlineJudge.Domain.Entities;

public class ChallengeTaskAnswer
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid ChallengeTaskId { get; set; }

    public Guid UserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Challenge? Challenge { get; set; }

    public ChallengeTask? ChallengeTask { get; set; }

    public User? User { get; set; }
}
