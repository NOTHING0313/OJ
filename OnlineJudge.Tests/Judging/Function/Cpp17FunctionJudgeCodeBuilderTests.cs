using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Tests.Judging.Function;

public class Cpp17FunctionJudgeCodeBuilderTests
{
    [Fact]
    public void Build_TwoSum_GeneratesVectorArgumentsAndExactFunctionCall()
    {
        var result = new Cpp17FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateTwoSumRequest());

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("vector<int> __oj_arg_0 = vector<int>{2, 7, 11, 15};", source);
        Assert.Contains("auto __oj_actual = __oj_solution.twoSum(__oj_arg_0, __oj_arg_1);", source);
        Assert.Contains("bool __oj_equal_value(const vector<T>& left, const vector<T>& right)", source);
        Assert.Contains("__OJ_CASE_AC__:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", result.Value.TestCases[0].ExpectedOutput);
    }

    [Fact]
    public void Build_StringCase_EscapesCppStringLiteral()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "echo",
              "returnType": "string",
              "parameters": [{ "name": "s", "type": "string" }],
              "supportedLanguages": ["cpp17"]
            }
            """,
            """
            { "s": "a\"b\\c\n\t" }
            """,
            """
            "a\"b\\c\n\t"
            """,
            JudgeLanguage.Cpp17);

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("""string("a\"b\\c\n\t")""", result.Value!.SourceCode);
    }

    [Fact]
    public void Build_DoubleCase_UsesEpsilonComparison()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "half",
              "returnType": "double",
              "parameters": [{ "name": "x", "type": "double" }],
              "supportedLanguages": ["cpp17"]
            }
            """,
            """{ "x": 3.0 }""",
            "1.5",
            JudgeLanguage.Cpp17);

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("fabs(left - right) <= 1e-6", result.Value!.SourceCode);
    }

    [Fact]
    public void Build_UnsupportedType_ReturnsFailure()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "solve",
              "returnType": "object",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """,
            "{}",
            "{}",
            JudgeLanguage.Cpp17);

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Build_ReverseList_GeneratesListNodeHelpersAndArrayComparison()
    {
        const string sourceCode = """
            struct ListNode {
                int val;
                ListNode* next;
                ListNode() : val(0), next(nullptr) {}
                ListNode(int x) : val(x), next(nullptr) {}
                ListNode(int x, ListNode* next) : val(x), next(next) {}
            };

            class Solution {
            public:
                ListNode* reverseList(ListNode* head) { return head; }
            };
            """;

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateReverseListRequest(JudgeLanguage.Cpp17, sourceCode));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("struct ListNode", source);
        Assert.Contains("ListNode* __oj_build_list(const vector<int>& values)", source);
        Assert.Contains("vector<int> __oj_list_to_vector(ListNode* head)", source);
        Assert.Contains("vector<int> __oj_arg_0_values = vector<int>{1, 2, 3};", source);
        Assert.Contains("ListNode* __oj_arg_0 = __oj_build_list(__oj_arg_0_values);", source);
        Assert.Contains("auto __oj_actual = __oj_solution.reverseList(__oj_arg_0);", source);
        Assert.Contains("vector<int> __oj_actual_values = __oj_list_to_vector(__oj_actual);", source);
        Assert.Contains("vector<int> __oj_expected = vector<int>{3, 2, 1};", source);
        Assert.Contains("__oj_to_json(__oj_actual_values)", source);
    }

    [Fact]
    public void Build_InvertTree_GeneratesTreeNodeHelpersAndLevelOrderComparison()
    {
        const string sourceCode = """
            struct TreeNode {
                int val;
                TreeNode* left;
                TreeNode* right;
                TreeNode() : val(0), left(nullptr), right(nullptr) {}
                TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}
                TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}
            };

            class Solution {
            public:
                TreeNode* invertTree(TreeNode* root) { return root; }
            };
            """;

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateInvertTreeRequest(JudgeLanguage.Cpp17, sourceCode));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Equal(1, CountOccurrences(source, "struct TreeNode"));
        Assert.Contains("TreeNode* __oj_build_tree(const vector<optional<int>>& values)", source);
        Assert.Contains("vector<optional<int>> __oj_tree_to_vector(TreeNode* root)", source);
        Assert.Contains("values.push_back(nullopt);", source);
        Assert.Contains("vector<optional<int>> __oj_arg_0_values = vector<optional<int>>{4, 2, 7, 1, 3, 6, 9};", source);
        Assert.Contains("TreeNode* __oj_arg_0 = __oj_build_tree(__oj_arg_0_values);", source);
        Assert.Contains("auto __oj_actual = __oj_solution.invertTree(__oj_arg_0);", source);
        Assert.Contains("vector<optional<int>> __oj_actual_values = __oj_tree_to_vector(__oj_actual);", source);
        Assert.Contains("vector<optional<int>> __oj_expected = vector<optional<int>>{4, 7, 2, 9, 6, 3, 1};", source);
        Assert.Contains("__oj_to_json(__oj_actual_values)", source);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
