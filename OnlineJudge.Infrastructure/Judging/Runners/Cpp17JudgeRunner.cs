using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;
using System.Text.RegularExpressions;

namespace OnlineJudge.Infrastructure.Judging.Runners;

public partial class Cpp17JudgeRunner(IJudgeSandbox judgeSandbox, IFunctionJudgeCodeBuilder functionJudgeCodeBuilder) : IJudgeRunner
{
    internal static readonly LanguageJudgeProfile Profile = new()
    {
        Language = JudgeLanguage.Cpp17,
        DisplayName = "C++17",
        SourceFileName = "main.cpp",
        CompileCommand = "g++ main.cpp -std=c++17 -O2 -pipe -s -o main",
        CompileAssetSourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cpp", ".cc", ".cxx" },
        CompileMemoryLimitMb = 512,
        RunCommand = "./main",
        DockerImageName = "onlinejudge-cpp17-sandbox:latest"
    };

    public bool Supports(JudgeLanguage language)
    {
        return language == JudgeLanguage.Cpp17;
    }

    public Task<JudgeResult> RunAsync(JudgeRequest request, CancellationToken cancellationToken = default)
    {
        if (request.JudgeMode == JudgeMode.Function)
        {
            return RunFunctionAsync(request, cancellationToken);
        }

        return judgeSandbox.RunAsync(request, Profile, cancellationToken);
    }

    private async Task<JudgeResult> RunFunctionAsync(JudgeRequest request, CancellationToken cancellationToken)
    {
        if (ContainsMainFunction(request.SourceCode))
        {
            return new JudgeResult
            {
                Status = JudgeStatus.CompileError,
                ErrorMessage = "函数式题目不需要编写 main，请只提交 Solution 类中的函数实现。"
            };
        }

        var buildResult = functionJudgeCodeBuilder.Build(request);
        if (buildResult.IsFailure || buildResult.Value is null)
        {
            return new JudgeResult
            {
                Status = JudgeStatus.SystemError,
                ErrorMessage = buildResult.ErrorMessage ?? "Function judge request is invalid.",
                FailureKind = JudgeFailureKind.PermanentConfiguration
            };
        }

        var judgeResult = await judgeSandbox.RunAsync(buildResult.Value, Profile, cancellationToken);
        return PostProcessFunctionResult(judgeResult, request);
    }

    private static JudgeResult PostProcessFunctionResult(JudgeResult judgeResult, JudgeRequest originalRequest)
    {
        if (judgeResult.CaseResults.Count == 0)
        {
            return judgeResult;
        }

        var originalCases = originalRequest.TestCases.ToDictionary(testCase => testCase.TestCaseId);
        var processedCases = judgeResult.CaseResults.Select(caseResult =>
        {
            if (caseResult.Status == JudgeStatus.Accepted && originalCases.TryGetValue(caseResult.TestCaseId, out var acceptedCase))
            {
                return new JudgeCaseResult
                {
                    TestCaseId = caseResult.TestCaseId,
                    Status = caseResult.Status,
                    TimeUsedMs = caseResult.TimeUsedMs,
                    MemoryUsedKb = caseResult.MemoryUsedKb,
                    ActualOutput = MinifyJsonOrFallback(acceptedCase.ExpectedJson),
                    ErrorMessage = caseResult.ErrorMessage
                };
            }

            var wrongAnswerPrefix = $"__OJ_CASE_WA__:{caseResult.TestCaseId:N}:";
            if (caseResult.Status == JudgeStatus.WrongAnswer
                && caseResult.ActualOutput is not null
                && caseResult.ActualOutput.StartsWith(wrongAnswerPrefix, StringComparison.Ordinal))
            {
                return new JudgeCaseResult
                {
                    TestCaseId = caseResult.TestCaseId,
                    Status = caseResult.Status,
                    TimeUsedMs = caseResult.TimeUsedMs,
                    MemoryUsedKb = caseResult.MemoryUsedKb,
                    ActualOutput = caseResult.ActualOutput[wrongAnswerPrefix.Length..],
                    ErrorMessage = "Function return value does not match expected value."
                };
            }

            return caseResult;
        }).ToList();

        return new JudgeResult
        {
            Status = judgeResult.Status,
            TimeUsedMs = judgeResult.TimeUsedMs,
            MemoryUsedKb = judgeResult.MemoryUsedKb,
            ErrorMessage = judgeResult.Status == JudgeStatus.WrongAnswer
                ? "Function return value does not match expected value."
                : judgeResult.ErrorMessage,
            FailureKind = judgeResult.FailureKind,
            CaseResults = processedCases
        };
    }

    private static string? MinifyJsonOrFallback(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(value);
            return System.Text.Json.JsonSerializer.Serialize(document.RootElement);
        }
        catch
        {
            return value;
        }
    }

    private static bool ContainsMainFunction(string sourceCode)
    {
        return MainFunctionRegex().IsMatch(sourceCode);
    }

    [GeneratedRegex(@"\b(?:int|auto|void)\s+main\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MainFunctionRegex();
}
