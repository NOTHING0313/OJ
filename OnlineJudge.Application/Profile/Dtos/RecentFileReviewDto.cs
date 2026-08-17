namespace OnlineJudge.Application.Profile.Dtos;

public class RecentFileReviewDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public string TaskTitle { get; set; } = string.Empty;

    public int? ReviewScore { get; set; }

    public string? ReviewComment { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
