using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class JudgeCaseResult
{
    public Guid TestCaseId { get; set; }

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public string? ActualOutput { get; set; }

    public string? ErrorMessage { get; set; }
}
