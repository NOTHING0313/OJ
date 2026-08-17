using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Tests.Judging.Function;

public class FunctionJudgeSpecParserTests
{
    [Fact]
    public void Parse_AllowsSupportedFunctionLanguages()
    {
        var result = FunctionJudgeSpecParser.Parse(FunctionJudgeTestData.TwoSumSpecJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("twoSum", result.Value.FunctionName);
        Assert.Equal("int[]", result.Value.ReturnType);
        Assert.Equal(2, result.Value.Parameters.Count);
    }

    [Fact]
    public void Parse_FailsWhenFunctionNameIsEmpty()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "functionName": "",
              "returnType": "int",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Equal("Function name is required.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_FailsWhenReturnTypeIsUnsupported()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "functionName": "solve",
              "returnType": "object",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Parse_FailsWhenParameterNameIsDuplicated()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "functionName": "solve",
              "returnType": "int",
              "parameters": [
                { "name": "value", "type": "int" },
                { "name": "value", "type": "int" }
              ],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Equal("Duplicate parameter name: value", result.ErrorMessage);
    }

    [Fact]
    public void Parse_FailsWhenParameterTypeIsUnsupported()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "functionName": "solve",
              "returnType": "int",
              "parameters": [
                { "name": "value", "type": "object" }
              ],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Parse_AllowsListNodeInt()
    {
        var result = FunctionJudgeSpecParser.Parse(FunctionJudgeTestData.ReverseListSpecJson);

        Assert.True(result.IsSuccess);
        Assert.Equal("ListNode<int>", result.Value!.ReturnType);
        Assert.Equal("ListNode<int>", result.Value.Parameters[0].Type);
    }

    [Fact]
    public void Parse_AllowsTreeNodeInt()
    {
        var result = FunctionJudgeSpecParser.Parse(FunctionJudgeTestData.InvertTreeSpecJson);

        Assert.True(result.IsSuccess);
        Assert.Equal("TreeNode<int>", result.Value!.ReturnType);
        Assert.Equal("TreeNode<int>", result.Value.Parameters[0].Type);
    }

    [Theory]
    [InlineData("ListNode<string>")]
    [InlineData("ListNode<long>")]
    public void Parse_FailsWhenListNodeTypeIsUnsupported(string type)
    {
        var result = FunctionJudgeSpecParser.Parse($$"""
            {
              "functionName": "solve",
              "returnType": "{{type}}",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Equal($"Function mode does not support type: {type}", result.ErrorMessage);
    }

    [Theory]
    [InlineData("TreeNode<string>")]
    [InlineData("TreeNode<long>")]
    public void Parse_FailsWhenTreeNodeTypeIsUnsupported(string type)
    {
        var result = FunctionJudgeSpecParser.Parse($$"""
            {
              "functionName": "solve",
              "returnType": "{{type}}",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Equal($"Function mode does not support type: {type}", result.ErrorMessage);
    }

    [Theory]
    [InlineData("""{ "head": [1, 2, 3] }""", "[3, 2, 1]", true)]
    [InlineData("""{ "head": [] }""", "[]", true)]
    [InlineData("""{ "head": null }""", "[]", false)]
    [InlineData("""{ "head": [1, "x"] }""", "[]", false)]
    public void ValidateTestCase_ValidatesListNodeIntJson(string argumentsJson, string expectedJson, bool expectedSuccess)
    {
        var spec = FunctionJudgeSpecParser.Parse(FunctionJudgeTestData.ReverseListSpecJson).Value!;

        var result = FunctionJudgeSpecParser.ValidateTestCase(spec, argumentsJson, expectedJson);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (!expectedSuccess)
        {
            Assert.Equal("ListNode<int> expects an integer array JSON value.", result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("""{ "root": [1, 2, 3, null, 4] }""", "[1, 3, 2, null, 4]", true)]
    [InlineData("""{ "root": [] }""", "[]", true)]
    [InlineData("""{ "root": null }""", "[]", false)]
    [InlineData("""{ "root": [1, "x"] }""", "[]", false)]
    [InlineData("""{ "root": [1, 1.5] }""", "[]", false)]
    public void ValidateTestCase_ValidatesTreeNodeIntJson(string argumentsJson, string expectedJson, bool expectedSuccess)
    {
        var spec = FunctionJudgeSpecParser.Parse(FunctionJudgeTestData.InvertTreeSpecJson).Value!;

        var result = FunctionJudgeSpecParser.ValidateTestCase(spec, argumentsJson, expectedJson);

        Assert.Equal(expectedSuccess, result.IsSuccess);
        if (!expectedSuccess)
        {
            Assert.Equal("TreeNode<int> expects a level-order integer array JSON value.", result.ErrorMessage);
        }
    }
}
