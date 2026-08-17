namespace OnlineJudge.Application.Problems.Requests;

public class ImportTestCasesRequest
{
    public IReadOnlyList<ImportTestCaseItemRequest> Items { get; set; } = [];
}
