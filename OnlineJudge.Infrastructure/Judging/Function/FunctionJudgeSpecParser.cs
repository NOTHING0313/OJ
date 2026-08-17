using System.Text.Json;
using System.Text.RegularExpressions;
using OnlineJudge.Application.Common;

namespace OnlineJudge.Infrastructure.Judging.Function;

internal static partial class FunctionJudgeSpecParser
{
    private static readonly HashSet<string> SupportedTypes =
    [
        "int",
        "long",
        "double",
        "bool",
        "string",
        "int[]",
        "long[]",
        "double[]",
        "bool[]",
        "string[]",
        "int[][]",
        "ListNode<int>",
        "TreeNode<int>"
    ];

    public static Result<FunctionJudgeSpec> Parse(string? functionSpecJson)
    {
        if (string.IsNullOrWhiteSpace(functionSpecJson))
        {
            return Result<FunctionJudgeSpec>.Failure("Function spec is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(functionSpecJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<FunctionJudgeSpec>.Failure("Function spec must be a JSON object.");
            }

            if (!TryGetRequiredString(root, "functionName", out var functionName))
            {
                return Result<FunctionJudgeSpec>.Failure("Function name is required.");
            }

            if (!IsIdentifier(functionName))
            {
                return Result<FunctionJudgeSpec>.Failure("Function name is invalid.");
            }

            if (!TryGetRequiredString(root, "returnType", out var returnType))
            {
                return Result<FunctionJudgeSpec>.Failure("Return type is required.");
            }

            var typeValidation = ValidateSupportedType(returnType);
            if (typeValidation.IsFailure)
            {
                return Result<FunctionJudgeSpec>.Failure(typeValidation.ErrorMessage!);
            }

            if (!root.TryGetProperty("parameters", out var parametersElement) || parametersElement.ValueKind != JsonValueKind.Array)
            {
                return Result<FunctionJudgeSpec>.Failure("Parameters must be an array.");
            }

            var parameters = new List<FunctionParameterSpec>();
            var parameterNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameterElement in parametersElement.EnumerateArray())
            {
                if (parameterElement.ValueKind != JsonValueKind.Object)
                {
                    return Result<FunctionJudgeSpec>.Failure("Each parameter must be a JSON object.");
                }

                if (!TryGetRequiredString(parameterElement, "name", out var parameterName))
                {
                    return Result<FunctionJudgeSpec>.Failure("Parameter name is required.");
                }

                if (!IsIdentifier(parameterName))
                {
                    return Result<FunctionJudgeSpec>.Failure($"Parameter name is invalid: {parameterName}");
                }

                if (!parameterNames.Add(parameterName))
                {
                    return Result<FunctionJudgeSpec>.Failure($"Duplicate parameter name: {parameterName}");
                }

                if (!TryGetRequiredString(parameterElement, "type", out var parameterType))
                {
                    return Result<FunctionJudgeSpec>.Failure($"Parameter type is required: {parameterName}");
                }

                typeValidation = ValidateSupportedType(parameterType);
                if (typeValidation.IsFailure)
                {
                    return Result<FunctionJudgeSpec>.Failure(typeValidation.ErrorMessage!);
                }

                parameters.Add(new FunctionParameterSpec(parameterName, parameterType));
            }

            if (root.TryGetProperty("supportedLanguages", out var supportedLanguages) && supportedLanguages.ValueKind == JsonValueKind.Array)
            {
                var supportsKnownLanguage = supportedLanguages
                    .EnumerateArray()
                    .Any(language => language.ValueKind == JsonValueKind.String
                        && (string.Equals(language.GetString(), "cpp17", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(language.GetString(), "c11", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(language.GetString(), "csharp", StringComparison.OrdinalIgnoreCase)));

                if (!supportsKnownLanguage)
                {
                    return Result<FunctionJudgeSpec>.Failure("Function mode currently supports C++17, C# and C11 only.");
                }
            }

            return Result<FunctionJudgeSpec>.Success(new FunctionJudgeSpec(functionName, returnType, parameters));
        }
        catch (JsonException)
        {
            return Result<FunctionJudgeSpec>.Failure("Function spec must be valid JSON.");
        }
    }

    public static Result ValidateStarterCode(string? starterCodeJson)
    {
        if (string.IsNullOrWhiteSpace(starterCodeJson))
        {
            return Result.Failure("Starter code is required for function mode.");
        }

        try
        {
            using var document = JsonDocument.Parse(starterCodeJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure("Starter code must be a JSON object.");
            }

            if (!TryGetRequiredString(root, "cpp17", out _))
            {
                return Result.Failure("C++17 starter code is required.");
            }

            return Result.Success();
        }
        catch (JsonException)
        {
            return Result.Failure("Starter code must be valid JSON.");
        }
    }

    public static Result ValidateTestCase(FunctionJudgeSpec spec, string? argumentsJson, string? expectedJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return Result.Failure("ArgumentsJson is required for function test case.");
        }

        if (string.IsNullOrWhiteSpace(expectedJson))
        {
            return Result.Failure("ExpectedJson is required for function test case.");
        }

        try
        {
            using var argumentsDocument = JsonDocument.Parse(argumentsJson);
            var argumentsRoot = argumentsDocument.RootElement;
            if (argumentsRoot.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure("ArgumentsJson must be a JSON object.");
            }

            var expectedParameterNames = spec.Parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
            var actualParameterNames = argumentsRoot.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (!actualParameterNames.SetEquals(expectedParameterNames))
            {
                return Result.Failure("ArgumentsJson must exactly match function parameters.");
            }

            foreach (var parameter in spec.Parameters)
            {
                if (!argumentsRoot.TryGetProperty(parameter.Name, out var argumentElement))
                {
                    return Result.Failure($"Missing argument: {parameter.Name}");
                }

                var argumentValidation = ValidateJsonValue(argumentElement, parameter.Type, parameter.Name);
                if (argumentValidation.IsFailure)
                {
                    return argumentValidation;
                }
            }

            using var expectedDocument = JsonDocument.Parse(expectedJson);
            return ValidateJsonValue(expectedDocument.RootElement, spec.ReturnType, "expected");
        }
        catch (JsonException)
        {
            return Result.Failure("Function test case JSON is invalid.");
        }
    }

