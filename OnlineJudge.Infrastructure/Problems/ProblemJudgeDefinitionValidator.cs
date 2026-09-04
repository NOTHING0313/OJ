using System.Text.Json;
using System.Text;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Infrastructure.Problems;

internal static class ProblemJudgeDefinitionValidator
{
    private const int AllAllowedLanguagesMask = 0b111;

    public static Result ValidateProblem(
        string title,
        string description,
        string inputDescription,
        string outputDescription,
        int timeLimitMs,
        int memoryLimitMb,
        JudgeMode judgeMode,
        int allowedLanguagesMask,
        string? functionSpecJson,
        string? starterCodeJson,
        JudgeResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (string.IsNullOrWhiteSpace(title) || title.Length > policy.MaxProblemTitleCharacters)
        {
            return Result.Failure($"Problem title must contain between 1 and {policy.MaxProblemTitleCharacters} characters.");
        }

        foreach (var (name, value) in new[]
        {
            ("description", description),
            ("inputDescription", inputDescription),
            ("outputDescription", outputDescription),
            ("functionSpecJson", functionSpecJson ?? string.Empty),
            ("starterCodeJson", starterCodeJson ?? string.Empty)
        })
        {
            if (Utf8ByteCount(value) > policy.MaxProblemContentBytes)
            {
                return Result.Failure($"Problem {name} exceeds the {policy.MaxProblemContentBytes}-byte UTF-8 limit.");
            }
        }

        var resourceValidation = ValidateRunLimits(timeLimitMs, memoryLimitMb, policy);
        if (resourceValidation.IsFailure)
        {
            return resourceValidation;
        }

        if (!Enum.IsDefined(judgeMode))
        {
            return Result.Failure("Unsupported judge mode.");
        }

        if (allowedLanguagesMask < 0 || (allowedLanguagesMask & ~AllAllowedLanguagesMask) != 0)
        {
            return Result.Failure("Unsupported allowed languages mask.");
        }

        if (judgeMode == JudgeMode.StandardInputOutput)
        {
            return Result.Success();
        }

        var specResult = FunctionJudgeSpecParser.Parse(functionSpecJson);
        if (specResult.IsFailure)
        {
            return Result.Failure(specResult.ErrorMessage!);
        }

        var languageValidation = ValidateFunctionAllowedLanguages(allowedLanguagesMask, functionSpecJson);
        if (languageValidation.IsFailure)
        {
            return languageValidation;
        }

        return FunctionJudgeSpecParser.ValidateStarterCode(starterCodeJson);
    }

    public static Result ValidateTestCase(
        Problem problem,
        string input,
        string expectedOutput,
        string? argumentsJson,
        string? expectedJson,
        TestCaseVisibility visibility,
        int score,
        JudgeResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (score < 0) return Result.Failure("Score cannot be negative.");
        if (!Enum.IsDefined(visibility)) return Result.Failure("Unsupported test case visibility.");

        var payloadValidation = ValidateTestCasePayload(new JudgeTestCasePayload(input, expectedOutput, argumentsJson, expectedJson), policy);
        if (payloadValidation.IsFailure)
        {
            return payloadValidation;
        }

        if (problem.JudgeMode == JudgeMode.StandardInputOutput)
        {
            if (!string.IsNullOrWhiteSpace(argumentsJson) || !string.IsNullOrWhiteSpace(expectedJson))
            {
                return Result.Failure("Standard input/output test cases cannot use function JSON fields.");
            }

            return Result.Success();
        }

        if (!string.IsNullOrWhiteSpace(input) || !string.IsNullOrWhiteSpace(expectedOutput))
        {
            return Result.Failure("Function test cases cannot use standard input/output fields.");
        }

        var specResult = FunctionJudgeSpecParser.Parse(problem.FunctionSpecJson);
        if (specResult.IsFailure || specResult.Value is null)
        {
            return Result.Failure(specResult.ErrorMessage ?? "Invalid function spec.");
        }

        return FunctionJudgeSpecParser.ValidateTestCase(specResult.Value, argumentsJson, expectedJson);
    }

