namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeAdminUserTaskStatusDto
{
    public Guid TaskId { get; set; }

    public string TaskTitle { get; set; } = string.Empty;

    public int TaskType { get; set; }

    public int Difficulty { get; set; }

    public int Score { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? CompletedScore { get; set; }

    public Guid? SubmissionId { get; set; }

    public Guid? FileSubmissionId { get; set; }

    public string? OriginalFileName { get; set; }

    public long? FileSizeBytes { get; set; }

    public int? ReviewScore { get; set; }

    public string? ReviewComment { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewedByUserName { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public bool IsReviewed { get; set; }
}
