namespace OnlineJudge.Application.Problems.Dtos;

public class ImportTestCasesResultDto
{
    public string Message { get; set; } = string.Empty;

    public int ImportedCount { get; set; }

    public IReadOnlyList<ImportTestCaseResultItemDto> Items { get; set; } = [];

    public IReadOnlyList<ImportTestCaseErrorDto> Errors { get; set; } = [];
}
