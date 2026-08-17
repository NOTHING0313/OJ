using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;
using System.Text.RegularExpressions;

namespace OnlineJudge.Infrastructure.Judging.Runners;

public partial class CSharpJudgeRunner(IJudgeSandbox judgeSandbox, CSharpFunctionJudgeCodeBuilder functionJudgeCodeBuilder) : IJudgeRunner
{
    private static readonly LanguageJudgeProfile Profile = new()
    {
        Language = JudgeLanguage.CSharp,
        DisplayName = "C#",
        SourceFileName = "Program.cs",
        CompileCommand = "dotnet build Main.csproj -c Release -o out --nologo --verbosity quiet",
        RunCommand = "dotnet out/Main.dll",
        DockerImageName = "onlinejudge-csharp-sandbox:latest",
        ExtraFiles = new Dictionary<string, string>
        {
            ["Main.csproj"] = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net9.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """
        }
    };

    public bool Supports(JudgeLanguage language)
    {
        return language == JudgeLanguage.CSharp;
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
        if (ContainsProgramOrMain(request.SourceCode))
        {
            return new JudgeResult
            {
                Status = JudgeStatus.CompileError,
                ErrorMessage = "函数式题目不需要编写 Main，请只提交 Solution 类中的函数实现。"
            };
        }

        var buildResult = functionJudgeCodeBuilder.Build(request);
        if (buildResult.IsFailure || buildResult.Value is null)
        {
            return new JudgeResult
            {
                Status = JudgeStatus.SystemError,
                ErrorMessage = buildResult.ErrorMessage ?? "Function judge request is invalid."
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

    private static bool ContainsProgramOrMain(string sourceCode)
    {
        return ProgramOrMainRegex().IsMatch(sourceCode);
    }

    [GeneratedRegex(@"\bclass\s+Program\b|\bMain\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ProgramOrMainRegex();
}
