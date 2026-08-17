using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class TestCaseDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    public string? ArgumentsJson { get; set; }

    public string? ExpectedJson { get; set; }

    public TestCaseVisibility Visibility { get; set; }

    public int Score { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
