using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class UpdateProblemRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InputDescription { get; set; } = string.Empty;

    public string OutputDescription { get; set; } = string.Empty;

    public int TimeLimitMs { get; set; }

    public int MemoryLimitMb { get; set; }

    public bool IsPublished { get; set; }

    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public string? FunctionSpecJson { get; set; }

    public string? StarterCodeJson { get; set; }
}
