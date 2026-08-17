using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ChallengeTask
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Task statement shown on the challenge board.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public ChallengeTaskType TaskType { get; set; }

    public ChallengeTaskDifficulty Difficulty { get; set; }

    public int BoardX { get; set; }

    public int BoardY { get; set; }

    public Guid? AlgorithmProblemId { get; set; }

    public int Score { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Challenge? Challenge { get; set; }

    public Problem? AlgorithmProblem { get; set; }

    public List<ChallengeTaskCompletion> Completions { get; set; } = [];

    public List<ChallengeTaskAnswer> Answers { get; set; } = [];

    public List<ChallengeTaskFileSubmission> FileSubmissions { get; set; } = [];
}
