using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class JudgeResult
{
    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Classifies a system error for retry handling. It must be null for user-code verdicts.
    /// </summary>
    public JudgeFailureKind? FailureKind { get; set; }

    public IReadOnlyList<JudgeCaseResult> CaseResults { get; set; } = [];
}
