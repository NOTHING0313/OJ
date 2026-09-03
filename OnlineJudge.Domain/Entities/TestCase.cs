using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class TestCase
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    /// <summary>
    /// Standard input content passed to the submitted program.
    /// </summary>
    public string Input { get; set; } = string.Empty;

    /// <summary>
    /// Expected standard output used by the judge comparison step.
    /// </summary>
    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>
    /// Function judge arguments keyed by parameter name.
    /// </summary>
    public string? ArgumentsJson { get; set; }

    /// <summary>
    /// Function judge expected return value.
    /// </summary>
    public string? ExpectedJson { get; set; }

    public TestCaseVisibility Visibility { get; set; }

    public int Score { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Problem? Problem { get; set; }

    public List<ProblemJudgeRevisionTestCase> JudgeRevisionTestCases { get; set; } = [];
}