    public static Result ValidateTestCaseCollection(int timeLimitMs, IReadOnlyCollection<JudgeTestCasePayload> testCases, JudgeResourcePolicy policy, bool requireAtLeastOne)
    {
        ArgumentNullException.ThrowIfNull(testCases);
        ArgumentNullException.ThrowIfNull(policy);

        if (requireAtLeastOne && testCases.Count == 0)
        {
            return Result.Failure(ProblemJudgeRevisionPublisher.NoActiveTestCasesMessage);
        }

        if (testCases.Count > policy.MaxTestCases)
        {
            return Result.Failure($"A problem cannot contain more than {policy.MaxTestCases} active test cases.");
        }

        if ((long)timeLimitMs * testCases.Count > policy.MaxDeclaredTestTimeBudgetMs)
        {
            return Result.Failure($"Declared test-time budget exceeds {policy.MaxDeclaredTestTimeBudgetMs} ms.");
        }

        var totalBytes = testCases.Sum(GetPayloadSizeBytes);
        return totalBytes > policy.MaxAggregateTestDataBytes
            ? Result.Failure($"Aggregate test data exceeds the {policy.MaxAggregateTestDataBytes}-byte UTF-8 limit.")
            : Result.Success();
    }

    public static Result ValidateSourceCode(string sourceCode, JudgeResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Utf8ByteCount(sourceCode) > policy.MaxSourceCodeBytes
            ? Result.Failure($"Source code exceeds the {policy.MaxSourceCodeBytes}-byte UTF-8 limit.")
            : Result.Success();
    }

    public static Result ValidateRunLimits(int timeLimitMs, int memoryLimitMb, JudgeResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (timeLimitMs < policy.MinTimeLimitMs || timeLimitMs > policy.MaxTimeLimitMs)
        {
            return Result.Failure($"Time limit must be between {policy.MinTimeLimitMs} and {policy.MaxTimeLimitMs} ms.");
        }

        return memoryLimitMb < policy.MinMemoryLimitMb || memoryLimitMb > policy.MaxMemoryLimitMb
            ? Result.Failure($"Memory limit must be between {policy.MinMemoryLimitMb} and {policy.MaxMemoryLimitMb} MB.")
            : Result.Success();
    }

    public static Result ValidateTestCasePayload(JudgeTestCasePayload payload, JudgeResourcePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        foreach (var (name, value) in new[]
        {
            ("input", payload.Input),
            ("expectedOutput", payload.ExpectedOutput),
            ("argumentsJson", payload.ArgumentsJson ?? string.Empty),
            ("expectedJson", payload.ExpectedJson ?? string.Empty)
        })
        {
            if (Utf8ByteCount(value) > policy.MaxTestCaseFieldBytes)
            {
                return Result.Failure($"Test case {name} exceeds the {policy.MaxTestCaseFieldBytes}-byte UTF-8 limit.");
            }
        }

        return Result.Success();
    }

    public static long GetPayloadSizeBytes(JudgeTestCasePayload testCase)
    {
        return (long)Utf8ByteCount(testCase.Input)
            + Utf8ByteCount(testCase.ExpectedOutput)
            + Utf8ByteCount(testCase.ArgumentsJson)
            + Utf8ByteCount(testCase.ExpectedJson);
    }

    private static int Utf8ByteCount(string? value) => Encoding.UTF8.GetByteCount(value ?? string.Empty);

    private static Result ValidateFunctionAllowedLanguages(int allowedLanguagesMask, string? functionSpecJson)
    {
        if (allowedLanguagesMask == 0 || string.IsNullOrWhiteSpace(functionSpecJson))
        {
            return Result.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(functionSpecJson);
            if (!document.RootElement.TryGetProperty("supportedLanguages", out var supportedLanguages)
                || supportedLanguages.ValueKind != JsonValueKind.Array)
            {
                return Result.Success();
            }

            var supportedMask = 0;
            foreach (var item in supportedLanguages.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                supportedMask |= item.GetString()?.ToLowerInvariant() switch
                {
                    "cpp17" => 0b001,
                    "c11" => 0b010,
                    "csharp" => 0b100,
                    _ => 0
                };
            }

            return (allowedLanguagesMask & ~supportedMask) == 0
                ? Result.Success()
                : Result.Failure("Allowed languages include a language not supported by the function spec.");
        }
        catch (JsonException)
        {
            return Result.Success();
        }
    }
}

internal readonly record struct JudgeTestCasePayload(string Input, string ExpectedOutput, string? ArgumentsJson, string? ExpectedJson)
{
    public static JudgeTestCasePayload From(TestCase testCase) =>
        new(testCase.Input, testCase.ExpectedOutput, testCase.ArgumentsJson, testCase.ExpectedJson);
}
