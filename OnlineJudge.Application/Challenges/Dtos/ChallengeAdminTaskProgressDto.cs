namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeAdminTaskProgressDto
{
    public Guid TaskId { get; set; }

    public string Title { get; set; } = string.Empty;

    public int TaskType { get; set; }

    public int Difficulty { get; set; }

    public int Score { get; set; }

    public int CompletedUserCount { get; set; }
}
