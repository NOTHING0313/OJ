using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ProblemListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public int TimeLimitMs { get; set; }

    public int MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode JudgeMode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
