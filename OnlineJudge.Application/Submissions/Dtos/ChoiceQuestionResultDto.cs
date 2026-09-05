using System.Text.Json.Serialization;

namespace OnlineJudge.Application.Submissions.Dtos;

public class ChoiceQuestionResultDto
{
    public Guid QuestionId { get; set; }
    public string StemMarkdown { get; set; } = string.Empty;
    public OnlineJudge.Domain.Enums.ChoiceSelectionMode SelectionMode { get; set; }
    public bool IsCorrect { get; set; }
    public int Score { get; set; }
    public IReadOnlyList<Guid> SelectedOptionIds { get; set; } = [];
    public IReadOnlyList<ChoiceResultOptionDto> Options { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Guid>? CorrectOptionIds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExplanationMarkdown { get; set; }
}

public class ChoiceResultOptionDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string ContentMarkdown { get; set; } = string.Empty;
}
