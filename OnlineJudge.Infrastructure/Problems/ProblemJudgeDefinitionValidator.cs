using System.Text.Json;
using OnlineJudge.Application.Common;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Infrastructure.Problems;

internal static class ProblemJudgeDefinitionValidator
{
    private const int AllAllowedLanguagesMask = 0b111;

    public static Result ValidateProblem(JudgeMode judgeMode, int allowedLanguagesMask, string? functionSpecJson, string? starterCodeJson)
    {
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
        int score)
    {
        if (score < 0) return Result.Failure("Score cannot be negative.");
        if (!Enum.IsDefined(visibility)) return Result.Failure("Unsupported test case visibility.");

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
