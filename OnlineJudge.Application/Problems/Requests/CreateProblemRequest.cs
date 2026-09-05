using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class CreateProblemRequest
{
    public ProblemKind ProblemKind { get; set; } = ProblemKind.Programming;
    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(ProblemDifficulty))]
    public ProblemDifficulty Difficulty { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InputDescription { get; set; } = string.Empty;

    public string OutputDescription { get; set; } = string.Empty;

    public int? TimeLimitMs { get; set; }

    public int? MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode? JudgeMode { get; set; } = Domain.Enums.JudgeMode.StandardInputOutput;

    public int AllowedLanguagesMask { get; set; }

    public string? FunctionSpecJson { get; set; }

    public string? StarterCodeJson { get; set; }

    public ChoiceAnswerRevealPolicy? ChoiceAnswerRevealPolicy { get; set; }

    public DateTimeOffset? ChoiceAnswerRevealAt { get; set; }

    public IReadOnlyList<ChoiceQuestionWriteRequest> ChoiceQuestions { get; set; } = [];
}
