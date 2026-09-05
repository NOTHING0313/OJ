namespace OnlineJudge.Application.Problems.Requests;

public class ProblemQueryRequest
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
