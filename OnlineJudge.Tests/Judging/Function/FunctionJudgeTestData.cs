using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Tests.Judging.Function;

internal static class FunctionJudgeTestData
{
    public static readonly Guid TestCaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static string TwoSumSpecJson => """
        {
          "functionName": "twoSum",
          "returnType": "int[]",
          "parameters": [
            { "name": "nums", "type": "int[]" },
            { "name": "target", "type": "int" }
          ],
          "supportedLanguages": ["cpp17", "csharp", "c11"]
        }
        """;

    public static string ReverseListSpecJson => """
        {
          "functionName": "reverseList",
          "returnType": "ListNode<int>",
          "parameters": [
            { "name": "head", "type": "ListNode<int>" }
          ],
          "supportedLanguages": ["cpp17", "csharp"]
        }
        """;

    public static string InvertTreeSpecJson => """
        {
          "functionName": "invertTree",
          "returnType": "TreeNode<int>",
          "parameters": [
            { "name": "root", "type": "TreeNode<int>" }
          ],
          "supportedLanguages": ["cpp17", "csharp"]
        }
        """;

    public static JudgeRequest CreateRequest(string functionSpecJson, string argumentsJson, string expectedJson, JudgeLanguage language = JudgeLanguage.Cpp17, string sourceCode = "class Solution {};")
    {
        return new JudgeRequest
        {
            SubmissionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ProblemId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            Language = language,
            JudgeMode = JudgeMode.Function,
            SourceCode = sourceCode,
            FunctionSpecJson = functionSpecJson,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            TestCases =
            [
                new JudgeCaseRequest
                {
                    TestCaseId = TestCaseId,
                    ArgumentsJson = argumentsJson,
                    ExpectedJson = expectedJson
                }
            ]
        };
    }

    public static JudgeRequest CreateTwoSumRequest(JudgeLanguage language = JudgeLanguage.Cpp17, string sourceCode = "class Solution {};")
    {
        return CreateRequest(
            TwoSumSpecJson,
            """{ "nums": [2, 7, 11, 15], "target": 9 }""",
            "[0, 1]",
            language,
            sourceCode);
    }

    public static JudgeRequest CreateReverseListRequest(JudgeLanguage language = JudgeLanguage.Cpp17, string sourceCode = "class Solution {};")
    {
        return CreateRequest(
            ReverseListSpecJson,
            """{ "head": [1, 2, 3] }""",
            "[3, 2, 1]",
            language,
            sourceCode);
    }

    public static JudgeRequest CreateInvertTreeRequest(JudgeLanguage language = JudgeLanguage.Cpp17, string sourceCode = "class Solution {};")
    {
        return CreateRequest(
            InvertTreeSpecJson,
            """{ "root": [4, 2, 7, 1, 3, 6, 9] }""",
            "[4, 7, 2, 9, 6, 3, 1]",
            language,
            sourceCode);
    }
}
