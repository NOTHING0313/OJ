namespace OnlineJudge.Domain.Entities;

public class ChallengeTaskCompletion
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid ChallengeTaskId { get; set; }

    public Guid UserId { get; set; }

    public Guid? SubmissionId { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsCompleted { get; set; }

    public int Score { get; set; }

    public Challenge? Challenge { get; set; }

    public ChallengeTask? ChallengeTask { get; set; }

    public User? User { get; set; }

    public Submission? Submission { get; set; }
}
