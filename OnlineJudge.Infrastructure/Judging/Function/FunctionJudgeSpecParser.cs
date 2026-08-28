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

    private static readonly HashSet<string> CustomFieldPrimitiveTypes =
    [
        "int",
        "long",
        "double",
        "bool",
        "string"
    ];

    private static readonly HashSet<string> ReservedIdentifiers = new(StringComparer.Ordinal)
    {
        "int", "long", "double", "bool", "string", "void", "class", "struct", "record", "namespace",
        "public", "private", "protected", "internal", "static", "const", "readonly", "ref", "out", "in",
        "new", "return", "if", "else", "for", "while", "switch", "case", "default", "true", "false", "null",
        "auto", "typename", "template", "using", "typedef", "sizeof", "Solution", "Program", "ListNode", "TreeNode"
    };

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

            if (!IsSafeIdentifier(functionName))
            {
                return Result<FunctionJudgeSpec>.Failure("Function name is invalid.");
            }

            var customTypesResult = ParseCustomTypes(root);
            if (customTypesResult.IsFailure || customTypesResult.Value is null)
            {
                return Result<FunctionJudgeSpec>.Failure(customTypesResult.ErrorMessage ?? "Invalid custom types.");
            }

            var customTypes = customTypesResult.Value;
            var customTypeMap = customTypes.ToDictionary(type => type.Name, StringComparer.Ordinal);

            if (!TryGetRequiredString(root, "returnType", out var returnType))
            {
                return Result<FunctionJudgeSpec>.Failure("Return type is required.");
            }

            var typeValidation = ValidateFunctionType(returnType, customTypeMap);
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

                if (!IsSafeIdentifier(parameterName))
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

                typeValidation = ValidateFunctionType(parameterType, customTypeMap);
                if (typeValidation.IsFailure)
                {
                    return Result<FunctionJudgeSpec>.Failure(typeValidation.ErrorMessage!);
                }

                parameters.Add(new FunctionParameterSpec(parameterName, parameterType));
            }

            var customTypeValidation = ValidateCustomTypes(customTypes, customTypeMap);
            if (customTypeValidation.IsFailure)
            {
                return Result<FunctionJudgeSpec>.Failure(customTypeValidation.ErrorMessage!);
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

            return Result<FunctionJudgeSpec>.Success(new FunctionJudgeSpec(functionName, returnType, parameters)
            {
                Types = customTypes
            });
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

                var argumentValidation = ValidateJsonValue(argumentElement, parameter.Type, parameter.Name, spec);
                if (argumentValidation.IsFailure)
                {
                    return argumentValidation;
                }
            }

            using var expectedDocument = JsonDocument.Parse(expectedJson);
            return ValidateJsonValue(expectedDocument.RootElement, spec.ReturnType, "expected", spec);
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

    internal static bool IsCustomType(FunctionJudgeSpec spec, string type)
    {
        return spec.FindCustomType(type) is not null;
    }

    internal static bool IsCustomArrayType(FunctionJudgeSpec spec, string type)
    {
        return TryGetArrayElementType(type, out var elementType) && IsCustomType(spec, elementType);
    }

    internal static bool TryGetArrayElementType(string type, out string elementType)
    {
        if (type.EndsWith("[]", StringComparison.Ordinal))
        {
            elementType = type[..^2];
            return true;
        }

        elementType = string.Empty;
        return false;
    }

    private static Result<List<FunctionCustomTypeSpec>> ParseCustomTypes(JsonElement root)
    {
        if (!root.TryGetProperty("types", out var typesElement))
        {
            return Result<List<FunctionCustomTypeSpec>>.Success([]);
        }

        if (typesElement.ValueKind != JsonValueKind.Array)
        {
            return Result<List<FunctionCustomTypeSpec>>.Failure("Types must be an array.");
        }

        var types = new List<FunctionCustomTypeSpec>();
        var typeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var typeElement in typesElement.EnumerateArray())
        {
            if (typeElement.ValueKind != JsonValueKind.Object)
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure("Each custom type must be a JSON object.");
            }

            if (!TryGetRequiredString(typeElement, "name", out var typeName))
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure("Custom type name is required.");
            }

            if (!IsSafeIdentifier(typeName) || ReservedIdentifiers.Contains(typeName) || typeName.StartsWith("__oj_", StringComparison.Ordinal))
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure($"Custom type name is invalid: {typeName}");
            }

            if (!typeNames.Add(typeName))
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure($"Duplicate custom type name: {typeName}");
            }

            if (!typeElement.TryGetProperty("fields", out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Array)
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure($"Fields must be an array: {typeName}");
            }

            var fields = new List<FunctionCustomTypeFieldSpec>();
            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fieldElement in fieldsElement.EnumerateArray())
            {
                if (fieldElement.ValueKind != JsonValueKind.Object)
                {
                    return Result<List<FunctionCustomTypeSpec>>.Failure($"Each field must be a JSON object: {typeName}");
                }

                if (!TryGetRequiredString(fieldElement, "name", out var fieldName))
                {
                    return Result<List<FunctionCustomTypeSpec>>.Failure($"Field name is required: {typeName}");
                }

                if (!IsSafeIdentifier(fieldName) || ReservedIdentifiers.Contains(fieldName) || fieldName.StartsWith("__oj_", StringComparison.Ordinal))
                {
                    return Result<List<FunctionCustomTypeSpec>>.Failure($"Field name is invalid: {typeName}.{fieldName}");
                }

                if (!fieldNames.Add(fieldName))
                {
                    return Result<List<FunctionCustomTypeSpec>>.Failure($"Duplicate field name: {typeName}.{fieldName}");
                }

                if (!TryGetRequiredString(fieldElement, "type", out var fieldType))
                {
                    return Result<List<FunctionCustomTypeSpec>>.Failure($"Field type is required: {typeName}.{fieldName}");
                }

                fields.Add(new FunctionCustomTypeFieldSpec(fieldName, fieldType));
            }

            if (fields.Count == 0)
            {
                return Result<List<FunctionCustomTypeSpec>>.Failure($"Custom type must contain at least one field: {typeName}");
            }

            types.Add(new FunctionCustomTypeSpec(typeName, fields));
        }

        return Result<List<FunctionCustomTypeSpec>>.Success(types);
    }

    private static Result ValidateCustomTypes(
        IReadOnlyList<FunctionCustomTypeSpec> customTypes,
        IReadOnlyDictionary<string, FunctionCustomTypeSpec> customTypeMap)
    {
        foreach (var customType in customTypes)
        {
            foreach (var field in customType.Fields)
            {
                if (CustomFieldPrimitiveTypes.Contains(field.Type) || customTypeMap.ContainsKey(field.Type))
                {
                    continue;
                }

                if (field.Type.EndsWith("[]", StringComparison.Ordinal))
                {
                    return Result.Failure($"Custom type field arrays are not supported yet: {customType.Name}.{field.Name}");
                }

                return Result.Failure($"Function mode does not support custom field type: {customType.Name}.{field.Name} -> {field.Type}");
            }
        }

        return ValidateNoCustomTypeCycles(customTypes, customTypeMap);
    }

    private static Result ValidateNoCustomTypeCycles(
        IReadOnlyList<FunctionCustomTypeSpec> customTypes,
        IReadOnlyDictionary<string, FunctionCustomTypeSpec> customTypeMap)
    {
        var states = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var customType in customTypes)
        {
            var validation = Visit(customType.Name, states, customTypeMap);
            if (validation.IsFailure)
            {
                return validation;
            }
        }

        return Result.Success();

        static Result Visit(
            string typeName,
            IDictionary<string, int> states,
            IReadOnlyDictionary<string, FunctionCustomTypeSpec> customTypeMap)
        {
            if (states.TryGetValue(typeName, out var state))
            {
                return state == 1
                    ? Result.Failure($"Custom type dependency cycle detected at: {typeName}")
                    : Result.Success();
            }

            states[typeName] = 1;
            foreach (var field in customTypeMap[typeName].Fields)
            {
                if (!customTypeMap.ContainsKey(field.Type))
                {
                    continue;
                }

                var validation = Visit(field.Type, states, customTypeMap);
                if (validation.IsFailure)
                {
                    return validation;
                }
            }

            states[typeName] = 2;
            return Result.Success();
        }
    }

    private static Result ValidateFunctionType(
        string type,
        IReadOnlyDictionary<string, FunctionCustomTypeSpec> customTypeMap)
    {
        if (SupportedTypes.Contains(type) || customTypeMap.ContainsKey(type))
        {
            return Result.Success();
        }

        if (TryGetArrayElementType(type, out var elementType) && customTypeMap.ContainsKey(elementType))
        {
            return Result.Success();
        }

        return Result.Failure($"Function mode does not support type: {type}");
    }

    private static Result ValidateJsonValue(JsonElement element, string type, string name, FunctionJudgeSpec spec)
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
            "int[]" => ValidateArray(element, "int", name, spec),
            "long[]" => ValidateArray(element, "long", name, spec),
            "double[]" => ValidateArray(element, "double", name, spec),
            "bool[]" => ValidateArray(element, "bool", name, spec),
            "string[]" => ValidateArray(element, "string", name, spec),
            "int[][]" => ValidateArray(element, "int[]", name, spec),
            "ListNode<int>" => ValidateListNode(element),
            "TreeNode<int>" => ValidateTreeNode(element),
            _ => ValidateCustomJsonValue(element, type, name, spec)
        };
    }

    private static Result ValidateCustomJsonValue(JsonElement element, string type, string name, FunctionJudgeSpec spec)
    {
        if (IsCustomArrayType(spec, type))
        {
            TryGetArrayElementType(type, out var elementType);
            return ValidateArray(element, elementType, name, spec);
        }

        var customType = spec.FindCustomType(type);
        if (customType is null)
        {
            return Result.Failure($"Function mode does not support type: {type}");
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return Result.Failure($"{name} must be an object of type {type}.");
        }

        var expectedFieldNames = customType.Fields.Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var actualFieldNames = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!actualFieldNames.SetEquals(expectedFieldNames))
        {
            return Result.Failure($"{name} must exactly match fields of {type}.");
        }

        foreach (var field in customType.Fields)
        {
            var fieldValidation = ValidateJsonValue(element.GetProperty(field.Name), field.Type, $"{name}.{field.Name}", spec);
            if (fieldValidation.IsFailure)
            {
                return fieldValidation;
            }
        }

        return Result.Success();
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

    private static Result ValidateArray(JsonElement element, string elementType, string name, FunctionJudgeSpec spec)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Result.Failure($"{name} must be an array.");
        }

        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var itemValidation = ValidateJsonValue(item, elementType, $"{name}[{index}]", spec);
            if (itemValidation.IsFailure)
            {
                return itemValidation;
            }

            index++;
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

    private static bool IsSafeIdentifier(string value)
    {
        return IdentifierRegex().IsMatch(value);
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierRegex();
}
