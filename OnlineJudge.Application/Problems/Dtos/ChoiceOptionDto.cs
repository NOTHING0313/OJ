namespace OnlineJudge.Application.Problems.Dtos;

public class ChoiceOptionDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
}
