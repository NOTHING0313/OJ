using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Infrastructure.Judging.Runners;
using OnlineJudge.Infrastructure.Judging.Sandbox;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Tests.Judging.Sandbox;

public class JudgeSandboxSecurityTests
{
    [Fact]
    public async Task CaptureBoundedAsync_SignalsOutputLimitBeforeTheStreamCompletes()
    {
        await using var stream = new BlockingOverflowStream(2048);
        var outputLimitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var budget = new CapturedOutputBudget(1024, outputLimitReached);

        var captureTask = DockerCommandClient.CaptureBoundedAsync(stream, budget);

        await outputLimitReached.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(captureTask.IsCompleted);

        stream.Complete();
        var captured = await captureTask;
        Assert.True(captured.Truncated);
        Assert.Equal(1024, Encoding.UTF8.GetByteCount(captured.Text));
    }

    [Fact]
    public async Task CaptureBoundedAsync_SharesOneBudgetAcrossStandardOutputAndError()
    {
        await using var standardOutput = new MemoryStream(Encoding.UTF8.GetBytes(new string('A', 700)));
        await using var standardError = new MemoryStream(Encoding.UTF8.GetBytes(new string('B', 700)));
        var outputLimitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var budget = new CapturedOutputBudget(1024, outputLimitReached);

        var captured = await Task.WhenAll(
            DockerCommandClient.CaptureBoundedAsync(standardOutput, budget),
            DockerCommandClient.CaptureBoundedAsync(standardError, budget));

        Assert.True(outputLimitReached.Task.IsCompleted);
        Assert.Equal(1024, captured.Sum(item => Encoding.UTF8.GetByteCount(item.Text)));
        Assert.Contains(captured, item => item.Truncated);
    }

    [Fact]
    public void DockerCreate_UsesRequiredIsolationAndResourceFlags()
    {
        var arguments = Arguments();

        AssertOption(arguments, "--network", "none");
        AssertOption(arguments, "--memory", "128m");
        AssertOption(arguments, "--memory-swap", "128m");
        AssertOption(arguments, "--cpus", "1");
        AssertOption(arguments, "--pids-limit", "64");
        AssertOption(arguments, "--security-opt", "no-new-privileges");
        AssertOption(arguments, "--cap-drop", "ALL");
        AssertOption(arguments, "--ipc", "none");
        AssertOption(arguments, "--user", "judge");
        AssertOption(arguments, "--ulimit", "fsize=67108864:67108864");
        Assert.Contains("--read-only", arguments);
        AssertOption(arguments, "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,size=64m");
    }

