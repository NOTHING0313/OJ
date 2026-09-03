namespace OnlineJudge.Tests.Frontend;

public class ProblemEditorSourceTests
{
    [Fact]
    public void AdminProblemEditor_CSharpStarterUsesPascalCaseHelper()
    {
        var source = File.ReadAllText(ResolveRepoFile("frontend", "src", "pages", "AdminProblemEditorPage.tsx"));

        Assert.Contains("const csharpFunctionName = toCSharpMethodName(functionName);", source);
        Assert.Contains("public ${csharpReturnType} ${csharpFunctionName}(", source);
    }

    [Fact]
    public void ProblemDetail_CSharpFallbackCanReplaceLegacyLowerCamelStarter()
    {
        var source = File.ReadAllText(ResolveRepoFile("frontend", "src", "pages", "ProblemDetailPage.tsx"));

        Assert.Contains("shouldReplaceLegacyCSharpStarter", source);
        Assert.Contains("starterCode.includes(`${originalName}(`)", source);
        Assert.Contains("!starterCode.includes(`${csharpName}(`)", source);
    }

    [Fact]
    public void AdminProblemEditor_JudgeAssetsUseDedicatedApiAndDoNotEnterStarterCodeOrLocalStorage()
    {
        var editorSource = File.ReadAllText(ResolveRepoFile("frontend", "src", "pages", "AdminProblemEditorPage.tsx"));
        var apiSource = File.ReadAllText(ResolveRepoFile("frontend", "src", "api", "problemsApi.ts"));

        Assert.Contains("uploadJudgeAsset", editorSource);
        Assert.Contains("getJudgeAssets", editorSource);
        Assert.Contains("deleteJudgeAsset", editorSource);
        Assert.Contains("body: formData", apiSource);
        Assert.DoesNotContain("localStorage.setItem", editorSource);
        Assert.DoesNotContain("judgeAssets", editorSource[editorSource.IndexOf("const payload", StringComparison.Ordinal)..editorSource.IndexOf("try {", editorSource.IndexOf("const payload", StringComparison.Ordinal), StringComparison.Ordinal)]);
    }

    [Fact]
    public void AdminTestCaseEditor_HasCrudValidationAndScopedAuthoringScrollbars()
    {
        var editorSource = File.ReadAllText(ResolveRepoFile("frontend", "src", "pages", "AdminTestCaseEditorPage.tsx"));
        var styles = File.ReadAllText(ResolveRepoFile("frontend", "src", "styles.css"));
        var detailSource = File.ReadAllText(ResolveRepoFile("frontend", "src", "pages", "ProblemDetailPage.tsx"));

        Assert.Contains("updateTestCase", editorSource);
        Assert.Contains("deleteTestCase", editorSource);
        Assert.Contains("确定删除该测试点吗？删除后不会影响历史提交，但不会再参与未来判题。", editorSource);
        Assert.Contains("Arguments JSON 格式无效", editorSource);
        Assert.Contains("Expected JSON 格式无效", editorSource);
        Assert.Contains(".testcase-editor-v2-page :is(textarea, .table-wrap, .content-block)", styles);
        Assert.Contains("formatFunctionSampleArguments", detailSource);
        Assert.Contains("formatFunctionSampleExpected", detailSource);
    }

    [Fact]
    public void JudgeWorker_UsesBoundRevisionAndPersistsImmutableResultSnapshots()
    {
        var source = File.ReadAllText(ResolveRepoFile("OnlineJudge.JudgeWorker", "JudgeJobProcessor.cs"));
        var factorySource = File.ReadAllText(ResolveRepoFile("OnlineJudge.Infrastructure", "Judging", "SubmissionJudgeRequestFactory.cs"));

        Assert.Contains("submission.ProblemJudgeRevision", source);
        Assert.Contains("LoadRevisionAsync", source);
        Assert.Contains("SubmissionJudgeRequestFactory.Create", source);
        Assert.DoesNotContain("problem.TestCases", source);
        Assert.DoesNotContain("compileAssetLoader.LoadAsync(submission.ProblemId", source);
        Assert.Contains("OrderBy(testCase => testCase.Order)", factorySource);
        Assert.Contains("TestCaseId = testCase.SourceTestCaseId", factorySource);
        Assert.DoesNotContain("submission.Problem.TestCases", factorySource);
        Assert.Contains("ExpectedOutputSnapshot = judgedCases[caseResult.TestCaseId].ExpectedOutput", source);
        Assert.Contains("ExpectedJsonSnapshot = judgedCases[caseResult.TestCaseId].ExpectedJson", source);
        Assert.Contains("VisibilitySnapshot = judgedCases[caseResult.TestCaseId].Visibility", source);
        Assert.Contains("ScoreSnapshot = judgedCases[caseResult.TestCaseId].Score", source);
    }

    private static string ResolveRepoFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName }.Concat(relativeSegments).ToArray());
    }
}
