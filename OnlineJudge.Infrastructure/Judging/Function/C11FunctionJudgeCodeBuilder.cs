using System.Globalization;
using System.Text;
using System.Text.Json;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Infrastructure.Judging.Function;

public class C11FunctionJudgeCodeBuilder
{
    private static readonly HashSet<string> SupportedTypes =
    [
        "int",
        "long",
        "double",
        "bool",
        "int[]",
        "long[]",
        "double[]"
    ];

    public Result<JudgeRequest> Build(JudgeRequest request)
    {
        var specResult = FunctionJudgeSpecParser.Parse(request.FunctionSpecJson);
        if (specResult.IsFailure || specResult.Value is null)
        {
            return Result<JudgeRequest>.Failure(specResult.ErrorMessage ?? "Invalid function spec.");
        }

        var spec = specResult.Value;
        var c11TypeValidation = ValidateC11SupportedTypes(spec);
        if (c11TypeValidation.IsFailure)
        {
            return Result<JudgeRequest>.Failure(c11TypeValidation.ErrorMessage!);
        }

        var convertedCases = new List<JudgeCaseRequest>();
        var caseBlocks = new List<string>();

        for (var index = 0; index < request.TestCases.Count; index++)
        {
            var testCase = request.TestCases[index];
            var validation = FunctionJudgeSpecParser.ValidateTestCase(spec, testCase.ArgumentsJson, testCase.ExpectedJson);
            if (validation.IsFailure)
            {
                return Result<JudgeRequest>.Failure(validation.ErrorMessage ?? "Invalid function test case.");
            }

            try
            {
                caseBlocks.Add(BuildCaseBlock(spec, testCase, index));
            }
            catch (NotSupportedException ex)
            {
                return Result<JudgeRequest>.Failure(ex.Message);
            }
            catch (JsonException)
            {
                return Result<JudgeRequest>.Failure("Function test case JSON is invalid.");
            }
            catch (InvalidOperationException)
            {
                return Result<JudgeRequest>.Failure("Function test case JSON does not match function spec.");
            }

            convertedCases.Add(new JudgeCaseRequest
            {
                TestCaseId = testCase.TestCaseId,
                Input = $"{index}{Environment.NewLine}",
                ExpectedOutput = GetAcceptedMarker(testCase.TestCaseId),
                ArgumentsJson = testCase.ArgumentsJson,
                ExpectedJson = testCase.ExpectedJson
            });
        }

        return Result<JudgeRequest>.Success(new JudgeRequest
        {
            SubmissionId = request.SubmissionId,
            ProblemId = request.ProblemId,
            Language = request.Language,
            JudgeMode = request.JudgeMode,
            SourceCode = BuildSource(request.SourceCode, caseBlocks),
            FunctionSpecJson = request.FunctionSpecJson,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitMb = request.MemoryLimitMb,
            TestCases = convertedCases
        });
    }

    private static Result ValidateC11SupportedTypes(FunctionJudgeSpec spec)
    {
        var returnValidation = ValidateC11SupportedType(spec.ReturnType);
        if (returnValidation.IsFailure)
        {
            return returnValidation;
        }

        foreach (var parameter in spec.Parameters)
        {
            var parameterValidation = ValidateC11SupportedType(parameter.Type);
            if (parameterValidation.IsFailure)
            {
                return parameterValidation;
            }
        }

        return Result.Success();
    }

    private static Result ValidateC11SupportedType(string type)
    {
        return SupportedTypes.Contains(type)
            ? Result.Success()
            : Result.Failure($"C11 function mode does not support type: {type}");
    }

    private static string BuildSource(string userSource, IReadOnlyList<string> caseBlocks)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#include <stdio.h>");
        builder.AppendLine("#include <stdlib.h>");
        builder.AppendLine("#include <stdbool.h>");
        builder.AppendLine("#include <math.h>");
        builder.AppendLine();
        builder.AppendLine(userSource);
        builder.AppendLine();
        AppendHelpers(builder);
        builder.AppendLine("int main(void) {");
        builder.AppendLine("    int __oj_case_index = 0;");
        builder.AppendLine("    if (scanf(\"%d\", &__oj_case_index) != 1) return 2;");
        builder.AppendLine("    switch (__oj_case_index) {");
        foreach (var caseBlock in caseBlocks)
        {
            builder.Append(caseBlock);
        }