    [Fact]
    public void DockerCreate_HasManagedLabelsAndNoDangerousHostAccess()
    {
        var arguments = Arguments();
        var text = string.Join(' ', arguments);

        AssertOption(arguments, "--label", DockerCommandClient.ManagedLabel);
        Assert.Contains(DockerCommandClient.JudgeKindLabel, arguments);
        Assert.Contains($"{DockerCommandClient.SubmissionLabel}=77777777777777777777777777777777", arguments);
        Assert.DoesNotContain("--privileged", arguments);
        Assert.DoesNotContain("--device", arguments);
        Assert.DoesNotContain("--pid", arguments);
        Assert.DoesNotContain("--uts", arguments);
        Assert.DoesNotContain("seccomp=unconfined", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/var/run/docker.sock", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postgres", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("redis", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uploads", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("team-repositories", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DockerCreate_CompileMountsOnlyWritableSubmissionWorkspaceAndMinimalNonSecretEnvironment()
    {
        var arguments = Arguments();
        var volumeIndexes = arguments.Select((value, index) => (value, index)).Where(item => item.value == "-v").ToList();
        var environmentValues = arguments.Select((value, index) => (value, index))
            .Where(item => item.value == "--env")
            .Select(item => arguments[item.index + 1])
            .ToList();

        Assert.Single(volumeIndexes);
        Assert.Equal("C:/safe/workspace:/workspace", arguments[volumeIndexes[0].index + 1]);
        Assert.Equal(["HOME=/tmp", "DOTNET_CLI_HOME=/tmp/dotnet", "NUGET_PACKAGES=/tmp/nuget"], environmentValues);
        Assert.DoesNotContain(environmentValues, value => value.Contains("Connection", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Redis", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Jwt", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DockerCreate_RuntimeMountsSubmissionWorkspaceReadOnly()
    {
        var arguments = Arguments(DockerWorkspaceAccess.ReadOnly);
        var volumeIndex = arguments.ToList().IndexOf("-v");

        Assert.True(volumeIndex >= 0);
        Assert.Equal("C:/safe/workspace:/workspace:ro", arguments[volumeIndex + 1]);
    }

    [Fact]
    public void OutputLimit_MapsToBoundedRuntimeErrorWithoutNewJudgeStatus()
    {
        var result = DockerJudgeSandbox.CreateCaseResult(new JudgeCaseRequest
        {
            TestCaseId = Guid.NewGuid(),
            ExpectedOutput = string.Empty
        }, new DockerCommandResult(
            ExitCode: 0,
            StandardOutput: new string('x', 1024),
            StandardError: string.Empty,
            ElapsedMs: 10,
            TimedOut: false,
            OutputLimitExceeded: true));

        Assert.Equal(JudgeStatus.RuntimeError, result.Status);
        Assert.Equal("Output limit exceeded.", result.ErrorMessage);
        Assert.Null(result.ActualOutput);
    }

    [Fact]
    public async Task SubmissionWorkspaces_AreUniqueAndCleanedAcrossRuns()
    {
        var client = new WorkspaceRecordingClient();
        var sandbox = new DockerJudgeSandbox(client, NullLogger<DockerJudgeSandbox>.Instance);

        await sandbox.RunAsync(Request(Guid.NewGuid()), Cpp17JudgeRunner.Profile);
        await sandbox.RunAsync(Request(Guid.NewGuid()), Cpp17JudgeRunner.Profile);

        Assert.Equal(4, client.Workspaces.Count);
        Assert.Equal(2, client.Workspaces.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            [DockerWorkspaceAccess.ReadWrite, DockerWorkspaceAccess.ReadOnly, DockerWorkspaceAccess.ReadWrite, DockerWorkspaceAccess.ReadOnly],
            client.WorkspaceAccesses);
        Assert.All(client.Workspaces.Distinct(StringComparer.OrdinalIgnoreCase), workspace => Assert.False(Directory.Exists(workspace)));
        Assert.Equal(client.CreatedNames, client.RemovedNames);
    }

    [Fact]
    public async Task DockerInfrastructureFailure_IsExplicitlyRetryable()
    {
        var sandbox = new DockerJudgeSandbox(new FailingCreateClient(), NullLogger<DockerJudgeSandbox>.Instance);

        var result = await sandbox.RunAsync(Request(Guid.NewGuid()), Cpp17JudgeRunner.Profile);

        Assert.Equal(JudgeStatus.SystemError, result.Status);
        Assert.Equal(JudgeFailureKind.TransientInfrastructure, result.FailureKind);
    }

    [Fact]
    public void WorkerStartup_InvokesOnlyManagedJudgeContainerReconciliation()
    {
        var workerSource = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.JudgeWorker", "Worker.cs"));
        var clientSource = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.Infrastructure", "Judging", "Sandbox", "DockerCommandClient.cs"));

        Assert.Contains("ReconcileStaleContainersAsync", workerSource, StringComparison.Ordinal);
        Assert.Contains("label={ManagedLabel}", clientSource, StringComparison.Ordinal);
        Assert.Contains("label={JudgeKindLabel}", clientSource, StringComparison.Ordinal);
        Assert.Contains("status=exited", clientSource, StringComparison.Ordinal);
        Assert.Contains("RemoveSubmissionContainersAsync", clientSource, StringComparison.Ordinal);
        Assert.DoesNotContain("docker rm $(docker ps", clientSource, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> Arguments(DockerWorkspaceAccess workspaceAccess = DockerWorkspaceAccess.ReadWrite) => DockerCommandClient.BuildCreateArguments(
        "oj-00000000000000000000000000000000",
        new DockerContainerRequest(
            "C:/safe/workspace",
            128,
            "sandbox",
            "./main",
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            workspaceAccess),
        new JudgeSandboxOptions());

    private static void AssertOption(IReadOnlyList<string> arguments, string option, string value)
    {
        var index = arguments.ToList().IndexOf(option);
        Assert.True(index >= 0, $"Missing Docker option: {option}");
        Assert.Equal(value, arguments[index + 1]);
    }

    private static JudgeRequest Request(Guid submissionId) => new()
    {
        SubmissionId = submissionId,
        SourceCode = "#include <iostream>\nint main(){ std::cout << 1; }",
        TimeLimitMs = 1000,
        MemoryLimitMb = 64,
        TestCases =
        [
            new JudgeCaseRequest { TestCaseId = Guid.NewGuid(), Input = string.Empty, ExpectedOutput = "1" }
        ]
    };

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class WorkspaceRecordingClient : IDockerCommandClient
    {
        private readonly Dictionary<string, DockerContainerRequest> requests = [];

        public List<string> Workspaces { get; } = [];
        public List<DockerWorkspaceAccess> WorkspaceAccesses { get; } = [];
        public List<string> CreatedNames { get; } = [];
        public List<string> RemovedNames { get; } = [];

        public Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken)
        {
            CreatedNames.Add(containerName);
            Workspaces.Add(request.WorkspaceDirectory);
            WorkspaceAccesses.Add(request.WorkspaceAccess);
            requests[containerName] = request;
            return Task.FromResult(new string('a', 64));
        }

        public Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var isCompile = requests[containerName].Command.Contains("g++", StringComparison.Ordinal);
            return Task.FromResult(new DockerCommandResult(
                ExitCode: 0,
                StandardOutput: isCompile ? string.Empty : "1",
                StandardError: string.Empty,
                ElapsedMs: 1,
                TimedOut: false));
        }

        public Task RemoveAsync(string containerName, CancellationToken cancellationToken)
        {
            RemovedNames.Add(containerName);
            return Task.CompletedTask;
        }

        public Task<int> RemoveManagedContainersAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> RemoveSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class FailingCreateClient : IDockerCommandClient
    {
        public Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Docker is unavailable.");

        public Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RemoveAsync(string containerName, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> RemoveManagedContainersAsync(CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> RemoveSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class BlockingOverflowStream(int firstReadSize) : Stream
    {
        private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool firstRead = true;

        public void Complete() => completion.TrySetResult();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (firstRead)
            {
                firstRead = false;
                var count = Math.Min(firstReadSize, buffer.Length);
                buffer.Span[..count].Fill((byte)'A');
                return count;
            }

            await completion.Task.WaitAsync(cancellationToken);
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
