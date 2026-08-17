using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Tests.Judging.Function;

public class CSharpFunctionJudgeCodeBuilderTests
{
    [Fact]
    public void Build_TwoSum_CallsPascalCaseFunctionName()
    {
        var result = new CSharpFunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.CSharp));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("var __oj_actual = __oj_solution.TwoSum(__oj_arg_0, __oj_arg_1);", source);
        Assert.DoesNotContain("__oj_solution.twoSum(", source);
        Assert.Contains("int[] __oj_arg_0 = new int[] { 2, 7, 11, 15 };", source);
    }

    [Fact]
    public void Build_IntMatrix_GeneratesNestedArrayLiteral()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "identity",
              "returnType": "int[][]",
              "parameters": [{ "name": "matrix", "type": "int[][]" }],
              "supportedLanguages": ["csharp"]
            }
            """,
            """{ "matrix": [[1, 2], [3, 4]] }""",
            "[[1, 2], [3, 4]]",
            JudgeLanguage.CSharp);

        var result = new CSharpFunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } }", result.Value!.SourceCode);
    }

    [Fact]
    public void Build_StringCase_EscapesCSharpStringLiteral()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "echo",
              "returnType": "string",
              "parameters": [{ "name": "s", "type": "string" }],
              "supportedLanguages": ["csharp"]
            }
            """,
            """
            { "s": "a\"b\\c\n\t" }
            """,
            """
            "a\"b\\c\n\t"
            """,
            JudgeLanguage.CSharp);

        var result = new CSharpFunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("\"a\\\"b\\\\c\\n\\t\"", result.Value!.SourceCode);
    }

    [Fact]
    public void Build_DoubleAndArrayOutput_UsesEpsilonAndJsonHelpers()
    {
        var result = new CSharpFunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.CSharp));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("Math.Abs(actual - expected) <= 1e-6", source);
        Assert.Contains("private static string __oj_to_json(int[] values)", source);
    }

    [Fact]
    public void Build_ReverseList_GeneratesListNodeHelpersAndPascalCaseCall()
    {
        const string sourceCode = """
            public class ListNode
            {
                public int val;
                public ListNode? next;

                public ListNode(int val = 0, ListNode? next = null)
                {
                    this.val = val;
                    this.next = next;
                }
            }

            public class Solution
            {
                public ListNode? ReverseList(ListNode? head) => head;
            }
            """;

        var result = new CSharpFunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateReverseListRequest(JudgeLanguage.CSharp, sourceCode));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("public class ListNode", source);
        Assert.Contains("private static ListNode? __oj_build_list(int[] values)", source);
        Assert.Contains("private static int[] __oj_list_to_array(ListNode? head)", source);
        Assert.Contains("int[] __oj_arg_0_values = new int[] { 1, 2, 3 };", source);
        Assert.Contains("ListNode? __oj_arg_0 = __oj_build_list(__oj_arg_0_values);", source);
        Assert.Contains("var __oj_actual = __oj_solution.ReverseList(__oj_arg_0);", source);
        Assert.DoesNotContain("__oj_solution.reverseList(", source);
        Assert.Contains("var __oj_actual_values = __oj_list_to_array(__oj_actual);", source);
        Assert.Contains("int[] __oj_expected = new int[] { 3, 2, 1 };", source);
        Assert.Contains("__oj_to_json(__oj_actual_values)", source);
    }

    [Fact]
    public void Build_InvertTree_GeneratesTreeNodeHelpersAndPascalCaseCall()
    {
        const string sourceCode = """
            public class TreeNode
            {
                public int val;
                public TreeNode? left;
                public TreeNode? right;

                public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)
                {
                    this.val = val;
                    this.left = left;
                    this.right = right;
                }
            }

            public class Solution
            {
                public TreeNode? InvertTree(TreeNode? root) => root;
            }
            """;

        var result = new CSharpFunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateInvertTreeRequest(JudgeLanguage.CSharp, sourceCode));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Equal(1, CountOccurrences(source, "public class TreeNode"));
        Assert.Contains("private static TreeNode? __oj_build_tree(int?[] values)", source);
        Assert.Contains("private static int?[] __oj_tree_to_array(TreeNode? root)", source);
        Assert.Contains("int?[] __oj_arg_0_values = new int?[] { 4, 2, 7, 1, 3, 6, 9 };", source);
        Assert.Contains("TreeNode? __oj_arg_0 = __oj_build_tree(__oj_arg_0_values);", source);
        Assert.Contains("var __oj_actual = __oj_solution.InvertTree(__oj_arg_0);", source);
        Assert.DoesNotContain("__oj_solution.invertTree(", source);
        Assert.Contains("var __oj_actual_values = __oj_tree_to_array(__oj_actual);", source);
        Assert.Contains("int?[] __oj_expected = new int?[] { 4, 7, 2, 9, 6, 3, 1 };", source);
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