    public static Result ValidateSupportedType(string type)
    {
        return SupportedTypes.Contains(type)
            ? Result.Success()
            : Result.Failure($"Function mode does not support type: {type}");
    }

    private static Result ValidateJsonValue(JsonElement element, string type, string name)
    {
        return type switch
        {
            "int" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _)
                ? Result.Success()
                : Result.Failure($"{name} must be int."),
            "long" => element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out _)
                ? Result.Success()
                : Result.Failure($"{name} must be long."),
            "double" => element.ValueKind == JsonValueKind.Number
                ? Result.Success()
                : Result.Failure($"{name} must be double."),
            "bool" => element.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? Result.Success()
                : Result.Failure($"{name} must be bool."),
            "string" => element.ValueKind == JsonValueKind.String
                ? Result.Success()
                : Result.Failure($"{name} must be string."),
            "int[]" => ValidateArray(element, "int", name),
            "long[]" => ValidateArray(element, "long", name),
            "double[]" => ValidateArray(element, "double", name),
            "bool[]" => ValidateArray(element, "bool", name),
            "string[]" => ValidateArray(element, "string", name),
            "int[][]" => ValidateArray(element, "int[]", name),
            "ListNode<int>" => ValidateListNode(element),
            "TreeNode<int>" => ValidateTreeNode(element),
            _ => ValidateSupportedType(type)
        };
    }

    private static Result ValidateListNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure("ListNode<int> expects an integer array JSON value.");
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out _))
            {
                return Result.Failure("ListNode<int> expects an integer array JSON value.");
            }
        }

        return Result.Success();
    }

    private static Result ValidateTreeNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure("TreeNode<int> expects a level-order integer array JSON value.");
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out _))
            {
                return Result.Failure("TreeNode<int> expects a level-order integer array JSON value.");
            }
        }

        return Result.Success();
    }

    private static Result ValidateArray(JsonElement element, string elementType, string name)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure($"{name} must be an array.");
        }

        foreach (var item in element.EnumerateArray())
        {
            var itemValidation = ValidateJsonValue(item, elementType, name);
            if (itemValidation.IsFailure)
            {
                return itemValidation;
            }
        }

        return Result.Success();
    }

    private static bool TryGetRequiredString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsIdentifier(string value)
    {
        return IdentifierRegex().IsMatch(value);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
