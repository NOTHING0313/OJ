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

    [Fact]
    public async Task C11FunctionMode_MainTextInsideComment_DoesNotRejectSubmission()
    {
        var sandbox = new RecordingJudgeSandbox();
        var runner = new C11JudgeRunner(sandbox, new C11FunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(
            JudgeLanguage.C11,
            "/* example: int main(void) */\nint* twoSum(int* nums, int numsSize, int target, int* returnSize);");

        await runner.RunAsync(request);

        Assert.True(sandbox.WasCalled);
    }

    [Fact]
    public async Task Cpp17FunctionMode_MainTextInsideString_DoesNotRejectSubmission()
    {
        var sandbox = new RecordingJudgeSandbox();
        var runner = new Cpp17JudgeRunner(sandbox, new Cpp17FunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(
            JudgeLanguage.Cpp17,
            "class Solution { const char* marker = \"int main()\"; };");

        await runner.RunAsync(request);

        Assert.True(sandbox.WasCalled);
    }

    [Fact]
    public async Task CSharpFunctionMode_MainTextInsideCommentAndString_DoesNotRejectSubmission()
    {
        var sandbox = new RecordingJudgeSandbox();
        var runner = new CSharpJudgeRunner(sandbox, new CSharpFunctionJudgeCodeBuilder());
        var request = FunctionJudgeTestData.CreateTwoSumRequest(
            JudgeLanguage.CSharp,
            """"
            // class Program
            public class Solution
            {
                private const string Regular = "Main(";
                private const string Verbatim = @"class Program";
                private const string InterpolatedVerbatim = @$"Main(";
                private const string Raw = """class Program Main(""";
            }
            """");

        await runner.RunAsync(request);

        Assert.True(sandbox.WasCalled);
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

    private sealed class RecordingJudgeSandbox : IJudgeSandbox
    {
        public bool WasCalled { get; private set; }

        public Task<JudgeResult> RunAsync(JudgeRequest request, LanguageJudgeProfile profile, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new JudgeResult { Status = JudgeStatus.Accepted });
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
