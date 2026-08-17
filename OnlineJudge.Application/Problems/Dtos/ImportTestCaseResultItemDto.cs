using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ImportTestCaseResultItemDto
{
    public Guid Id { get; set; }

    public int Score { get; set; }

    public TestCaseVisibility Visibility { get; set; }
}
