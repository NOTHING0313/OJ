using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;
using OnlineJudge.Infrastructure.Judging.Runners;

namespace OnlineJudge.Tests.Judging.Function;

public class MainGuardTests
{
    [Fact]
    public async Task C11FunctionMode_UserMain_ReturnsCompileErrorBeforeSandbox()
    {
        var sandbox = new ThrowingJudgeSandbox();
        var runner = new C11JudgeRunner(sandbox, new C11FunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.C11, "int main(void) { return 0; }");

        var result = await runner.RunAsync(request);

        Assert.Equal(JudgeStatus.CompileError, result.Status);
        Assert.False(sandbox.WasCalled);
        Assert.False(result.Status == JudgeStatus.SystemError);
        Assert.True(result.ErrorMessage?.Contains("main", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CSharpFunctionMode_UserMain_ReturnsCompileErrorBeforeSandbox()
    {
        var sandbox = new ThrowingJudgeSandbox();
        var runner = new CSharpJudgeRunner(sandbox, new CSharpFunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.CSharp, "public class Program { public static void Main() {} }");

        var result = await runner.RunAsync(request);

        Assert.Equal(JudgeStatus.CompileError, result.Status);
        Assert.False(sandbox.WasCalled);
        Assert.False(result.Status == JudgeStatus.SystemError);
        Assert.True(result.ErrorMessage?.Contains("Main", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Cpp17FunctionMode_UserMain_ReturnsCompileErrorBeforeSandbox()
    {
        var sandbox = new ThrowingJudgeSandbox();
        var runner = new Cpp17JudgeRunner(sandbox, new FakeFunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(JudgeLanguage.Cpp17, "int main() { return 0; }");

        var result = await runner.RunAsync(request);

        Assert.Equal(JudgeStatus.CompileError, result.Status);
        Assert.False(sandbox.WasCalled);
        Assert.False(result.Status == JudgeStatus.SystemError);
        Assert.True(result.ErrorMessage?.Contains("main", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ThrowingJudgeSandbox : IJudgeSandbox
    {
        public bool WasCalled { get; private set; }

        public Task<JudgeResult> RunAsync(JudgeRequest request, LanguageJudgeProfile profile, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Sandbox should not be called for main guard tests.");
        }
    }

    private sealed class FakeFunctionJudgeCodeBuilder : IFunctionJudgeCodeBuilder
    {
        public Result<JudgeRequest> Build(JudgeRequest request)
        {
            throw new InvalidOperationException("Builder should not be called for main guard tests.");
        }
    }
}
