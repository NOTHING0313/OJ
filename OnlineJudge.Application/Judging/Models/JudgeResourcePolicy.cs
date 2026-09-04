namespace OnlineJudge.Application.Judging.Models;

/// <summary>
/// Shared input and resource boundaries applied before judge work is persisted or executed.
/// </summary>
public sealed class JudgeResourcePolicy
{
    public const string SectionName = "JudgeResourcePolicy";

    public static JudgeResourcePolicy Default { get; } = new();

    public int MaxSourceCodeBytes { get; init; } = 512 * 1024;

    public int MaxProblemTitleCharacters { get; init; } = 200;

    public int MaxProblemContentBytes { get; init; } = 256 * 1024;

    public int MinTimeLimitMs { get; init; } = 100;

    public int MaxTimeLimitMs { get; init; } = 10_000;

    public int MaxDeclaredTestTimeBudgetMs { get; init; } = 120_000;

    public int MinMemoryLimitMb { get; init; } = 16;

    public int MaxMemoryLimitMb { get; init; } = 512;

    public int MaxTestCases { get; init; } = 200;

    public int MaxTestCaseFieldBytes { get; init; } = 1024 * 1024;

    public long MaxAggregateTestDataBytes { get; init; } = 64L * 1024 * 1024;

    public int MaxImportTestCases { get; init; } = 200;

    public long MaxImportPayloadBytes { get; init; } = 64L * 1024 * 1024;

    public int SubmissionJudgeWallTimeSeconds { get; init; } = 180;
}
