using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class SubmissionCaseResult
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public Guid TestCaseId { get; set; }

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    /// <summary>
    /// Actual standard output captured from the submitted program.
    /// </summary>
    public string? ActualOutput { get; set; }

    /// <summary>
    /// Runtime or system error details for this test case.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public Submission? Submission { get; set; }

    public TestCase? TestCase { get; set; }
}
