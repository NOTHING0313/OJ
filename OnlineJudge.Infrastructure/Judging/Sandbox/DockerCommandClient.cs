using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

internal interface IDockerCommandClient
{
    Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken);

    Task<DockerCommandResult> StartAsync(string containerName, string containerId, TimeSpan timeout, CancellationToken cancellationToken);

    Task RemoveAsync(string containerName, CancellationToken cancellationToken);
}

internal sealed class DockerCommandClient : IDockerCommandClient
{
    public async Task<string> CreateAsync(string containerName, DockerContainerRequest request, CancellationToken cancellationToken)
    {
        var result = await RunCliAsync(BuildCreateArguments(containerName, request), cancellationToken);
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
        var standardOutputBuilder = new StringBuilder();
        var standardErrorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) => AppendLine(standardOutputBuilder, args.Data);
        process.ErrorDataReceived += (_, args) => AppendLine(standardErrorBuilder, args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitTask = process.WaitForExitAsync(cancellationToken);
        var memoryMetricPathTask = TryResolveMemoryMetricPathAsync(containerName, containerId, process, cancellationToken);
        var timeoutTask = Task.Delay(timeout, cancellationToken);

        if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryKill(process);
            stopwatch.Stop();

            var timedOutMetricPath = await AwaitTelemetryPathAsync(memoryMetricPathTask);
            var timedOutPeakMemoryBytes = TryReadPeakMemoryBytes(timedOutMetricPath);
            return new DockerCommandResult(
                ExitCode: null,
                StandardOutput: standardOutputBuilder.ToString(),
                StandardError: standardErrorBuilder.ToString(),
                ElapsedMs: ToElapsedMilliseconds(stopwatch),
                TimedOut: true,
                PeakMemoryBytes: timedOutPeakMemoryBytes,
                OomKilled: false,
                TelemetryWarning: GetMemoryTelemetryWarning(timedOutPeakMemoryBytes));
        }

        await waitTask;
        stopwatch.Stop();

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
            StandardOutput: standardOutputBuilder.ToString(),
            StandardError: standardErrorBuilder.ToString(),
            ElapsedMs: ToElapsedMilliseconds(stopwatch),
            TimedOut: false,
            PeakMemoryBytes: peakMemoryBytes,
            OomKilled: oomKilled == true,
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

    internal static IReadOnlyList<string> BuildCreateArguments(string containerName, DockerContainerRequest request)
    {
        return
        [
            "create",
            "--name",
            containerName,
            "--network",
            "none",
            "--memory",
            $"{Math.Max(request.MemoryLimitMb, 16)}m",
            "--cpus",
            "1",
            "--pids-limit",
            "64",
            "-v",
            $"{request.WorkspaceDirectory}:{DockerJudgeSandbox.ContainerWorkspace}",
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

    private static void AppendLine(StringBuilder builder, string? value)
    {
        if (value is not null)
        {
            builder.AppendLine(value);
        }
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
}

internal sealed record DockerContainerRequest(
    string WorkspaceDirectory,
    int MemoryLimitMb,
    string DockerImageName,
    string Command);

internal sealed record DockerCommandResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    int ElapsedMs,
    bool TimedOut,
    long? PeakMemoryBytes = null,
    bool OomKilled = false,
    string? TelemetryWarning = null);
