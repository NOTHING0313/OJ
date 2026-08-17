using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Challenges.Requests;

public class UpdateChallengeTaskRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ChallengeTaskDifficulty Difficulty { get; set; }

    public int BoardX { get; set; }

    public int BoardY { get; set; }

    public Guid? AlgorithmProblemId { get; set; }

    public int Score { get; set; }

    public bool IsPublished { get; set; }
}
