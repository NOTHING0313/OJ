using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

/// <summary>
/// Immutable snapshot of every problem field that can affect a judge result.
/// </summary>
public class ProblemJudgeRevision
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public int RevisionNumber { get; set; }

    public ProblemKind ProblemKind { get; set; } = ProblemKind.Programming;

    public JudgeMode? JudgeMode { get; set; }

    public int AllowedLanguagesMask { get; set; }

    public string? FunctionSpecJson { get; set; }

    public int? TimeLimitMs { get; set; }

    public int? MemoryLimitMb { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Problem? Problem { get; set; }

    public List<ProblemJudgeRevisionTestCase> TestCases { get; set; } = [];

    public List<ProblemJudgeRevisionAsset> Assets { get; set; } = [];

    public List<ProblemJudgeRevisionChoiceQuestion> ChoiceQuestions { get; set; } = [];
}
