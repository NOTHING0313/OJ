using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Runners;
using OnlineJudge.Infrastructure.Judging.Sandbox;

namespace OnlineJudge.Tests.Judging.Sandbox;

public class JudgeMemoryTelemetryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(1024, 1)]
    [InlineData(1025, 2)]
    [InlineData(long.MaxValue, int.MaxValue)]
    public void PeakMemoryBytes_AreRoundedUpAndSafelyConverted(long bytes, int expectedKilobytes)
    {
        Assert.Equal(expectedKilobytes, DockerJudgeSandbox.ConvertPeakMemoryBytesToKb(bytes));
    }

    [Fact]
    public void MissingPeakMemory_RemainsNull()
    {
        Assert.Null(DockerJudgeSandbox.ConvertPeakMemoryBytesToKb(null));
    }

    [Fact]
    public void SubmissionMemory_IsMaximumKnownCaseMemory()
    {
        var result = DockerJudgeSandbox.GetPeakMemoryUsedKb(
        [
            CaseResult(20_000),
            CaseResult(null),
            CaseResult(35_000),
            CaseResult(28_000)
        ]);

        Assert.Equal(35_000, result);
        Assert.Null(DockerJudgeSandbox.GetPeakMemoryUsedKb([CaseResult(null)]));
    }

    [Fact]
    public void OomKilled_IsMemoryLimitExceededAndPreservesTelemetry()
    {
        var result = CreateCaseResult(Result(exitCode: 137, peakMemoryBytes: 33L * 1024 * 1024, oomKilled: true));

        Assert.Equal(JudgeStatus.MemoryLimitExceeded, result.Status);
        Assert.Equal(33 * 1024, result.MemoryUsedKb);
        Assert.Equal("Memory limit exceeded.", result.ErrorMessage);
    }

    [Fact]
    public void Timeout_TakesPriorityOverOomKilled()
    {
        var result = CreateCaseResult(Result(exitCode: 137, peakMemoryBytes: 20 * 1024, oomKilled: true, timedOut: true));

        Assert.Equal(JudgeStatus.TimeLimitExceeded, result.Status);
        Assert.Equal(20, result.MemoryUsedKb);
    }

    [Fact]
    public void NonzeroExitWithoutOom_IsRuntimeErrorAndPreservesTelemetry()
    {
        var result = CreateCaseResult(Result(exitCode: 2, peakMemoryBytes: 14 * 1024, standardError: "runtime failed"));

        Assert.Equal(JudgeStatus.RuntimeError, result.Status);
        Assert.Equal(14, result.MemoryUsedKb);
        Assert.Equal("runtime failed", result.ErrorMessage);
    }

    [Fact]
    public void WrongAnswer_PreservesTelemetry()
    {
        var result = CreateCaseResult(Result(exitCode: 0, peakMemoryBytes: 15 * 1024, standardOutput: "wrong"));

        Assert.Equal(JudgeStatus.WrongAnswer, result.Status);
        Assert.Equal(15, result.MemoryUsedKb);
    }

    [Fact]
    public void Accepted_PreservesTelemetry()
    {
        var result = CreateCaseResult(Result(exitCode: 0, peakMemoryBytes: 16 * 1024, standardOutput: "expected"));

        Assert.Equal(JudgeStatus.Accepted, result.Status);
        Assert.Equal(16, result.MemoryUsedKb);
    }

    [Fact]
    public void MissingTelemetry_DoesNotChangeAcceptedStatus()
    {
        var result = CreateCaseResult(Result(
            exitCode: 0,
            standardOutput: "expected",
            telemetryWarning: "Docker cgroup memory telemetry was unavailable."));

        Assert.Equal(JudgeStatus.Accepted, result.Status);
        Assert.Null(result.MemoryUsedKb);
    }

    [Fact]
    public void ContainerNames_AreUniqueAndDoNotContainUserInput()
    {
        var first = DockerJudgeSandbox.CreateContainerName();
        var second = DockerJudgeSandbox.CreateContainerName();

        Assert.Matches("^oj-[0-9a-f]{32}$", first);
        Assert.Matches("^oj-[0-9a-f]{32}$", second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void RuntimeMemoryLimit_RemainsInDockerCreateArguments()
    {
        var arguments = DockerCommandClient.BuildCreateArguments(
            "oj-00000000000000000000000000000000",
            new DockerContainerRequest("/tmp/workspace", 64, "sandbox", "./main"));
        var memoryIndex = arguments.ToList().IndexOf("--memory");

        Assert.True(memoryIndex >= 0);
        Assert.Equal("64m", arguments[memoryIndex + 1]);
    }

    [Fact]
    public async Task ContainerCleanup_HappensAfterNormalExit()
    {
        var client = new FakeDockerCommandClient(Result(exitCode: 0));
        var sandbox = CreateSandbox(client);

        await sandbox.RunDockerCommandAsync("workspace", 64, "sandbox", "./main", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(client.CreatedNames, client.RemovedNames);
    }

    [Fact]
    public async Task ContainerCleanup_HappensAfterTimeout()
    {
        var client = new FakeDockerCommandClient(Result(exitCode: null, timedOut: true));
        var sandbox = CreateSandbox(client);

        var result = await sandbox.RunDockerCommandAsync("workspace", 64, "sandbox", "./main", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.Equal(client.CreatedNames, client.RemovedNames);
    }

    [Fact]
    public async Task ContainerCleanup_HappensAfterException()
    {
        var client = new FakeDockerCommandClient(new InvalidOperationException("start failed"));
        var sandbox = CreateSandbox(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sandbox.RunDockerCommandAsync(
            "workspace", 64, "sandbox", "./main", TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Equal(client.CreatedNames, client.RemovedNames);
    }

    [Fact]
    public async Task ContainerCleanup_HappensAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new FakeDockerCommandClient(new OperationCanceledException(cancellation.Token));
        var sandbox = CreateSandbox(client);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sandbox.RunDockerCommandAsync(
            "workspace", 64, "sandbox", "./main", TimeSpan.FromSeconds(1), cancellation.Token));

        Assert.Equal(client.CreatedNames, client.RemovedNames);
    }

    [Fact]
    public async Task CompileMemory_DoesNotEnterSubmissionMemory()
    {
        var client = new FakeDockerCommandClient(
            Result(exitCode: 0, peakMemoryBytes: 200L * 1024 * 1024),
            Result(exitCode: 0, peakMemoryBytes: 20L * 1024, standardOutput: "expected"));
        var sandbox = CreateSandbox(client);

        var result = await sandbox.RunAsync(RequestWithSingleCase(), Cpp17JudgeRunner.Profile);

        Assert.Equal(JudgeStatus.Accepted, result.Status);
        Assert.Equal(20, result.MemoryUsedKb);
    }

    [Fact]
    public async Task EmptyCaseList_PreservesAcceptedWithUnknownMemory()
    {
        var client = new FakeDockerCommandClient(Result(exitCode: 0, peakMemoryBytes: 200L * 1024 * 1024));
        var sandbox = CreateSandbox(client);
        var request = RequestWithSingleCase();
        request.TestCases = [];

        var result = await sandbox.RunAsync(request, Cpp17JudgeRunner.Profile);

        Assert.Equal(JudgeStatus.Accepted, result.Status);
        Assert.Null(result.MemoryUsedKb);
        Assert.Empty(result.CaseResults);
    }

    [Fact]
    public async Task HiddenCompileAssets_AreDeletedBeforeTelemetryRuntimeStarts()
    {
        var hiddenAsset = new JudgeCompileAsset { FileName = "Hidden.cpp", Content = "const char* secret = \"hidden\";" };
        var client = new FakeDockerCommandClient(
            Result(exitCode: 0),
            request =>
            {
                Assert.False(File.Exists(Path.Combine(request.WorkspaceDirectory, hiddenAsset.FileName)));
                return Result(exitCode: 0, peakMemoryBytes: 12 * 1024, standardOutput: "expected");
            });
        var sandbox = CreateSandbox(client);
        var request = RequestWithSingleCase();
        request.CompileAssets = [hiddenAsset];

        var result = await sandbox.RunAsync(request, Cpp17JudgeRunner.Profile);

        Assert.Equal(JudgeStatus.Accepted, result.Status);
        Assert.Equal(12, result.MemoryUsedKb);
    }

    [Fact]
    public void CgroupMetricPaths_SupportV2AndV1()
    {
        var v2 = DockerCommandClient.GetMemoryMetricPaths("0::/system.slice/docker-test.scope");
        var v1 = DockerCommandClient.GetMemoryMetricPaths("5:cpu,memory:/docker/test");

        Assert.EndsWith(Path.Combine("system.slice", "docker-test.scope", "memory.peak"), Assert.Single(v2));
        Assert.EndsWith(Path.Combine("docker", "test", "memory.max_usage_in_bytes"), Assert.Single(v1));
    }

    [Fact]
    public void KnownCgroupPaths_UseTrustedContainerIdForFastProcessFallback()
    {
        const string containerId = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        var paths = DockerCommandClient.GetKnownMemoryMetricPaths(containerId);

        Assert.Contains($"/sys/fs/cgroup/system.slice/docker-{containerId}.scope/memory.peak", paths);
        Assert.Contains($"/sys/fs/cgroup/memory/docker/{containerId}/memory.max_usage_in_bytes", paths);
        Assert.Empty(DockerCommandClient.GetKnownMemoryMetricPaths("../../unsafe"));
    }

    private static DockerJudgeSandbox CreateSandbox(IDockerCommandClient client)
    {
        return new DockerJudgeSandbox(client, NullLogger<DockerJudgeSandbox>.Instance);
    }

    private static JudgeCaseResult CreateCaseResult(DockerCommandResult result)
    {
        return DockerJudgeSandbox.CreateCaseResult(
            new JudgeCaseRequest { TestCaseId = Guid.NewGuid(), ExpectedOutput = "expected" },
            result);
    }

    private static JudgeRequest RequestWithSingleCase()
    {
        return new JudgeRequest
        {
            SubmissionId = Guid.NewGuid(),
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main() { return 0; }",
            TimeLimitMs = 1000,
            MemoryLimitMb = 64,
            TestCases =
            [
                new JudgeCaseRequest
                {
                    TestCaseId = Guid.NewGuid(),
                    ExpectedOutput = "expected"
                }
            ]
        };
    }

    private static JudgeCaseResult CaseResult(int? memoryUsedKb)
    {
        return new JudgeCaseResult { MemoryUsedKb = memoryUsedKb };
    }

    private static DockerCommandResult Result(
        int? exitCode,
        long? peakMemoryBytes = null,
        bool oomKilled = false,
        bool timedOut = false,
        string standardOutput = "",
        string standardError = "",
        string? telemetryWarning = null)
    {
        return new DockerCommandResult(
            exitCode,
            standardOutput,
            standardError,
            ElapsedMs: 10,
            timedOut,
            peakMemoryBytes,
            oomKilled,
            telemetryWarning);
    }

    private sealed class FakeDockerCommandClient : IDockerCommandClient
    {
        private readonly Queue<Func<DockerContainerRequest, DockerCommandResult>> results = new();
        private readonly Exception? startException;
        private readonly Dictionary<string, DockerContainerRequest> requests = new();

        public FakeDockerCommandClient(params DockerCommandResult[] results)
        {
            foreach (var result in results)
            {
                this.results.Enqueue(_ => result);
            }
        }

        public FakeDockerCommandClient(params Func<DockerContainerRequest, DockerCommandResult>[] results)
        {
            foreach (var result in results)
            {
                this.results.Enqueue(result);
            }
        }

        public FakeDockerCommandClient(DockerCommandResult first, Func<DockerContainerRequest, DockerCommandResult> second)
            : this(_ => first, second)
        {
        }

        public FakeDockerCommandClient(Exception startException)
        {
            this.startException = startException;
        }

        public List<string> CreatedNames { get; } = [];

        public List<string> RemovedNames { get; } = [];

        public Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken)
        {
            CreatedNames.Add(containerName);
            requests.Add(containerName, request);
            return Task.FromResult("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        }

        public Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (startException is not null)
            {
                return Task.FromException<DockerCommandResult>(startException);
            }

            return Task.FromResult(results.Dequeue()(requests[containerName]));
        }

        public Task RemoveAsync(string containerName, CancellationToken cancellationToken)
        {
            RemovedNames.Add(containerName);
            return Task.CompletedTask;
        }

        public Task<int> RemoveManagedContainersAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> RemoveSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken) => Task.FromResult(0);
    }
}
