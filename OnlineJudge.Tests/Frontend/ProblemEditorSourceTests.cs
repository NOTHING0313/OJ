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
