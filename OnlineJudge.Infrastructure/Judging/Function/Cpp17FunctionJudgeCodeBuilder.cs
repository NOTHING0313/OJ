using System.Globalization;
using System.Text;
using System.Text.Json;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;

namespace OnlineJudge.Infrastructure.Judging.Function;

public class Cpp17FunctionJudgeCodeBuilder : IFunctionJudgeCodeBuilder
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

            convertedCases.Add(new JudgeCaseRequest
            {
                TestCaseId = testCase.TestCaseId,
                Input = $"{index}{Environment.NewLine}",
                ExpectedOutput = GetAcceptedMarker(testCase.TestCaseId),
                ArgumentsJson = testCase.ArgumentsJson,
                ExpectedJson = testCase.ExpectedJson
            });
        }

        var generatedSource = BuildSource(request.SourceCode, caseBlocks, spec);
        return Result<JudgeRequest>.Success(new JudgeRequest
        {
            SubmissionId = request.SubmissionId,
            ProblemId = request.ProblemId,
            Language = request.Language,
            JudgeMode = request.JudgeMode,
            SourceCode = generatedSource,
            FunctionSpecJson = request.FunctionSpecJson,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitMb = request.MemoryLimitMb,
            TestCases = convertedCases
        });
    }

    private static string BuildSource(string userSource, IReadOnlyList<string> caseBlocks, FunctionJudgeSpec spec)
    {
        var builder = new StringBuilder();
        builder.AppendLine("#include <bits/stdc++.h>");
        builder.AppendLine("using namespace std;");
        builder.AppendLine();
        builder.AppendLine("template <typename T>");
        builder.AppendLine("bool __oj_equal_value(const T& left, const T& right) { return left == right; }");
        builder.AppendLine("bool __oj_equal_value(const double& left, const double& right) { return fabs(left - right) <= 1e-6; }");
        builder.AppendLine("bool __oj_equal_value(const optional<int>& left, const optional<int>& right) { return left == right; }");
        builder.AppendLine("template <typename T>");
        builder.AppendLine("bool __oj_equal_value(const vector<T>& left, const vector<T>& right) {");
        builder.AppendLine("    if (left.size() != right.size()) return false;");
        builder.AppendLine("    for (size_t i = 0; i < left.size(); ++i) {");
        builder.AppendLine("        if (!__oj_equal_value(left[i], right[i])) return false;");
        builder.AppendLine("    }");
        builder.AppendLine("    return true;");
        builder.AppendLine("}");
        builder.AppendLine("string __oj_escape_json_string(const string& value) {");
        builder.AppendLine("    string result;");
        builder.AppendLine("    for (char ch : value) {");
        builder.AppendLine("        switch (ch) {");
        builder.AppendLine("            case '\\\\': result += \"\\\\\\\\\"; break;");
        builder.AppendLine("            case '\"': result += \"\\\\\\\"\"; break;");
        builder.AppendLine("            case '\\n': result += \"\\\\n\"; break;");
        builder.AppendLine("            case '\\r': result += \"\\\\r\"; break;");
        builder.AppendLine("            case '\\t': result += \"\\\\t\"; break;");
        builder.AppendLine("            default: result += ch; break;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine("    return result;");
        builder.AppendLine("}");
        builder.AppendLine("string __oj_to_json(const string& value) { return string(\"\\\"\") + __oj_escape_json_string(value) + \"\\\"\"; }");
        builder.AppendLine("string __oj_to_json(const char* value) { return __oj_to_json(string(value)); }");
        builder.AppendLine("string __oj_to_json(bool value) { return value ? \"true\" : \"false\"; }");
        builder.AppendLine("string __oj_to_json(int value) { return to_string(value); }");
        builder.AppendLine("string __oj_to_json(long long value) { return to_string(value); }");
        builder.AppendLine("string __oj_to_json(double value) { ostringstream oss; oss << setprecision(15) << value; return oss.str(); }");
        builder.AppendLine("string __oj_to_json(const optional<int>& value) { return value.has_value() ? to_string(value.value()) : string(\"null\"); }");
        builder.AppendLine("template <typename T>");
        builder.AppendLine("string __oj_to_json(const vector<T>& values) {");
        builder.AppendLine("    string result = \"[\";");
        builder.AppendLine("    for (size_t i = 0; i < values.size(); ++i) {");
        builder.AppendLine("        if (i > 0) result += \",\";");
        builder.AppendLine("        result += __oj_to_json(values[i]);");
        builder.AppendLine("    }");
        builder.AppendLine("    result += \"]\";");
        builder.AppendLine("    return result;");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(userSource);
        builder.AppendLine();

        AppendCustomTypeHelpers(builder, spec);

        if (ContainsListNode(spec))
        {
            AppendListNodeHelpers(builder);
        }

        if (ContainsTreeNode(spec))
        {
            AppendTreeNodeHelpers(builder);
        }

        builder.AppendLine("int main() {");
        builder.AppendLine("    int __oj_case_index = -1;");
        builder.AppendLine("    if (!(cin >> __oj_case_index)) return 2;");
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

    private static void AppendCustomTypeHelpers(StringBuilder builder, FunctionJudgeSpec spec)
    {
        if (spec.Types.Count == 0)
        {
            return;
        }

        foreach (var type in spec.Types)
        {
            builder.AppendLine($"bool __oj_equal_value(const {type.Name}& left, const {type.Name}& right);");
            builder.AppendLine($"string __oj_to_json(const {type.Name}& value);");
        }

        builder.AppendLine();

        foreach (var type in spec.Types)
        {
            var comparisons = type.Fields
                .Select(field => $"__oj_equal_value(left.{field.Name}, right.{field.Name})")
                .ToList();

            builder.AppendLine($"bool __oj_equal_value(const {type.Name}& left, const {type.Name}& right) {{");
            builder.AppendLine($"    return {string.Join(" && ", comparisons)};");
            builder.AppendLine("}");

            builder.AppendLine($"string __oj_to_json(const {type.Name}& value) {{");
            builder.AppendLine("    string result = \"{\";");
            for (var index = 0; index < type.Fields.Count; index++)
            {
                var field = type.Fields[index];
                var prefix = index == 0 ? string.Empty : ",";
                builder.AppendLine($"    result += \"{prefix}\\\"{field.Name}\\\":\" + __oj_to_json(value.{field.Name});");
            }

            builder.AppendLine("    result += \"}\";");
            builder.AppendLine("    return result;");
            builder.AppendLine("}");
            builder.AppendLine();
        }
    }

    private static string BuildCaseBlock(FunctionJudgeSpec spec, JudgeCaseRequest testCase, int caseIndex)
    {
        using var argumentsDocument = JsonDocument.Parse(testCase.ArgumentsJson!);
        using var expectedDocument = JsonDocument.Parse(testCase.ExpectedJson!);

        var builder = new StringBuilder();
        builder.AppendLine($"        case {caseIndex}: {{");
        builder.AppendLine("            Solution __oj_solution;");

        for (var parameterIndex = 0; parameterIndex < spec.Parameters.Count; parameterIndex++)
        {
            var parameter = spec.Parameters[parameterIndex];
            AppendVariableDeclaration(builder, $"__oj_arg_{parameterIndex}", parameter.Type, argumentsDocument.RootElement.GetProperty(parameter.Name), spec);
        }

        AppendExpectedDeclaration(builder, spec.ReturnType, expectedDocument.RootElement, spec);
        builder.AppendLine($"            auto __oj_actual = __oj_solution.{spec.FunctionName}({BuildArgumentList(spec.Parameters.Count)});");
        if (spec.ReturnType == "ListNode<int>")
        {
            builder.AppendLine("            vector<int> __oj_actual_values = __oj_list_to_vector(__oj_actual);");
        }
        else if (spec.ReturnType == "TreeNode<int>")
        {
            builder.AppendLine("            vector<optional<int>> __oj_actual_values = __oj_tree_to_vector(__oj_actual);");
        }

        var actualForComparison = spec.ReturnType is "ListNode<int>" or "TreeNode<int>" ? "__oj_actual_values" : "__oj_actual";
        builder.AppendLine($"            if (__oj_equal_value({actualForComparison}, __oj_expected)) {{");
        builder.AppendLine($"                cout << \"{GetAcceptedMarker(testCase.TestCaseId)}\" << endl;");
        builder.AppendLine("            } else {");
        builder.AppendLine($"                cout << \"__OJ_CASE_WA__:{testCase.TestCaseId:N}:\" << __oj_to_json({actualForComparison}) << endl;");
        builder.AppendLine("            }");
        builder.AppendLine("            return 0;");
        builder.AppendLine("        }");
        return builder.ToString();
    }

    private static string BuildArgumentList(int parameterCount)
    {
        return string.Join(", ", Enumerable.Range(0, parameterCount).Select(index => $"__oj_arg_{index}"));
    }

    private static void AppendVariableDeclaration(StringBuilder builder, string variableName, string type, JsonElement element, FunctionJudgeSpec spec)
    {
        if (type == "ListNode<int>")
        {
            builder.AppendLine($"            vector<int> {variableName}_values = {ToListNodeValuesLiteral(element, spec)};");
            builder.AppendLine($"            ListNode* {variableName} = __oj_build_list({variableName}_values);");
            return;
        }

        if (type == "TreeNode<int>")
        {
            builder.AppendLine($"            vector<optional<int>> {variableName}_values = {ToTreeNodeValuesLiteral(element)};");
            builder.AppendLine($"            TreeNode* {variableName} = __oj_build_tree({variableName}_values);");
            return;
        }

        var literal = ToCppLiteral(element, type, spec);
        builder.AppendLine($"            {ToCppType(type, spec)} {variableName} = {literal};");
    }

    private static void AppendExpectedDeclaration(StringBuilder builder, string returnType, JsonElement element, FunctionJudgeSpec spec)
    {
        if (returnType == "ListNode<int>")
        {
            builder.AppendLine($"            vector<int> __oj_expected = {ToListNodeValuesLiteral(element, spec)};");
            return;
        }

        if (returnType == "TreeNode<int>")
        {
            builder.AppendLine($"            vector<optional<int>> __oj_expected = {ToTreeNodeValuesLiteral(element)};");
            return;
        }

        var expectedLiteral = ToCppLiteral(element, returnType, spec);
        builder.AppendLine($"            {ToCppType(returnType, spec)} __oj_expected = {expectedLiteral};");
    }

    private static void AppendListNodeHelpers(StringBuilder builder)
    {
        builder.AppendLine("ListNode* __oj_build_list(const vector<int>& values) {");
        builder.AppendLine("    ListNode dummy;");
        builder.AppendLine("    ListNode* tail = &dummy;");
        builder.AppendLine("    for (int value : values) {");
        builder.AppendLine("        tail->next = new ListNode(value);");
        builder.AppendLine("        tail = tail->next;");
        builder.AppendLine("    }");
        builder.AppendLine("    return dummy.next;");
        builder.AppendLine("}");
        builder.AppendLine("vector<int> __oj_list_to_vector(ListNode* head) {");
        builder.AppendLine("    vector<int> values;");
        builder.AppendLine("    while (head != nullptr) {");
        builder.AppendLine("        values.push_back(head->val);");
        builder.AppendLine("        head = head->next;");
        builder.AppendLine("    }");
        builder.AppendLine("    return values;");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendTreeNodeHelpers(StringBuilder builder)
    {
        builder.AppendLine("TreeNode* __oj_build_tree(const vector<optional<int>>& values) {");
        builder.AppendLine("    if (values.empty() || !values[0].has_value()) return nullptr;");
        builder.AppendLine("    TreeNode* root = new TreeNode(values[0].value());");
        builder.AppendLine("    queue<TreeNode*> nodes;");
        builder.AppendLine("    nodes.push(root);");
        builder.AppendLine("    size_t index = 1;");
        builder.AppendLine("    while (!nodes.empty() && index < values.size()) {");
        builder.AppendLine("        TreeNode* current = nodes.front();");
        builder.AppendLine("        nodes.pop();");
        builder.AppendLine("        if (index < values.size() && values[index].has_value()) {");
        builder.AppendLine("            current->left = new TreeNode(values[index].value());");
        builder.AppendLine("            nodes.push(current->left);");
        builder.AppendLine("        }");
        builder.AppendLine("        ++index;");
        builder.AppendLine("        if (index < values.size() && values[index].has_value()) {");
        builder.AppendLine("            current->right = new TreeNode(values[index].value());");
        builder.AppendLine("            nodes.push(current->right);");
        builder.AppendLine("        }");
        builder.AppendLine("        ++index;");
        builder.AppendLine("    }");
        builder.AppendLine("    return root;");
        builder.AppendLine("}");
        builder.AppendLine("vector<optional<int>> __oj_tree_to_vector(TreeNode* root) {");
        builder.AppendLine("    vector<optional<int>> values;");
        builder.AppendLine("    if (root == nullptr) return values;");
        builder.AppendLine("    queue<TreeNode*> nodes;");
        builder.AppendLine("    nodes.push(root);");
        builder.AppendLine("    while (!nodes.empty()) {");
        builder.AppendLine("        TreeNode* current = nodes.front();");
        builder.AppendLine("        nodes.pop();");
        builder.AppendLine("        if (current == nullptr) {");
        builder.AppendLine("            values.push_back(nullopt);");
        builder.AppendLine("            continue;");
        builder.AppendLine("        }");
        builder.AppendLine("        values.push_back(current->val);");
        builder.AppendLine("        nodes.push(current->left);");
        builder.AppendLine("        nodes.push(current->right);");
        builder.AppendLine("    }");
        builder.AppendLine("    while (!values.empty() && !values.back().has_value()) {");
        builder.AppendLine("        values.pop_back();");
        builder.AppendLine("    }");
        builder.AppendLine("    return values;");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static string ToCppType(string type, FunctionJudgeSpec spec)
    {
        if (spec.FindCustomType(type) is not null)
        {
            return type;
        }

        if (FunctionJudgeSpecParser.IsCustomArrayType(spec, type))
        {
            FunctionJudgeSpecParser.TryGetArrayElementType(type, out var elementType);
            return $"vector<{elementType}>";
        }

        return type switch
        {
            "int" => "int",
            "long" => "long long",
            "double" => "double",
            "bool" => "bool",
            "string" => "string",
            "int[]" => "vector<int>",
            "long[]" => "vector<long long>",
            "double[]" => "vector<double>",
            "bool[]" => "vector<bool>",
            "string[]" => "vector<string>",
            "int[][]" => "vector<vector<int>>",
            "ListNode<int>" => "ListNode*",
            "TreeNode<int>" => "TreeNode*",
            _ => throw new NotSupportedException($"Function mode does not support type: {type}")
        };
    }

    private static string ToCppLiteral(JsonElement element, string type, FunctionJudgeSpec spec)
    {
        var customType = spec.FindCustomType(type);
        if (customType is not null)
        {
            var values = customType.Fields.Select(field => ToCppLiteral(element.GetProperty(field.Name), field.Type, spec));
            return $"{customType.Name}{{{string.Join(", ", values)}}}";
        }

        if (FunctionJudgeSpecParser.IsCustomArrayType(spec, type))
        {
            FunctionJudgeSpecParser.TryGetArrayElementType(type, out var elementType);
            return ToVectorLiteral(element, elementType, spec);
        }

        return type switch
        {
            "int" => element.GetInt32().ToString(CultureInfo.InvariantCulture),
            "long" => $"{element.GetInt64().ToString(CultureInfo.InvariantCulture)}LL",
            "double" => element.GetDouble().ToString("R", CultureInfo.InvariantCulture),
            "bool" => element.GetBoolean() ? "true" : "false",
            "string" => $"string(\"{EscapeCppString(element.GetString() ?? string.Empty)}\")",
            "int[]" => ToVectorLiteral(element, "int", spec),
            "long[]" => ToVectorLiteral(element, "long", spec),
            "double[]" => ToVectorLiteral(element, "double", spec),
            "bool[]" => ToVectorLiteral(element, "bool", spec),
            "string[]" => ToVectorLiteral(element, "string", spec),
            "int[][]" => ToVectorLiteral(element, "int[]", spec),
            _ => throw new NotSupportedException($"Function mode does not support type: {type}")
        };
    }

    private static string ToListNodeValuesLiteral(JsonElement element, FunctionJudgeSpec spec)
    {
        return ToVectorLiteral(element, "int", spec);
    }

    private static string ToTreeNodeValuesLiteral(JsonElement element)
    {
        var values = element.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null
            ? "nullopt"
            : item.GetInt32().ToString(CultureInfo.InvariantCulture));
        return $"vector<optional<int>>{{{string.Join(", ", values)}}}";
    }

    private static string ToVectorLiteral(JsonElement element, string elementType, FunctionJudgeSpec spec)
    {
        var values = element.EnumerateArray().Select(item => ToCppLiteral(item, elementType, spec));
        return $"{ToCppType($"{elementType}[]", spec)}{{{string.Join(", ", values)}}}";
    }

    private static string EscapeCppString(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
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