        builder.AppendLine("        default:");
        builder.AppendLine("            return 2;");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendHelpers(StringBuilder builder)
    {
        builder.AppendLine("static int __oj_compare_int(int actual, int expected) { return actual == expected; }");
        builder.AppendLine("static int __oj_compare_long(long actual, long expected) { return actual == expected; }");
        builder.AppendLine("static int __oj_compare_double(double actual, double expected) { return fabs(actual - expected) <= 1e-6; }");
        builder.AppendLine("static int __oj_compare_bool(bool actual, bool expected) { return actual == expected; }");
        builder.AppendLine("static int __oj_compare_int_array(int* actual, int actualSize, int* expected, int expectedSize) {");
        builder.AppendLine("    if (actualSize != expectedSize) return 0;");
        builder.AppendLine("    if (actualSize == 0) return 1;");
        builder.AppendLine("    if (actual == NULL || expected == NULL) return 0;");
        builder.AppendLine("    for (int i = 0; i < expectedSize; i++) if (actual[i] != expected[i]) return 0;");
        builder.AppendLine("    return 1;");
        builder.AppendLine("}");
        builder.AppendLine("static int __oj_compare_long_array(long* actual, int actualSize, long* expected, int expectedSize) {");
        builder.AppendLine("    if (actualSize != expectedSize) return 0;");
        builder.AppendLine("    if (actualSize == 0) return 1;");
        builder.AppendLine("    if (actual == NULL || expected == NULL) return 0;");
        builder.AppendLine("    for (int i = 0; i < expectedSize; i++) if (actual[i] != expected[i]) return 0;");
        builder.AppendLine("    return 1;");
        builder.AppendLine("}");
        builder.AppendLine("static int __oj_compare_double_array(double* actual, int actualSize, double* expected, int expectedSize) {");
        builder.AppendLine("    if (actualSize != expectedSize) return 0;");
        builder.AppendLine("    if (actualSize == 0) return 1;");
        builder.AppendLine("    if (actual == NULL || expected == NULL) return 0;");
        builder.AppendLine("    for (int i = 0; i < expectedSize; i++) if (fabs(actual[i] - expected[i]) > 1e-6) return 0;");
        builder.AppendLine("    return 1;");
        builder.AppendLine("}");
        builder.AppendLine("static void __oj_print_int_json(int value) { printf(\"%d\", value); }");
        builder.AppendLine("static void __oj_print_long_json(long value) { printf(\"%ld\", value); }");
        builder.AppendLine("static void __oj_print_double_json(double value) { printf(\"%.15g\", value); }");
        builder.AppendLine("static void __oj_print_bool_json(bool value) { printf(value ? \"true\" : \"false\"); }");
        builder.AppendLine("static void __oj_print_int_array_json(int* values, int size) {");
        builder.AppendLine("    if (values == NULL) { printf(\"null\"); return; }");
        builder.AppendLine("    printf(\"[\");");
        builder.AppendLine("    for (int i = 0; i < size; i++) { if (i > 0) printf(\",\"); printf(\"%d\", values[i]); }");
        builder.AppendLine("    printf(\"]\");");
        builder.AppendLine("}");
        builder.AppendLine("static void __oj_print_long_array_json(long* values, int size) {");
        builder.AppendLine("    if (values == NULL) { printf(\"null\"); return; }");
        builder.AppendLine("    printf(\"[\");");
        builder.AppendLine("    for (int i = 0; i < size; i++) { if (i > 0) printf(\",\"); printf(\"%ld\", values[i]); }");
        builder.AppendLine("    printf(\"]\");");
        builder.AppendLine("}");
        builder.AppendLine("static void __oj_print_double_array_json(double* values, int size) {");
        builder.AppendLine("    if (values == NULL) { printf(\"null\"); return; }");
        builder.AppendLine("    printf(\"[\");");
        builder.AppendLine("    for (int i = 0; i < size; i++) { if (i > 0) printf(\",\"); printf(\"%.15g\", values[i]); }");
        builder.AppendLine("    printf(\"]\");");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static string BuildCaseBlock(FunctionJudgeSpec spec, JudgeCaseRequest testCase, int caseIndex)
    {
        using var argumentsDocument = JsonDocument.Parse(testCase.ArgumentsJson!);
        using var expectedDocument = JsonDocument.Parse(testCase.ExpectedJson!);

        var builder = new StringBuilder();
        builder.AppendLine($"        case {caseIndex}: {{");

        for (var parameterIndex = 0; parameterIndex < spec.Parameters.Count; parameterIndex++)
        {
            var parameter = spec.Parameters[parameterIndex];
            AppendVariableDeclaration(builder, $"__oj_arg_{parameterIndex}", parameter.Type, argumentsDocument.RootElement.GetProperty(parameter.Name));
        }

        if (IsArrayType(spec.ReturnType))
        {
            AppendArrayReturnCase(builder, spec, testCase, expectedDocument.RootElement);
        }
        else
        {
            AppendScalarReturnCase(builder, spec, testCase, expectedDocument.RootElement);
        }

        builder.AppendLine("            return 0;");
        builder.AppendLine("        }");
        return builder.ToString();
    }

    private static void AppendScalarReturnCase(StringBuilder builder, FunctionJudgeSpec spec, JudgeCaseRequest testCase, JsonElement expectedElement)
    {
        var expectedLiteral = ToCLiteral(expectedElement, spec.ReturnType);
        builder.AppendLine($"            {ToCType(spec.ReturnType)} __oj_expected = {expectedLiteral};");
        builder.AppendLine($"            {ToCType(spec.ReturnType)} __oj_actual = {spec.FunctionName}({BuildArgumentList(spec)});");
        builder.AppendLine($"            if ({CompareExpression(spec.ReturnType, "__oj_actual", "__oj_expected")}) {{");
        builder.AppendLine($"                printf(\"{GetAcceptedMarker(testCase.TestCaseId)}\");");
        builder.AppendLine("            } else {");
        builder.AppendLine($"                printf(\"__OJ_CASE_WA__:{testCase.TestCaseId:N}:\");");
        builder.AppendLine($"                {PrintJsonStatement(spec.ReturnType, "__oj_actual")}");
        builder.AppendLine("            }");
    }

    private static void AppendArrayReturnCase(StringBuilder builder, FunctionJudgeSpec spec, JudgeCaseRequest testCase, JsonElement expectedElement)
    {
        AppendVariableDeclaration(builder, "__oj_expected", spec.ReturnType, expectedElement);
        builder.AppendLine("            int __oj_return_size = 0;");
        builder.AppendLine($"            {ToCArrayPointerType(spec.ReturnType)} __oj_actual = {spec.FunctionName}({BuildArgumentList(spec, includeReturnSize: true)});");
        builder.AppendLine($"            if ({CompareArrayExpression(spec.ReturnType, "__oj_actual", "__oj_return_size", "__oj_expected", "__oj_expectedSize")}) {{");
        builder.AppendLine($"                printf(\"{GetAcceptedMarker(testCase.TestCaseId)}\");");
        builder.AppendLine("            } else {");
        builder.AppendLine($"                printf(\"__OJ_CASE_WA__:{testCase.TestCaseId:N}:\");");
        builder.AppendLine($"                {PrintJsonStatement(spec.ReturnType, "__oj_actual", "__oj_return_size")}");
        builder.AppendLine("            }");
        builder.AppendLine("            free(__oj_actual);");
    }

    private static void AppendVariableDeclaration(StringBuilder builder, string variableName, string type, JsonElement element)
    {
        if (!IsArrayType(type))
        {
            builder.AppendLine($"            {ToCType(type)} {variableName} = {ToCLiteral(element, type)};");
            return;
        }

        var values = element.EnumerateArray().Select(item => ToCLiteral(item, GetArrayElementType(type))).ToList();
        if (values.Count == 0)
        {
            builder.AppendLine($"            {ToCArrayPointerType(type)} {variableName} = NULL;");
            builder.AppendLine($"            int {variableName}Size = 0;");
            return;
        }

        builder.AppendLine($"            {ToCType(GetArrayElementType(type))} {variableName}[] = {{ {string.Join(", ", values)} }};");
        builder.AppendLine($"            int {variableName}Size = {values.Count.ToString(CultureInfo.InvariantCulture)};");
    }

    private static string BuildArgumentList(FunctionJudgeSpec spec, bool includeReturnSize = false)
    {
        var arguments = new List<string>();
        for (var parameterIndex = 0; parameterIndex < spec.Parameters.Count; parameterIndex++)
        {
            var parameter = spec.Parameters[parameterIndex];
            var variableName = $"__oj_arg_{parameterIndex}";
            arguments.Add(variableName);
            if (IsArrayType(parameter.Type))
            {
                arguments.Add($"{variableName}Size");
            }
        }

        if (includeReturnSize)
        {
            arguments.Add("&__oj_return_size");
        }

        return string.Join(", ", arguments);
    }

    private static string CompareExpression(string type, string actual, string expected)
    {
        return type switch
        {
            "int" => $"__oj_compare_int({actual}, {expected})",
            "long" => $"__oj_compare_long({actual}, {expected})",
            "double" => $"__oj_compare_double({actual}, {expected})",
            "bool" => $"__oj_compare_bool({actual}, {expected})",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static string CompareArrayExpression(string type, string actual, string actualSize, string expected, string expectedSize)
    {
        return type switch
        {
            "int[]" => $"__oj_compare_int_array({actual}, {actualSize}, {expected}, {expectedSize})",
            "long[]" => $"__oj_compare_long_array({actual}, {actualSize}, {expected}, {expectedSize})",
            "double[]" => $"__oj_compare_double_array({actual}, {actualSize}, {expected}, {expectedSize})",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static string PrintJsonStatement(string type, string variableName, string? sizeVariableName = null)
    {
        return type switch
        {
            "int" => $"__oj_print_int_json({variableName});",
            "long" => $"__oj_print_long_json({variableName});",
            "double" => $"__oj_print_double_json({variableName});",
            "bool" => $"__oj_print_bool_json({variableName});",
            "int[]" => $"__oj_print_int_array_json({variableName}, {sizeVariableName});",
            "long[]" => $"__oj_print_long_array_json({variableName}, {sizeVariableName});",
            "double[]" => $"__oj_print_double_array_json({variableName}, {sizeVariableName});",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static string ToCType(string type)
    {
        return type switch
        {
            "int" => "int",
            "long" => "long",
            "double" => "double",
            "bool" => "bool",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static string ToCArrayPointerType(string type)
    {
        return type switch
        {
            "int[]" => "int*",
            "long[]" => "long*",
            "double[]" => "double*",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static string ToCLiteral(JsonElement element, string type)
    {
        return type switch
        {
            "int" => element.GetInt32().ToString(CultureInfo.InvariantCulture),
            "long" => $"{element.GetInt64().ToString(CultureInfo.InvariantCulture)}L",
            "double" => element.GetDouble().ToString("R", CultureInfo.InvariantCulture),
            "bool" => element.GetBoolean() ? "true" : "false",
            _ => throw new NotSupportedException($"C11 function mode does not support type: {type}")
        };
    }

    private static bool IsArrayType(string type)
    {
        return type.EndsWith("[]", StringComparison.Ordinal);
    }

    private static string GetArrayElementType(string type)
    {
        return type[..^2];
    }

    private static string GetAcceptedMarker(Guid testCaseId)
    {
        return $"__OJ_CASE_AC__:{testCaseId:N}";
    }
}
