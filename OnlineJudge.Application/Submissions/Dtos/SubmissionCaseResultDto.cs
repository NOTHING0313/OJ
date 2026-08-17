using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Submissions.Dtos;

public class SubmissionCaseResultDto
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    public Guid TestCaseId { get; set; }

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public string? ActualOutput { get; set; }

    public string? ExpectedOutput { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsHidden { get; set; }

    public bool IsRedacted { get; set; }
}
