using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

internal interface IDockerCommandClient
{
    Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken);

    Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken);

    Task RemoveAsync(string containerName, CancellationToken cancellationToken);

    Task<int> RemoveManagedContainersAsync(CancellationToken cancellationToken);

    Task<int> RemoveSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken);
}

internal sealed class DockerCommandClient : IDockerCommandClient
{
    internal const string ManagedLabel = "onlinejudge.managed=true";
    internal const string JudgeKindLabel = "onlinejudge.kind=judge";
    internal const string SubmissionLabel = "onlinejudge.submission";
    private readonly JudgeSandboxOptions options;

    public DockerCommandClient()
        : this(new JudgeSandboxOptions())
    {
    }

    public DockerCommandClient(JudgeSandboxOptions options)
    {
        this.options = options;
    }

    public async Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken)
    {
        var result = await RunCliAsync(BuildCreateArguments(containerName, request, options), cancellationToken);
        var containerId = result.StandardOutput.Trim();
        if (result.ExitCode != 0 || !IsSafeContainerId(containerId))
        {
            throw new InvalidOperationException("Docker container creation failed.");
        }

        return containerId;
    }

    public async Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = CreateProcess(["start", "--attach", containerName]);
        process.Start();
        var outputLimitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outputBudget = new CapturedOutputBudget(options.MaxCapturedOutputBytes, outputLimitReached);
        var standardOutputTask = CaptureBoundedAsync(process.StandardOutput.BaseStream, outputBudget);
        var standardErrorTask = CaptureBoundedAsync(process.StandardError.BaseStream, outputBudget);

        Task waitTask;
        try
        {
            waitTask = process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            TryKill(process);
            throw;
        }
        var memoryMetricPathTask = TryResolveMemoryMetricPathAsync(containerName, containerId, process, cancellationToken);
        var timeoutTask = Task.Delay(timeout, cancellationToken);

        var completionTask = await Task.WhenAny(waitTask, timeoutTask, outputLimitReached.Task);
        if (completionTask == outputLimitReached.Task)
        {
            await TryKillContainerAsync(containerName);
            TryKill(process);
            await WaitForExitWithoutThrowAsync(process);
            stopwatch.Stop();

            var limitedOutput = await standardOutputTask;
            var limitedError = await standardErrorTask;
            var limitedMetricPath = await AwaitTelemetryPathAsync(memoryMetricPathTask);
            var limitedPeakMemoryBytes = TryReadPeakMemoryBytes(limitedMetricPath);
            return new DockerCommandResult(
                ExitCode: null,
                StandardOutput: limitedOutput.Text,
                StandardError: limitedError.Text,
                ElapsedMs: ToElapsedMilliseconds(stopwatch),
                TimedOut: false,
                PeakMemoryBytes: limitedPeakMemoryBytes,
                OomKilled: false,
                OutputLimitExceeded: true,
                TelemetryWarning: GetMemoryTelemetryWarning(limitedPeakMemoryBytes));
        }

        if (completionTask == timeoutTask)
        {
            await TryKillContainerAsync(containerName);
            TryKill(process);
            await WaitForExitWithoutThrowAsync(process);
            cancellationToken.ThrowIfCancellationRequested();
            stopwatch.Stop();

            var timedOutOutput = await standardOutputTask;
            var timedOutError = await standardErrorTask;
            var timedOutMetricPath = await AwaitTelemetryPathAsync(memoryMetricPathTask);
            var timedOutPeakMemoryBytes = TryReadPeakMemoryBytes(timedOutMetricPath);
            return new DockerCommandResult(
                ExitCode: null,
                StandardOutput: timedOutOutput.Text,
                StandardError: timedOutError.Text,
                ElapsedMs: ToElapsedMilliseconds(stopwatch),
                TimedOut: true,
                PeakMemoryBytes: timedOutPeakMemoryBytes,
                OomKilled: false,
                OutputLimitExceeded: timedOutOutput.Truncated || timedOutError.Truncated,
                TelemetryWarning: GetMemoryTelemetryWarning(timedOutPeakMemoryBytes));
        }

        try
        {
            await waitTask;
        }
        catch (OperationCanceledException)
        {
            await TryKillContainerAsync(containerName);
            TryKill(process);
            await WaitForExitWithoutThrowAsync(process);
            throw;
        }
        stopwatch.Stop();

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;
        var memoryMetricPath = await AwaitTelemetryPathAsync(memoryMetricPathTask);
        var peakMemoryBytes = TryReadPeakMemoryBytes(memoryMetricPath);
        var oomKilled = await TryReadOomKilledAsync(containerName, cancellationToken);
        var telemetryWarnings = new[]
        {
            GetMemoryTelemetryWarning(peakMemoryBytes),
            oomKilled.HasValue ? null : "Docker OOM telemetry was unavailable."
        };

        return new DockerCommandResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutput.Text,
            StandardError: standardError.Text,
            ElapsedMs: ToElapsedMilliseconds(stopwatch),
            TimedOut: false,
            PeakMemoryBytes: peakMemoryBytes,
            OomKilled: oomKilled == true,
            OutputLimitExceeded: standardOutput.Truncated || standardError.Truncated,
            TelemetryWarning: string.Join(' ', telemetryWarnings.Where(message => message is not null)) is { Length: > 0 } warning ? warning : null);
    }

    public async Task RemoveAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await RunCliAsync(["rm", "-f", containerName], cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Docker container cleanup failed.");
        }
    }

    public async Task<int> RemoveManagedContainersAsync(CancellationToken cancellationToken)
    {
        var list = await RunCliAsync(
            [
                "ps", "-aq",
                "--filter", $"label={ManagedLabel}",
                "--filter", $"label={JudgeKindLabel}",
                "--filter", "status=exited",
                "--filter", "status=dead"
            ],
            cancellationToken);
        if (list.ExitCode != 0)
        {
            throw new InvalidOperationException("Managed judge container discovery failed.");
        }

        var containerIds = list.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsSafeContainerId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var containerId in containerIds)
        {
            await RemoveAsync(containerId, cancellationToken);
        }

        return containerIds.Count;
    }

    public async Task<int> RemoveSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var list = await RunCliAsync(
            [
                "ps", "-aq",
                "--filter", $"label={ManagedLabel}",
                "--filter", $"label={JudgeKindLabel}",
                "--filter", $"label={SubmissionLabel}={submissionId:N}"
            ],
            cancellationToken);
        if (list.ExitCode != 0)
        {
            throw new InvalidOperationException("Submission judge container discovery failed.");
        }

        var containerIds = list.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsSafeContainerId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var containerId in containerIds)
        {
            await RemoveAsync(containerId, cancellationToken);
        }

        return containerIds.Count;
    }

    internal static IReadOnlyList<string> BuildCreateArguments(string containerName, DockerContainerRequest request)
        => BuildCreateArguments(containerName, request, new JudgeSandboxOptions());

    internal static IReadOnlyList<string> BuildCreateArguments(string containerName, DockerContainerRequest request, JudgeSandboxOptions options)
    {
        var memoryLimitMb = Math.Max(request.MemoryLimitMb, 16);
        var fileSizeLimitBytes = (long)options.TempFileSystemSizeMb * 1024 * 1024;
        var workspaceMount = request.WorkspaceAccess == DockerWorkspaceAccess.ReadOnly
            ? $"{request.WorkspaceDirectory}:{DockerJudgeSandbox.ContainerWorkspace}:ro"
            : $"{request.WorkspaceDirectory}:{DockerJudgeSandbox.ContainerWorkspace}";
        IReadOnlyList<string> submissionLabel = request.SubmissionId.HasValue
            ? ["--label", $"{SubmissionLabel}={request.SubmissionId.Value:N}"]
            : [];
        return
        [
            "create",
            "--name",
            containerName,
            "--network",
            "none",
            "--ipc",
            "none",
            "--memory",
            $"{memoryLimitMb}m",
            "--memory-swap",
            $"{memoryLimitMb}m",
            "--cpus",
            options.CpuLimit.ToString(CultureInfo.InvariantCulture),
            "--pids-limit",
            options.PidsLimit.ToString(CultureInfo.InvariantCulture),
            "--security-opt",
            "no-new-privileges",
            "--cap-drop",
            "ALL",
            "--user",
            "judge",
            "--ulimit",
            $"fsize={fileSizeLimitBytes}:{fileSizeLimitBytes}",
            "--read-only",
            "--tmpfs",
            $"/tmp:rw,noexec,nosuid,nodev,size={options.TempFileSystemSizeMb}m",
            "--label",
            ManagedLabel,
            "--label",
            JudgeKindLabel,
            ..submissionLabel,
            "--env",
            "HOME=/tmp",
            "--env",
            "DOTNET_CLI_HOME=/tmp/dotnet",
            "--env",
            "NUGET_PACKAGES=/tmp/nuget",
            "-v",
            workspaceMount,
            "-w",
            DockerJudgeSandbox.ContainerWorkspace,
            request.DockerImageName,
            "bash",
            "-lc",
            $"umask 000; {request.Command}"
        ];
    }

    internal static IReadOnlyList<string> GetMemoryMetricPaths(string procCgroupText)
    {
        var paths = new List<string>();

        foreach (var line in procCgroupText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(':', 3);
            if (parts.Length != 3)
            {
                continue;
            }

            if (parts[0] == "0" && string.IsNullOrEmpty(parts[1]))
            {
                paths.Add(BuildCgroupMetricPath("/sys/fs/cgroup", parts[2], "memory.peak"));
                continue;
            }

            if (parts[1].Split(',').Contains("memory", StringComparer.Ordinal))
            {
                paths.Add(BuildCgroupMetricPath("/sys/fs/cgroup/memory", parts[2], "memory.max_usage_in_bytes"));
            }
        }

        return paths;
    }

    internal static IReadOnlyList<string> GetKnownMemoryMetricPaths(string containerId)
    {
        if (!IsSafeContainerId(containerId))
        {
            return [];
        }

        return
        [
            $"/sys/fs/cgroup/system.slice/docker-{containerId}.scope/memory.peak",
            $"/sys/fs/cgroup/docker.slice/docker-{containerId}.scope/memory.peak",
            $"/sys/fs/cgroup/docker/{containerId}/memory.peak",
            $"/sys/fs/cgroup/memory/system.slice/docker-{containerId}.scope/memory.max_usage_in_bytes",
            $"/sys/fs/cgroup/memory/docker.slice/docker-{containerId}.scope/memory.max_usage_in_bytes",
            $"/sys/fs/cgroup/memory/docker/{containerId}/memory.max_usage_in_bytes"
        ];
    }

    private static async Task<string?> TryResolveMemoryMetricPathAsync(
        string containerName,
        string containerId,
        Process attachedProcess,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        while (!attachedProcess.HasExited)
        {
            var knownPath = GetKnownMemoryMetricPaths(containerId).FirstOrDefault(File.Exists);
            if (knownPath is not null)
            {
                return knownPath;
            }

            var pid = await TryReadContainerPidAsync(containerName, cancellationToken);
            if (pid is > 0)
            {
                try
                {
                    var procCgroupText = await File.ReadAllTextAsync($"/proc/{pid.Value}/cgroup", cancellationToken);
                    return GetMemoryMetricPaths(procCgroupText).FirstOrDefault(File.Exists);
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }

            await Task.Delay(10, cancellationToken);
        }

        return GetKnownMemoryMetricPaths(containerId).FirstOrDefault(File.Exists);
    }

    private static async Task<int?> TryReadContainerPidAsync(string containerName, CancellationToken cancellationToken)
    {
        var result = await RunCliAsync(["inspect", "--format", "{{.State.Pid}}", containerName], cancellationToken);
        return result.ExitCode == 0
            && int.TryParse(result.StandardOutput.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
                ? pid
                : null;
    }

    private static async Task<bool?> TryReadOomKilledAsync(string containerName, CancellationToken cancellationToken)
    {
        try
        {
            var result = await RunCliAsync(["inspect", "--format", "{{.State.OOMKilled}}", containerName], cancellationToken);
            return result.ExitCode == 0
                && bool.TryParse(result.StandardOutput.Trim(), out var oomKilled)
                    ? oomKilled
                    : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> AwaitTelemetryPathAsync(Task<string?> memoryMetricPathTask)
    {
        try
        {
            return await memoryMetricPathTask;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static long? TryReadPeakMemoryBytes(string? memoryMetricPath)
    {
        if (memoryMetricPath is null)
        {
            return null;
        }

        try
        {
            var value = File.ReadAllText(memoryMetricPath).Trim();
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var peakMemoryBytes) && peakMemoryBytes >= 0
                ? peakMemoryBytes
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? GetMemoryTelemetryWarning(long? peakMemoryBytes)
    {
        return peakMemoryBytes is null ? "Docker cgroup memory telemetry was unavailable." : null;
    }

    private static string BuildCgroupMetricPath(string root, string cgroupPath, string metricFileName)
    {
        var relativePath = cgroupPath
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(root, relativePath, metricFileName);
    }

    private static bool IsSafeContainerId(string containerId)
    {
        return containerId.Length is >= 12 and <= 64 && containerId.All(Uri.IsHexDigit);
    }

    private static async Task<CliResult> RunCliAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var process = CreateProcess(arguments);
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new CliResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    internal static async Task<CapturedStream> CaptureBoundedAsync(Stream stream, CapturedOutputBudget budget)
    {
        var captured = new MemoryStream(Math.Min(budget.InitialBytes, 81920));
        var buffer = new byte[81920];
        var truncated = false;
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) break;

            var granted = budget.Claim(read);
            if (granted > 0)
            {
                await captured.WriteAsync(buffer.AsMemory(0, granted));
            }

            if (granted < read)
            {
                truncated = true;
            }
        }

        return new CapturedStream(Encoding.UTF8.GetString(captured.ToArray()), truncated);
    }

    private static async Task WaitForExitWithoutThrowAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch
        {
        }
    }

    private static async Task TryKillContainerAsync(string containerName)
    {
        try
        {
            await RunCliAsync(["kill", containerName], CancellationToken.None);
        }
        catch
        {
        }
    }

    private static Process CreateProcess(IReadOnlyList<string> arguments)
    {
        var process = new Process();
        process.StartInfo.FileName = "docker";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private static int ToElapsedMilliseconds(Stopwatch stopwatch)
    {
        return (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    internal sealed record CapturedStream(string Text, bool Truncated);
}

internal sealed record DockerContainerRequest(
    string WorkspaceDirectory,
    int MemoryLimitMb,
    string DockerImageName,
    string Command,
    Guid? SubmissionId = null,
    DockerWorkspaceAccess WorkspaceAccess = DockerWorkspaceAccess.ReadWrite);

internal enum DockerWorkspaceAccess
{
    ReadWrite,
    ReadOnly
}

internal sealed class CapturedOutputBudget
{
    private readonly object gate = new();
    private readonly TaskCompletionSource outputLimitReached;
    private int remainingBytes;

    public CapturedOutputBudget(int maxBytes, TaskCompletionSource outputLimitReached)
    {
        InitialBytes = maxBytes;
        remainingBytes = maxBytes;
        this.outputLimitReached = outputLimitReached;
    }

    public int InitialBytes { get; }

    public int Claim(int requestedBytes)
    {
        lock (gate)
        {
            var grantedBytes = Math.Min(remainingBytes, requestedBytes);
            remainingBytes -= grantedBytes;
            if (grantedBytes < requestedBytes)
            {
                outputLimitReached.TrySetResult();
            }

            return grantedBytes;
        }
    }
}

internal sealed record DockerCommandResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    int ElapsedMs,
    bool TimedOut,
    long? PeakMemoryBytes = null,
    bool OomKilled = false,
    string? TelemetryWarning = null,
    bool OutputLimitExceeded = false);
