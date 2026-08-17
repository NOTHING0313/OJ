using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class JudgeResult
{
    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<JudgeCaseResult> CaseResults { get; set; } = [];
}
