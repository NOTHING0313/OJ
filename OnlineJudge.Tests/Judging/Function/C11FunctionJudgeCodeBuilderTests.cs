using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Tests.Judging.Function;

public class C11FunctionJudgeCodeBuilderTests
{
    [Fact]
    public void Build_TwoSum_GeneratesCArrayArgumentsAndReturnSizeCall()
    {
        const string sourceCode = "int* twoSum(int* nums, int numsSize, int target, int* returnSize) { return NULL; }";
        var result = new C11FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.C11, sourceCode));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains(sourceCode, source);
        Assert.Contains("int __oj_arg_0[] = { 2, 7, 11, 15 };", source);
        Assert.Contains("int __oj_arg_0Size = 4;", source);
        Assert.Contains("int __oj_return_size = 0;", source);
        Assert.Contains("int* __oj_actual = twoSum(__oj_arg_0, __oj_arg_0Size, __oj_arg_1, &__oj_return_size);", source);
        Assert.Contains("free(__oj_actual);", source);
    }

    [Fact]
    public void Build_EmptyArray_GeneratesNullPointerAndZeroSize()
    {
        const string sourceCode = "int* twoSum(int* nums, int numsSize, int target, int* returnSize) { *returnSize = 0; return NULL; }";
        var request = FunctionJudgeTestData.CreateRequest(
            FunctionJudgeTestData.TwoSumSpecJson,
            """{ "nums": [], "target": 0 }""",
            "[]",
            JudgeLanguage.C11,
            sourceCode);

        var result = new C11FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("int* __oj_arg_0 = NULL;", source);
        Assert.Contains("int __oj_arg_0Size = 0;", source);
        Assert.Contains("int* __oj_expected = NULL;", source);
        Assert.Contains("int __oj_expectedSize = 0;", source);
        Assert.DoesNotContain("[] = {  };", source);
    }

    [Fact]
    public void Build_DoubleCase_UsesFabsComparison()
    {
        var request = FunctionJudgeTestData.CreateRequest(
            """
            {
              "functionName": "half",
              "returnType": "double",
              "parameters": [{ "name": "x", "type": "double" }],
              "supportedLanguages": ["c11"]
            }
            """,
            """{ "x": 3.0 }""",
            "1.5",
            JudgeLanguage.C11,
            "double half(double x) { return x / 2.0; }");

        var result = new C11FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("fabs(actual - expected) <= 1e-6", result.Value!.SourceCode);
    }

    [Fact]
    public void Build_ArrayReturn_GeneratesNullActualProtection()
    {
        var result = new C11FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.C11, "int* twoSum(int* nums, int numsSize, int target, int* returnSize) { return NULL; }"));

        Assert.True(result.IsSuccess);
        var source = result.Value!.SourceCode;
        Assert.Contains("if (actualSize == 0) return 1;", source);
        Assert.Contains("if (actual == NULL || expected == NULL) return 0;", source);
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int[][]")]
    public void Build_UnsupportedC11Type_ReturnsFriendlyFailure(string unsupportedType)
    {
        var request = FunctionJudgeTestData.CreateRequest(
            $$"""
            {
              "functionName": "solve",
              "returnType": "{{unsupportedType}}",
              "parameters": [],
              "supportedLanguages": ["c11"]
            }
            """,
            "{}",
            unsupportedType == "string" ? "\"x\"" : "[]",
            JudgeLanguage.C11,
            "int solve(void) { return 0; }");

        var result = new C11FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsFailure);
        Assert.Equal($"C11 function mode does not support type: {unsupportedType}", result.ErrorMessage);
    }

    [Fact]
    public void Build_ListNodeInt_ReturnsFriendlyFailureBeforeDocker()
    {
        var result = new C11FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateReverseListRequest(JudgeLanguage.C11, "int reverseList(int head) { return head; }"));

        Assert.True(result.IsFailure);
        Assert.Equal("C11 function mode does not support type: ListNode<int>", result.ErrorMessage);
    }

    [Fact]
    public void Build_TreeNodeInt_ReturnsFriendlyFailureBeforeDocker()
    {
        var result = new C11FunctionJudgeCodeBuilder().Build(FunctionJudgeTestData.CreateInvertTreeRequest(JudgeLanguage.C11, "int invertTree(int root) { return root; }"));

        Assert.True(result.IsFailure);
        Assert.Equal("C11 function mode does not support type: TreeNode<int>", result.ErrorMessage);
    }
}
