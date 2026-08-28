using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;
using OnlineJudge.Infrastructure.Judging.Runners;
using OnlineJudge.Infrastructure.Judging.Sandbox;
using OnlineJudge.Tests.Judging.Function;

namespace OnlineJudge.Tests.Judging.Sandbox;

public class JudgeCompileAssetTests : IDisposable
{
    private readonly string workspace = Path.Combine(Path.GetTempPath(), "onlinejudge-compile-asset-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CppAndCCompileCommands_IncludeOnlyTranslationUnits()
    {
        var cpp = DockerJudgeSandbox.BuildCompileCommand(Cpp17JudgeRunner.Profile,
        [
            Asset("Geometry.cpp"),
            Asset("Geometry.hpp")
        ]);
        var c = DockerJudgeSandbox.BuildCompileCommand(C11JudgeRunner.Profile,
        [
            Asset("helper.c"),
            Asset("helper.h")
        ]);

        Assert.Contains("'./Geometry.cpp'", cpp);
        Assert.DoesNotContain("Geometry.hpp", cpp);
        Assert.Contains("'./helper.c'", c);
        Assert.DoesNotContain("helper.h", c);
    }

    [Fact]
    public void CompileCommand_QuotesFileNamesWithSpacesAndUsesRelativePrefix()
    {
        var command = DockerJudgeSandbox.BuildCompileCommand(Cpp17JudgeRunner.Profile, [Asset("helper test.cpp")]);

        Assert.Contains("'./helper test.cpp'", command);
    }

    [Fact]
    public void CSharpSdkProfile_UsesDefaultCompileItemsForHiddenCs()
    {
        var profile = CSharpJudgeRunner.Profile;

        Assert.True(profile.IncludesCompileAssetsByDefault);
        Assert.Contains("<Project Sdk=\"Microsoft.NET.Sdk\">", profile.ExtraFiles["Main.csproj"]);
        Assert.DoesNotContain("EnableDefaultCompileItems", profile.ExtraFiles["Main.csproj"]);
        Assert.Equal(profile.CompileCommand, DockerJudgeSandbox.BuildCompileCommand(profile, [Asset("Geometry.cs")]));
    }

    [Fact]
    public async Task CompileAssets_AreRemovedBeforeRuntimeCanReadOriginalStoredOrWildcardSources()
    {
        Directory.CreateDirectory(workspace);
        var assets = new[]
        {
            Asset("Geometry.cs", "internal static class Geometry { internal const string Secret = \"hidden-csharp\"; }"),
            Asset("Geometry.cpp", "const char* secret = \"hidden-cpp\";"),
            Asset("Geometry.cc"),
            Asset("Geometry.cxx"),
            Asset("Geometry.h"),
            Asset("Geometry.hpp"),
            Asset("Geometry.c")
        };

        await DockerJudgeSandbox.WriteCompileAssetsAsync(workspace, assets, CSharpJudgeRunner.Profile, [], CancellationToken.None);
        Assert.Contains("hidden-csharp", await File.ReadAllTextAsync(Path.Combine(workspace, "Geometry.cs")));

        DockerJudgeSandbox.DeleteCompileAssets(workspace, assets);

        Assert.All(assets, asset => Assert.False(File.Exists(Path.Combine(workspace, asset.FileName))));
        Assert.False(File.Exists(Path.Combine(workspace, "4e1f9d43f1414f499203f904006ad397.cs")));
        Assert.Empty(Directory.EnumerateFiles(workspace, "*.cs"));
        Assert.Empty(Directory.EnumerateFiles(workspace, "*.cpp"));
    }

    [Fact]
    public async Task CompileAssets_CannotOverwritePlatformFilesOrEscapeWorkspace()
    {
        Directory.CreateDirectory(workspace);
        await File.WriteAllTextAsync(Path.Combine(workspace, "Program.cs"), "user source");

        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(
            workspace,
            [Asset("Program.cs", "hidden")],
            CSharpJudgeRunner.Profile,
            [],
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(
            workspace,
            [Asset("../Geometry.cs", "hidden")],
            CSharpJudgeRunner.Profile,
            [],
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(
            workspace,
            [Asset("helper.cpp;echo injected", "hidden")],
            Cpp17JudgeRunner.Profile,
            [],
            CancellationToken.None));

        Assert.Equal("user source", await File.ReadAllTextAsync(Path.Combine(workspace, "Program.cs")));
    }

    [Fact]
    public async Task CompileAssets_CannotOverwriteRunnerOrGeneratedInputFiles()
    {
        Directory.CreateDirectory(workspace);
        var testCaseId = Guid.NewGuid();
        var testCases = new[] { new JudgeCaseRequest { TestCaseId = testCaseId } };

        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(workspace, [Asset("main.cpp")], Cpp17JudgeRunner.Profile, [], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(workspace, [Asset("main.c")], C11JudgeRunner.Profile, [], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(workspace, [Asset("Main.csproj")], CSharpJudgeRunner.Profile, [], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => DockerJudgeSandbox.WriteCompileAssetsAsync(workspace, [Asset($"{testCaseId:N}.input.txt")], Cpp17JudgeRunner.Profile, testCases, CancellationToken.None));
    }

    [Fact]
    public void CompileError_ThatReferencesHiddenAsset_IsSanitized()
    {
        var result = new DockerJudgeSandbox.DockerCommandResult(
            ExitCode: 1,
            StandardOutput: string.Empty,
            StandardError: "/workspace/Geometry.cs(1,1): error CS1002: ; expected",
            ElapsedMs: 5,
            TimedOut: false);

        var message = DockerJudgeSandbox.GetCompileErrorMessage(result, [Asset("Geometry.cs", "secret source text")]);

        Assert.Equal("Judge support source compilation failed.", message);
        Assert.DoesNotContain("Geometry.cs", message);
        Assert.DoesNotContain("/workspace", message);
    }

    [Fact]
    public void CompileError_FromUserSource_PreservesUsefulDiagnostic()
    {
        var result = new DockerJudgeSandbox.DockerCommandResult(
            ExitCode: 1,
            StandardOutput: string.Empty,
            StandardError: "/workspace/main.cpp:12:5: error: expected ';'",
            ElapsedMs: 5,
            TimedOut: false);

        var message = DockerJudgeSandbox.GetCompileErrorMessage(result, [Asset("Geometry.hpp", "int intersect();")]);

        Assert.Contains("main.cpp:12:5", message);
        Assert.Contains("expected ';'", message);
        Assert.DoesNotContain("/workspace/", message);
    }

    [Fact]
    public void CompileError_WithStoragePathStoredNameAndSecretLine_IsSanitized()
    {
        const string storedFileName = "4e1f9d43f1414f499203f904006ad397.cs";
        const string secretLine = "SECRET_SOURCE_LINE";
        var result = new DockerJudgeSandbox.DockerCommandResult(
            ExitCode: 1,
            StandardOutput: string.Empty,
            StandardError: $"/var/lib/onlinejudge/judge-assets/problem/csharp/{storedFileName}: GeometrySecret.cs: {secretLine}",
            ElapsedMs: 5,
            TimedOut: false);

        var message = DockerJudgeSandbox.GetCompileErrorMessage(result, [Asset("GeometrySecret.cs", secretLine)]);

        Assert.Equal("Judge support source compilation failed.", message);
        Assert.DoesNotContain("/var/lib/onlinejudge/judge-assets", message);
        Assert.DoesNotContain(storedFileName, message);
        Assert.DoesNotContain("GeometrySecret.cs", message);
        Assert.DoesNotContain(secretLine, message);
    }

    [Fact]
    public void EmptyCompileAssets_PreserveExistingCompileCommands()
    {
        Assert.Equal(Cpp17JudgeRunner.Profile.CompileCommand, DockerJudgeSandbox.BuildCompileCommand(Cpp17JudgeRunner.Profile, []));
        Assert.Equal(C11JudgeRunner.Profile.CompileCommand, DockerJudgeSandbox.BuildCompileCommand(C11JudgeRunner.Profile, []));
        Assert.Equal(CSharpJudgeRunner.Profile.CompileCommand, DockerJudgeSandbox.BuildCompileCommand(CSharpJudgeRunner.Profile, []));
    }

    [Fact]
    public void FunctionBuilders_PreserveCompileAssetsWithoutAppendingThemToSubmissionSource()
    {
        var compileAsset = Asset("Geometry.cs", "static class Geometry { }");
        var csharpRequest = FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.CSharp);
        csharpRequest.CompileAssets = [compileAsset];
        var cppRequest = FunctionJudgeTestData.CreateTwoSumRequest();
        cppRequest.CompileAssets = [Asset("helper.cpp", "int helper() { return 1; }")];
        var c11Request = FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.C11, "int* twoSum(int* nums, int numsSize, int target, int* returnSize) { return NULL; }");
        c11Request.CompileAssets = [Asset("helper.c", "int helper(void) { return 1; }")];

        var csharp = new CSharpFunctionJudgeCodeBuilder().Build(csharpRequest);
        var cpp = new Cpp17FunctionJudgeCodeBuilder().Build(cppRequest);
        var c11 = new C11FunctionJudgeCodeBuilder().Build(c11Request);

        Assert.Same(compileAsset, Assert.Single(csharp.Value!.CompileAssets));
        Assert.Single(cpp.Value!.CompileAssets);
        Assert.Single(c11.Value!.CompileAssets);
        Assert.DoesNotContain(compileAsset.Content, csharp.Value.SourceCode);
    }

    public void Dispose()
    {
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static JudgeCompileAsset Asset(string fileName, string content = "source")
    {
        return new JudgeCompileAsset
        {
            FileName = fileName,
            Content = content
        };
    }
}
