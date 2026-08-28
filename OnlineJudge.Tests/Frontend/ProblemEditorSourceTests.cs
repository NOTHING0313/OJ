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
