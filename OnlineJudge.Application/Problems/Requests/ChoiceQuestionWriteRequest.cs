using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class ChoiceQuestionWriteRequest
{
    public Guid? Id { get; set; }
    public string StemMarkdown { get; set; } = string.Empty;
    public ChoiceSelectionMode SelectionMode { get; set; } = ChoiceSelectionMode.Single;
    public int Score { get; set; } = 1;
    public string ExplanationMarkdown { get; set; } = string.Empty;
    public IReadOnlyList<ChoiceOptionWriteRequest> Options { get; set; } = [];
}

public class ChoiceOptionWriteRequest
{
    public Guid? Id { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
