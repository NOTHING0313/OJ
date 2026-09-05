using System.Text.Json.Serialization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ChoiceQuestionDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public string StemMarkdown { get; set; } = string.Empty;
    public ChoiceSelectionMode SelectionMode { get; set; }
    public int Score { get; set; }
    public IReadOnlyList<ChoiceOptionDto> Options { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Guid>? CorrectOptionIds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExplanationMarkdown { get; set; }
}
