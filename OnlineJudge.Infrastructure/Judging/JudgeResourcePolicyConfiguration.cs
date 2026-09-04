using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Infrastructure.Judging;

internal static class JudgeResourcePolicyConfiguration
{
    public static JudgeResourcePolicy FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(JudgeResourcePolicy.SectionName);
        var policy = new JudgeResourcePolicy
        {
            MaxSourceCodeBytes = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxSourceCodeBytes), JudgeResourcePolicy.Default.MaxSourceCodeBytes),
            MaxProblemTitleCharacters = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxProblemTitleCharacters), JudgeResourcePolicy.Default.MaxProblemTitleCharacters),
            MaxProblemContentBytes = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxProblemContentBytes), JudgeResourcePolicy.Default.MaxProblemContentBytes),
            MinTimeLimitMs = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MinTimeLimitMs), JudgeResourcePolicy.Default.MinTimeLimitMs),
            MaxTimeLimitMs = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxTimeLimitMs), JudgeResourcePolicy.Default.MaxTimeLimitMs),
            MaxDeclaredTestTimeBudgetMs = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxDeclaredTestTimeBudgetMs), JudgeResourcePolicy.Default.MaxDeclaredTestTimeBudgetMs),
            MinMemoryLimitMb = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MinMemoryLimitMb), JudgeResourcePolicy.Default.MinMemoryLimitMb),
            MaxMemoryLimitMb = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxMemoryLimitMb), JudgeResourcePolicy.Default.MaxMemoryLimitMb),
            MaxTestCases = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxTestCases), JudgeResourcePolicy.Default.MaxTestCases),
            MaxTestCaseFieldBytes = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxTestCaseFieldBytes), JudgeResourcePolicy.Default.MaxTestCaseFieldBytes),
            MaxAggregateTestDataBytes = ReadPositiveLong(section, nameof(JudgeResourcePolicy.MaxAggregateTestDataBytes), JudgeResourcePolicy.Default.MaxAggregateTestDataBytes),
            MaxImportTestCases = ReadPositiveInt(section, nameof(JudgeResourcePolicy.MaxImportTestCases), JudgeResourcePolicy.Default.MaxImportTestCases),
            MaxImportPayloadBytes = ReadPositiveLong(section, nameof(JudgeResourcePolicy.MaxImportPayloadBytes), JudgeResourcePolicy.Default.MaxImportPayloadBytes),
            SubmissionJudgeWallTimeSeconds = ReadPositiveInt(section, nameof(JudgeResourcePolicy.SubmissionJudgeWallTimeSeconds), JudgeResourcePolicy.Default.SubmissionJudgeWallTimeSeconds)
        };

        if (policy.MinTimeLimitMs > policy.MaxTimeLimitMs
            || policy.MinMemoryLimitMb > policy.MaxMemoryLimitMb
            || policy.MaxImportTestCases > policy.MaxTestCases
            || policy.MaxImportPayloadBytes > policy.MaxAggregateTestDataBytes
            || policy.MaxDeclaredTestTimeBudgetMs < policy.MinTimeLimitMs)
        {
            throw new InvalidOperationException($"Configuration section '{JudgeResourcePolicy.SectionName}' contains inconsistent limits.");
        }

        return policy;
    }

    private static int ReadPositiveInt(IConfiguration section, string key, int fallback)
    {
        var configured = section[key];
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        return int.TryParse(configured, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"Configuration value '{JudgeResourcePolicy.SectionName}:{key}' must be a positive integer.");
    }

    private static long ReadPositiveLong(IConfiguration section, string key, long fallback)
    {
        var configured = section[key];
        if (string.IsNullOrWhiteSpace(configured)) return fallback;
        return long.TryParse(configured, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"Configuration value '{JudgeResourcePolicy.SectionName}:{key}' must be a positive integer.");
    }
}
