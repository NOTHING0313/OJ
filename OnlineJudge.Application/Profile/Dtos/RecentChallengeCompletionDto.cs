namespace OnlineJudge.Application.Profile.Dtos;

public class RecentChallengeCompletionDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public string TaskTitle { get; set; } = string.Empty;

    public int Score { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}
