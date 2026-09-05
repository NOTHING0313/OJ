using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ProblemListItemDto
{
    public Guid Id { get; set; }

    public ProblemDifficulty Difficulty { get; set; } = ProblemDifficulty.Unrated;

    public string Title { get; set; } = string.Empty;

    public ProblemKind ProblemKind { get; set; }

    public int? TimeLimitMs { get; set; }

    public int? MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode? JudgeMode { get; set; }

    public int AllowedLanguagesMask { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
