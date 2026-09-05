using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ProblemChoiceQuestion
{
    public Guid Id { get; set; }
    public Guid ProblemId { get; set; }
    public int Order { get; set; }
    public string StemMarkdown { get; set; } = string.Empty;
    public ChoiceSelectionMode SelectionMode { get; set; }
    public int Score { get; set; }
    public string ExplanationMarkdown { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Problem? Problem { get; set; }
    public List<ProblemChoiceOption> Options { get; set; } = [];
}
