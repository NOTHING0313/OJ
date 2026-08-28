using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class JudgeRequest
{
    public Guid SubmissionId { get; set; }

    public Guid ProblemId { get; set; }

    public JudgeLanguage Language { get; set; }

    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public string SourceCode { get; set; } = string.Empty;

    public string? FunctionSpecJson { get; set; }

    public int TimeLimitMs { get; set; }

    public int MemoryLimitMb { get; set; }

    public bool CollectAllCaseResults { get; set; }

    public IReadOnlyList<JudgeCompileAsset> CompileAssets { get; set; } = [];

    public IReadOnlyList<JudgeCaseRequest> TestCases { get; set; } = [];
}
