using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class UpdateTestCaseRequest
{
    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string? ArgumentsJson { get; set; }

    public string? ExpectedJson { get; set; }

    public TestCaseVisibility Visibility { get; set; }

    public int Score { get; set; }
}
