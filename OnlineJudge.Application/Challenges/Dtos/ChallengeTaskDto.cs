using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTaskDto
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ChallengeTaskType TaskType { get; set; }

    public ChallengeTaskDifficulty Difficulty { get; set; }

    public int BoardX { get; set; }

    public int BoardY { get; set; }

    public Guid? AlgorithmProblemId { get; set; }

    public ProblemDifficulty? AlgorithmProblemDifficulty { get; set; }

    public JudgeStatus? MyLatestSubmissionStatus { get; set; }

    public int Score { get; set; }

    public bool IsPublished { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int? CompletedScore { get; set; }

    public int EarnedScore { get; set; }
}
