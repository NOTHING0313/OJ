namespace OnlineJudge.Application.Judging.Models;

public class JudgeCaseRequest
{
    public Guid TestCaseId { get; set; }

    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string? ArgumentsJson { get; set; }

    public string? ExpectedJson { get; set; }
}
