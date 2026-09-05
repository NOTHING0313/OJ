namespace OnlineJudge.Domain.Entities;

public class ProblemJudgeRevisionChoiceOption
{
    public Guid Id { get; set; }
    public Guid RevisionQuestionId { get; set; }
    public Guid SourceOptionId { get; set; }
    public int Order { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public ProblemJudgeRevisionChoiceQuestion? RevisionQuestion { get; set; }
}
