namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTaskFileSubmissionDto
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid ChallengeTaskId { get; set; }

    public Guid UserId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int? ReviewScore { get; set; }

    public string? ReviewComment { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public string? ReviewedByUserName { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public bool IsReviewed { get; set; }

    public bool CanWithdrawSubmission { get; set; }
}
