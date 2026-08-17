using System.Globalization;
using System.Text;
using System.Text.Json;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Infrastructure.Judging.Function;

public class CSharpFunctionJudgeCodeBuilder
{
    public Result<JudgeRequest> Build(JudgeRequest request)
    {
        var specResult = FunctionJudgeSpecParser.Parse(request.FunctionSpecJson);
        if (specResult.IsFailure || specResult.Value is null)
        {
            return Result<JudgeRequest>.Failure(specResult.ErrorMessage ?? "Invalid function spec.");
        }

        var spec = specResult.Value;
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
            SourceCode = BuildSource(request.SourceCode, caseBlocks, ContainsListNode(spec), ContainsTreeNode(spec)),
            FunctionSpecJson = request.FunctionSpecJson,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitMb = request.MemoryLimitMb,
            TestCases = convertedCases
        });
    }

    private static string BuildSource(string userSource, IReadOnlyList<string> caseBlocks, bool includeListNodeHelpers, bool includeTreeNodeHelpers)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using System.Globalization;");
        builder.AppendLine("using System.Linq;");
        builder.AppendLine();
        builder.AppendLine(userSource);
        builder.AppendLine();
        builder.AppendLine("public class Program");
        builder.AppendLine("{");
        builder.AppendLine("    public static void Main()");
        builder.AppendLine("    {");
        builder.AppendLine("        var __oj_line = Console.ReadLine();");
        builder.AppendLine("        var __oj_case_index = int.Parse(__oj_line ?? \"0\", CultureInfo.InvariantCulture);");
        builder.AppendLine("        switch (__oj_case_index)");
        builder.AppendLine("        {");
        foreach (var caseBlock in caseBlocks)
        {
            builder.Append(caseBlock);
        }

        builder.AppendLine("            default:");
        builder.AppendLine("                Environment.Exit(2);");
        builder.AppendLine("                return;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static bool __oj_compare(int actual, int expected) => actual == expected;");
        builder.AppendLine("    private static bool __oj_compare(long actual, long expected) => actual == expected;");
        builder.AppendLine("    private static bool __oj_compare(double actual, double expected) => Math.Abs(actual - expected) <= 1e-6;");
        builder.AppendLine("    private static bool __oj_compare(bool actual, bool expected) => actual == expected;");
        builder.AppendLine("    private static bool __oj_compare(string actual, string expected) => string.Equals(actual, expected, StringComparison.Ordinal);");
        builder.AppendLine("    private static bool __oj_compare(int[] actual, int[] expected) => actual is not null && expected is not null && actual.SequenceEqual(expected);");
        builder.AppendLine("    private static bool __oj_compare(long[] actual, long[] expected) => actual is not null && expected is not null && actual.SequenceEqual(expected);");
        builder.AppendLine("    private static bool __oj_compare(bool[] actual, bool[] expected) => actual is not null && expected is not null && actual.SequenceEqual(expected);");
        builder.AppendLine("    private static bool __oj_compare(string[] actual, string[] expected) => actual is not null && expected is not null && actual.SequenceEqual(expected);");
        builder.AppendLine("    private static bool __oj_compare(int?[] actual, int?[] expected) => actual is not null && expected is not null && actual.SequenceEqual(expected);");
        builder.AppendLine("    private static bool __oj_compare(double[] actual, double[] expected)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (actual is null || expected is null || actual.Length != expected.Length) return false;");
        builder.AppendLine("        for (var i = 0; i < actual.Length; i++)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (!__oj_compare(actual[i], expected[i])) return false;");
        builder.AppendLine("        }");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine("    private static bool __oj_compare(int[][] actual, int[][] expected)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (actual is null || expected is null || actual.Length != expected.Length) return false;");
        builder.AppendLine("        for (var i = 0; i < actual.Length; i++)");
        builder.AppendLine("        {");
        builder.AppendLine("            if (!__oj_compare(actual[i], expected[i])) return false;");
        builder.AppendLine("        }");
        builder.AppendLine("        return true;");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static string __oj_to_json(int value) => value.ToString(CultureInfo.InvariantCulture);");
        builder.AppendLine("    private static string __oj_to_json(long value) => value.ToString(CultureInfo.InvariantCulture);");
        builder.AppendLine("    private static string __oj_to_json(double value) => value.ToString(\"R\", CultureInfo.InvariantCulture);");
        builder.AppendLine("    private static string __oj_to_json(bool value) => value ? \"true\" : \"false\";");
        builder.AppendLine("    private static string __oj_to_json(string value) => value is null ? \"null\" : \"\\\"\" + __oj_escape_json_string(value) + \"\\\"\";");
        builder.AppendLine("    private static string __oj_to_json(int[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(long[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(double[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(bool[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(string[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(int[][] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(__oj_to_json)) + \"]\";");
        builder.AppendLine("    private static string __oj_to_json(int?[] values) => values is null ? \"null\" : \"[\" + string.Join(\",\", values.Select(value => value.HasValue ? __oj_to_json(value.Value) : \"null\")) + \"]\";");
        if (includeListNodeHelpers)
        {
            builder.AppendLine("    private static ListNode? __oj_build_list(int[] values)");
            builder.AppendLine("    {");
            builder.AppendLine("        var dummy = new ListNode();");
            builder.AppendLine("        var tail = dummy;");
            builder.AppendLine("        foreach (var value in values)");
            builder.AppendLine("        {");
            builder.AppendLine("            tail.next = new ListNode(value);");
            builder.AppendLine("            tail = tail.next!;");
            builder.AppendLine("        }");
            builder.AppendLine("        return dummy.next;");
            builder.AppendLine("    }");
            builder.AppendLine("    private static int[] __oj_list_to_array(ListNode? head)");
            builder.AppendLine("    {");
            builder.AppendLine("        var values = new List<int>();");
            builder.AppendLine("        while (head is not null)");
            builder.AppendLine("        {");
            builder.AppendLine("            values.Add(head.val);");
            builder.AppendLine("            head = head.next;");
            builder.AppendLine("        }");
            builder.AppendLine("        return values.ToArray();");
            builder.AppendLine("    }");
        }
        if (includeTreeNodeHelpers)
        {
            builder.AppendLine("    private static TreeNode? __oj_build_tree(int?[] values)");
            builder.AppendLine("    {");
            builder.AppendLine("        if (values.Length == 0 || !values[0].HasValue) return null;");
            builder.AppendLine("        var root = new TreeNode(values[0].Value);");
            builder.AppendLine("        var nodes = new Queue<TreeNode>();");
            builder.AppendLine("        nodes.Enqueue(root);");
            builder.AppendLine("        var index = 1;");
            builder.AppendLine("        while (nodes.Count > 0 && index < values.Length)");
            builder.AppendLine("        {");
            builder.AppendLine("            var current = nodes.Dequeue();");
            builder.AppendLine("            if (index < values.Length && values[index].HasValue)");
            builder.AppendLine("            {");
            builder.AppendLine("                current.left = new TreeNode(values[index].Value);");
            builder.AppendLine("                nodes.Enqueue(current.left);");
            builder.AppendLine("            }");
            builder.AppendLine("            index++;");
            builder.AppendLine("            if (index < values.Length && values[index].HasValue)");
            builder.AppendLine("            {");
            builder.AppendLine("                current.right = new TreeNode(values[index].Value);");
            builder.AppendLine("                nodes.Enqueue(current.right);");
            builder.AppendLine("            }");
            builder.AppendLine("            index++;");
            builder.AppendLine("        }");
            builder.AppendLine("        return root;");
            builder.AppendLine("    }");
            builder.AppendLine("    private static int?[] __oj_tree_to_array(TreeNode? root)");
            builder.AppendLine("    {");
            builder.AppendLine("        var values = new List<int?>();");
            builder.AppendLine("        if (root is null) return Array.Empty<int?>();");
            builder.AppendLine("        var nodes = new Queue<TreeNode?>();");
            builder.AppendLine("        nodes.Enqueue(root);");
            builder.AppendLine("        while (nodes.Count > 0)");
            builder.AppendLine("        {");
            builder.AppendLine("            var current = nodes.Dequeue();");
            builder.AppendLine("            if (current is null)");
            builder.AppendLine("            {");
            builder.AppendLine("                values.Add(null);");
            builder.AppendLine("                continue;");
            builder.AppendLine("            }");
            builder.AppendLine("            values.Add(current.val);");
            builder.AppendLine("            nodes.Enqueue(current.left);");
            builder.AppendLine("            nodes.Enqueue(current.right);");
            builder.AppendLine("        }");
            builder.AppendLine("        while (values.Count > 0 && !values[^1].HasValue)");
            builder.AppendLine("        {");
            builder.AppendLine("            values.RemoveAt(values.Count - 1);");
            builder.AppendLine("        }");
            builder.AppendLine("        return values.ToArray();");
            builder.AppendLine("    }");
        }
        builder.AppendLine("    private static string __oj_escape_json_string(string value)");
        builder.AppendLine("    {");
        builder.AppendLine("        return value");
        builder.AppendLine("            .Replace(\"\\\\\", \"\\\\\\\\\", StringComparison.Ordinal)");
        builder.AppendLine("            .Replace(\"\\\"\", \"\\\\\\\"\", StringComparison.Ordinal)");
        builder.AppendLine("            .Replace(\"\\r\", \"\\\\r\", StringComparison.Ordinal)");
        builder.AppendLine("            .Replace(\"\\n\", \"\\\\n\", StringComparison.Ordinal)");
        builder.AppendLine("            .Replace(\"\\t\", \"\\\\t\", StringComparison.Ordinal);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string BuildCaseBlock(FunctionJudgeSpec spec, JudgeCaseRequest testCase, int caseIndex)
    {
        using var argumentsDocument = JsonDocument.Parse(testCase.ArgumentsJson!);
        using var expectedDocument = JsonDocument.Parse(testCase.ExpectedJson!);

        var builder = new StringBuilder();
        builder.AppendLine($"            case {caseIndex}:");
        builder.AppendLine("            {");
        builder.AppendLine("                var __oj_solution = new Solution();");

        for (var parameterIndex = 0; parameterIndex < spec.Parameters.Count; parameterIndex++)
        {
            var parameter = spec.Parameters[parameterIndex];
            AppendVariableDeclaration(builder, $"__oj_arg_{parameterIndex}", parameter.Type, argumentsDocument.RootElement.GetProperty(parameter.Name));
        }

        AppendExpectedDeclaration(builder, spec.ReturnType, expectedDocument.RootElement);
        builder.AppendLine($"                var __oj_actual = __oj_solution.{ToCSharpMethodName(spec.FunctionName)}({BuildArgumentList(spec.Parameters.Count)});");
        if (spec.ReturnType == "ListNode<int>")
        {
            builder.AppendLine("                var __oj_actual_values = __oj_list_to_array(__oj_actual);");
        }
        else if (spec.ReturnType == "TreeNode<int>")
        {
            builder.AppendLine("                var __oj_actual_values = __oj_tree_to_array(__oj_actual);");
        }

        var actualForComparison = spec.ReturnType is "ListNode<int>" or "TreeNode<int>" ? "__oj_actual_values" : "__oj_actual";
        builder.AppendLine($"                if (__oj_compare({actualForComparison}, __oj_expected))");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Console.WriteLine(\"{GetAcceptedMarker(testCase.TestCaseId)}\");");
        builder.AppendLine("                }");
        builder.AppendLine("                else");
        builder.AppendLine("                {");
        builder.AppendLine($"                    Console.WriteLine(\"__OJ_CASE_WA__:{testCase.TestCaseId:N}:\" + __oj_to_json({actualForComparison}));");
        builder.AppendLine("                }");
        builder.AppendLine("                return;");
        builder.AppendLine("            }");
        return builder.ToString();
    }

    private static string BuildArgumentList(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(index => $"__oj_arg_{index}"));
    }

    private static void AppendVariableDeclaration(StringBuilder builder, string variableName, string type, JsonElement element)
    {
        if (type == "ListNode<int>")
        {
            builder.AppendLine($"                int[] {variableName}_values = {ToListNodeValuesLiteral(element)};");
            builder.AppendLine($"                ListNode? {variableName} = __oj_build_list({variableName}_values);");
            return;
        }

        if (type == "TreeNode<int>")
        {
            builder.AppendLine($"                int?[] {variableName}_values = {ToTreeNodeValuesLiteral(element)};");
            builder.AppendLine($"                TreeNode? {variableName} = __oj_build_tree({variableName}_values);");
            return;
        }

        var literal = ToCSharpLiteral(element, type);
        builder.AppendLine($"                {ToCSharpType(type)} {variableName} = {literal};");
    }

    private static void AppendExpectedDeclaration(StringBuilder builder, string returnType, JsonElement element)
    {
        if (returnType == "ListNode<int>")
        {
            builder.AppendLine($"                int[] __oj_expected = {ToListNodeValuesLiteral(element)};");
            return;
        }

        if (returnType == "TreeNode<int>")
        {
            builder.AppendLine($"                int?[] __oj_expected = {ToTreeNodeValuesLiteral(element)};");
            return;
        }

        var expectedLiteral = ToCSharpLiteral(element, returnType);
        builder.AppendLine($"                {ToCSharpType(returnType)} __oj_expected = {expectedLiteral};");
    }

    private static string ToCSharpMethodName(string functionName)
    {
        if (string.IsNullOrEmpty(functionName))
        {
            return functionName;
        }

        return $"{char.ToUpperInvariant(functionName[0])}{functionName[1..]}";
    }

    private static string ToCSharpType(string type)
    {
        return type switch
        {
            "int" => "int",
            "long" => "long",
            "double" => "double",
            "bool" => "bool",
            "string" => "string",
            "int[]" => "int[]",
            "long[]" => "long[]",
            "double[]" => "double[]",
            "bool[]" => "bool[]",
            "string[]" => "string[]",
            "int[][]" => "int[][]",
            "ListNode<int>" => "ListNode?",
            "TreeNode<int>" => "TreeNode?",
            _ => throw new NotSupportedException($"Function mode does not support type: {type}")
        };
    }

    private static string ToCSharpLiteral(JsonElement element, string type)
    {
        return type switch
        {
            "int" => element.GetInt32().ToString(CultureInfo.InvariantCulture),
            "long" => $"{element.GetInt64().ToString(CultureInfo.InvariantCulture)}L",
            "double" => element.GetDouble().ToString("R", CultureInfo.InvariantCulture),
            "bool" => element.GetBoolean() ? "true" : "false",
            "string" => $"\"{EscapeCSharpString(element.GetString() ?? string.Empty)}\"",
            "int[]" => ToArrayLiteral(element, "int"),
            "long[]" => ToArrayLiteral(element, "long"),
            "double[]" => ToArrayLiteral(element, "double"),
            "bool[]" => ToArrayLiteral(element, "bool"),
            "string[]" => ToArrayLiteral(element, "string"),
            "int[][]" => ToArrayLiteral(element, "int[]"),
            _ => throw new NotSupportedException($"Function mode does not support type: {type}")
        };
    }

    private static string ToListNodeValuesLiteral(JsonElement element)
    {
        return ToArrayLiteral(element, "int");
    }

    private static string ToTreeNodeValuesLiteral(JsonElement element)
    {
        var values = element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null
            ? "null"
            : item.GetInt32().ToString(CultureInfo.InvariantCulture));
        return $"new int?[] {{ {string.Join(", ", values)} }}";
    }

    private static string ToArrayLiteral(JsonElement element, string elementType)
    {
        var values = element.EnumerateArray().Select(item => ToCSharpLiteral(item, elementType));
        return $"new {ToCSharpType(elementType)}[] {{ {string.Join(", ", values)} }}";
    }

    private static string EscapeCSharpString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    private static string GetAcceptedMarker(Guid testCaseId)
    {
        return $"__OJ_CASE_AC__:{testCaseId:N}";
    }

    private static bool ContainsListNode(FunctionJudgeSpec spec)
    {
        return spec.ReturnType == "ListNode<int>"
            || spec.Parameters.Any(parameter => parameter.Type == "ListNode<int>");
    }

    private static bool ContainsTreeNode(FunctionJudgeSpec spec)
    {
        return spec.ReturnType == "TreeNode<int>"
            || spec.Parameters.Any(parameter => parameter.Type == "TreeNode<int>");
    }
}
