using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ProblemDetailDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InputDescription { get; set; } = string.Empty;

    public string OutputDescription { get; set; } = string.Empty;

    public ProblemKind ProblemKind { get; set; }

    public long AuthoringVersion { get; set; }

    public Guid? CurrentJudgeRevisionId { get; set; }

    public int? TimeLimitMs { get; set; }

    public int? MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode? JudgeMode { get; set; }

    public int AllowedLanguagesMask { get; set; }

    public int TotalScore { get; set; }

    public string? FunctionSpecJson { get; set; }

    public string? StarterCodeJson { get; set; }

    public ChoiceAnswerRevealPolicy? ChoiceAnswerRevealPolicy { get; set; }

    public DateTimeOffset? ChoiceAnswerRevealAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public IReadOnlyList<TestCaseDto> TestCases { get; set; } = [];

    public IReadOnlyList<ChoiceQuestionDto> ChoiceQuestions { get; set; } = [];
}
