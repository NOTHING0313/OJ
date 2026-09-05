using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class Problem
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full problem statement shown to users.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Input format and constraints shown with the problem statement.
    /// </summary>
    public string InputDescription { get; set; } = string.Empty;

    /// <summary>
    /// Output format shown with the problem statement.
    /// </summary>
    public string OutputDescription { get; set; } = string.Empty;

    public ProblemKind ProblemKind { get; set; } = ProblemKind.Programming;

    public long AuthoringVersion { get; set; } = 1;

    public int? TimeLimitMs { get; set; }

    public int? MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode? JudgeMode { get; set; } = Domain.Enums.JudgeMode.StandardInputOutput;

    /// <summary>
    /// Bit mask of explicitly allowed judge languages. 0 means unrestricted.
    /// C++17 = 1, C11 = 2, C# = 4.
    /// </summary>
    public int AllowedLanguagesMask { get; set; }

    /// <summary>
    /// Function judge signature and parameter metadata.
    /// </summary>
    public string? FunctionSpecJson { get; set; }

    /// <summary>
    /// Language-specific starter code for function judge mode.
    /// </summary>
    public string? StarterCodeJson { get; set; }

    public ChoiceAnswerRevealPolicy? ChoiceAnswerRevealPolicy { get; set; }

    public DateTimeOffset? ChoiceAnswerRevealAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Current immutable judge definition used for future submissions. Draft problems may not have one.
    /// </summary>
    public Guid? CurrentJudgeRevisionId { get; set; }

    public List<TestCase> TestCases { get; set; } = [];

    public List<Submission> Submissions { get; set; } = [];

    public List<ProblemCollaborator> Collaborators { get; set; } = [];

    public List<ProblemJudgeAsset> JudgeAssets { get; set; } = [];

    public ProblemJudgeRevision? CurrentJudgeRevision { get; set; }

    public List<ProblemJudgeRevision> JudgeRevisions { get; set; } = [];

    public List<ProblemChoiceQuestion> ChoiceQuestions { get; set; } = [];

    public List<ChallengeTask> ChallengeTasks { get; set; } = [];

    /// <summary>
    /// Calculates the current problem score from active test cases.
    /// </summary>
    public int CalculateTotalScore() => ProblemScoreCalculator.Calculate(TestCases);
}
