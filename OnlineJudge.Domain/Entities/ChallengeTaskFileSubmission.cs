namespace OnlineJudge.Domain.Entities;

public class ChallengeTaskFileSubmission
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid ChallengeTaskId { get; set; }

    public Guid UserId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public int? ReviewScore { get; set; }

    public string? ReviewComment { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Challenge? Challenge { get; set; }

    public ChallengeTask? ChallengeTask { get; set; }

    public User? User { get; set; }

    public User? ReviewedByUser { get; set; }
}
