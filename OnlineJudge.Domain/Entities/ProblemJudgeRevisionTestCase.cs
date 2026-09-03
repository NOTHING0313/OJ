using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

/// <summary>
/// Immutable test-case snapshot belonging to one judge revision.
/// </summary>
public class ProblemJudgeRevisionTestCase
{
    public Guid Id { get; set; }

    public Guid ProblemJudgeRevisionId { get; set; }

    public Guid SourceTestCaseId { get; set; }

    public int Order { get; set; }

    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string? ArgumentsJson { get; set; }

    public string? ExpectedJson { get; set; }

    public TestCaseVisibility Visibility { get; set; }

    public int Score { get; set; }

    public ProblemJudgeRevision? ProblemJudgeRevision { get; set; }

    public TestCase? SourceTestCase { get; set; }
}
