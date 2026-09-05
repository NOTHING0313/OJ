namespace OnlineJudge.Domain.Entities;

public class ProblemChoiceOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public int Order { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ProblemChoiceQuestion? Question { get; set; }
}
