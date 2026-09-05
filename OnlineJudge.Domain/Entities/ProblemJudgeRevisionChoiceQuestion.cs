using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ProblemJudgeRevisionChoiceQuestion
{
    public Guid Id { get; set; }
    public Guid ProblemJudgeRevisionId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public int Order { get; set; }
    public string StemMarkdown { get; set; } = string.Empty;
    public ChoiceSelectionMode SelectionMode { get; set; }
    public int Score { get; set; }
    public string ExplanationMarkdown { get; set; } = string.Empty;
    public ProblemJudgeRevision? ProblemJudgeRevision { get; set; }
    public List<ProblemJudgeRevisionChoiceOption> Options { get; set; } = [];
}
