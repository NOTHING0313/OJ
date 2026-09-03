using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class JudgeJob
{
    public Guid SubmissionId { get; set; }

    public JudgeJobStatus Status { get; set; }

    /// <summary>
    /// Number of leases acquired for this job, including expired attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public Guid? LeaseToken { get; set; }

    public string? LeaseOwner { get; set; }

    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset? LastAttemptStartedAt { get; set; }

    public JudgeFailureKind? LastFailureKind { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public Submission? Submission { get; set; }
}
