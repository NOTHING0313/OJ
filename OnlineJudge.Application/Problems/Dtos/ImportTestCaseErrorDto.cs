namespace OnlineJudge.Application.Problems.Dtos;

public class ImportTestCaseErrorDto
{
    public int Index { get; set; }

    public string Field { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
