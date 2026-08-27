using System.Diagnostics;
using System.Text;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

public class DockerJudgeSandbox : IJudgeSandbox
{
    private const string ContainerWorkspace = "/workspace";

    private const UnixFileMode WorkspaceDirectoryMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute |
        UnixFileMode.GroupRead |
        UnixFileMode.GroupWrite |
        UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead |
        UnixFileMode.OtherWrite |
        UnixFileMode.OtherExecute;

    private const UnixFileMode WorkspaceParentMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.UserExecute |
        UnixFileMode.GroupRead |
        UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead |
        UnixFileMode.OtherExecute;

    public async Task<JudgeResult> RunAsync(JudgeRequest request, LanguageJudgeProfile profile, CancellationToken cancellationToken = default)
    {
        var tempDirectory = CreateTempDirectory(request.SubmissionId);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDirectory, profile.SourceFileName), request.SourceCode, cancellationToken);
            await WriteExtraFilesAsync(tempDirectory, profile.ExtraFiles, cancellationToken);
            await WriteTestCaseInputsAsync(tempDirectory, request.TestCases, cancellationToken);

            var compileResult = await RunDockerCommandAsync(
                tempDirectory,
                JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile),
                profile.DockerImageName,
                profile.CompileCommand,
                timeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            if (compileResult.TimedOut)
            {
                return new JudgeResult
                {
                    Status = JudgeStatus.CompileError,
                    ErrorMessage = "Compilation timed out."
                };
            }

            if (compileResult.ExitCode != 0)
            {
                return new JudgeResult
                {
                    Status = JudgeStatus.CompileError,
                    ErrorMessage = GetErrorMessage(compileResult, "Compilation failed.")
                };
            }

            return await RunTestCasesAsync(request, profile, tempDirectory, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new JudgeResult
            {
                Status = JudgeStatus.SystemError,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    private async Task<JudgeResult> RunTestCasesAsync(JudgeRequest request, LanguageJudgeProfile profile, string tempDirectory, CancellationToken cancellationToken)
    {
        var totalTimeUsedMs = 0;
        var caseResults = new List<JudgeCaseResult>();
        var overallStatus = JudgeStatus.Accepted;
        string? firstErrorMessage = null;

        foreach (var testCase in request.TestCases)
        {
            var inputFileName = GetInputFileName(testCase.TestCaseId);
            var runResult = await RunDockerCommandAsync(
                tempDirectory,
                JudgeResourceLimits.ResolveRunMemoryLimitMb(request.MemoryLimitMb),
                profile.DockerImageName,
                $"{profile.RunCommand} < {inputFileName}",
                timeout: TimeSpan.FromMilliseconds(Math.Max(request.TimeLimitMs, 1)),
                cancellationToken: cancellationToken);

            totalTimeUsedMs += runResult.ElapsedMs;
            var actualOutput = NormalizeOutput(runResult.StandardOutput);
            JudgeCaseResult caseResult;

            if (runResult.TimedOut)
            {
                caseResult = new JudgeCaseResult
                {
                    TestCaseId = testCase.TestCaseId,
                    Status = JudgeStatus.TimeLimitExceeded,
                    TimeUsedMs = runResult.ElapsedMs
                };
            }
            else if (runResult.ExitCode != 0)
            {
                var errorMessage = GetErrorMessage(runResult, $"Process exited with code {runResult.ExitCode}.");
                caseResult = new JudgeCaseResult
                {
                    TestCaseId = testCase.TestCaseId,
                    Status = JudgeStatus.RuntimeError,
                    TimeUsedMs = runResult.ElapsedMs,
                    ActualOutput = actualOutput,
                    ErrorMessage = errorMessage
                };
            }
            else if (actualOutput != NormalizeOutput(testCase.ExpectedOutput))
            {
                caseResult = new JudgeCaseResult
                {
                    TestCaseId = testCase.TestCaseId,
                    Status = JudgeStatus.WrongAnswer,
                    TimeUsedMs = runResult.ElapsedMs,
                    ActualOutput = actualOutput,
                    ErrorMessage = "Output does not match expected output."
                };
            }
            else
            {
                caseResult = new JudgeCaseResult
                {
                    TestCaseId = testCase.TestCaseId,
                    Status = JudgeStatus.Accepted,
                    TimeUsedMs = runResult.ElapsedMs,
                    ActualOutput = actualOutput
                };
            }

            caseResults.Add(caseResult);

            if (caseResult.Status != JudgeStatus.Accepted && overallStatus == JudgeStatus.Accepted)
            {
                overallStatus = caseResult.Status;
                firstErrorMessage = caseResult.ErrorMessage;
            }

            if (!request.CollectAllCaseResults && caseResult.Status != JudgeStatus.Accepted)
            {
                return new JudgeResult
                {
                    Status = caseResult.Status,
                    TimeUsedMs = totalTimeUsedMs,
                    ErrorMessage = caseResult.ErrorMessage,
                    CaseResults = caseResults
                };
            }
        }

        return new JudgeResult
        {
            Status = overallStatus,
            TimeUsedMs = totalTimeUsedMs,
            ErrorMessage = firstErrorMessage,
            CaseResults = caseResults
        };
    }

    private async Task<DockerCommandResult> RunDockerCommandAsync(
        string workspaceDirectory,
        int memoryLimitMb,
        string dockerImageName,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process();

        process.StartInfo.FileName = "docker";
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--rm");
        process.StartInfo.ArgumentList.Add("--network");
        process.StartInfo.ArgumentList.Add("none");
        process.StartInfo.ArgumentList.Add("--memory");
        process.StartInfo.ArgumentList.Add($"{Math.Max(memoryLimitMb, 16)}m");
        process.StartInfo.ArgumentList.Add("--cpus");
        process.StartInfo.ArgumentList.Add("1");
        process.StartInfo.ArgumentList.Add("--pids-limit");
        process.StartInfo.ArgumentList.Add("64");
        process.StartInfo.ArgumentList.Add("-v");
        process.StartInfo.ArgumentList.Add($"{workspaceDirectory}:{ContainerWorkspace}");
        process.StartInfo.ArgumentList.Add("-w");
        process.StartInfo.ArgumentList.Add(ContainerWorkspace);
        process.StartInfo.ArgumentList.Add(dockerImageName);
        process.StartInfo.ArgumentList.Add("bash");
        process.StartInfo.ArgumentList.Add("-lc");
        process.StartInfo.ArgumentList.Add($"umask 000; {command}");

        var standardOutputBuilder = new StringBuilder();
        var standardErrorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                standardOutputBuilder.AppendLine(args.Data);
            }
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                standardErrorBuilder.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeout, cancellationToken);

        if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
        {
            TryKill(process);
            stopwatch.Stop();

            return new DockerCommandResult(
                ExitCode: null,
                StandardOutput: standardOutputBuilder.ToString(),
                StandardError: standardErrorBuilder.ToString(),
                ElapsedMs: (int)stopwatch.ElapsedMilliseconds,
                TimedOut: true);
        }

        await waitTask;
        stopwatch.Stop();

        return new DockerCommandResult(
            ExitCode: process.ExitCode,
            StandardOutput: standardOutputBuilder.ToString(),
            StandardError: standardErrorBuilder.ToString(),
            ElapsedMs: (int)stopwatch.ElapsedMilliseconds,
            TimedOut: false);
    }

    private static async Task WriteTestCaseInputsAsync(string tempDirectory, IReadOnlyList<JudgeCaseRequest> testCases, CancellationToken cancellationToken)
    {
        foreach (var testCase in testCases)
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDirectory, GetInputFileName(testCase.TestCaseId)),
                testCase.Input,
                cancellationToken);
        }
    }

    private static async Task WriteExtraFilesAsync(string tempDirectory, IReadOnlyDictionary<string, string> extraFiles, CancellationToken cancellationToken)
    {
        foreach (var extraFile in extraFiles)
        {
            var path = Path.Combine(tempDirectory, extraFile.Key);
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
                SetWorkspaceDirectoryMode(directory);
            }

            await File.WriteAllTextAsync(path, extraFile.Value, cancellationToken);
        }
    }

    private static string CreateTempDirectory(Guid submissionId)
    {
        var parentDirectory = Path.Combine(Path.GetTempPath(), "onlinejudge");
        Directory.CreateDirectory(parentDirectory);

        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(parentDirectory, WorkspaceParentMode);
        }

        var directory = Path.Combine(parentDirectory, submissionId.ToString("N"));
        Directory.CreateDirectory(directory);
        SetWorkspaceDirectoryMode(directory);

        return directory;
    }

    private static void SetWorkspaceDirectoryMode(string directory)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(directory, WorkspaceDirectoryMode);
        }
    }

    private static string GetInputFileName(Guid testCaseId)
    {
        return $"{testCaseId:N}.input.txt";
    }

    private static string NormalizeOutput(string output)
    {
        return output.Replace("\r\n", "\n").TrimEnd();
    }

    private static string GetErrorMessage(DockerCommandResult result, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.StandardError.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return result.StandardOutput.Trim();
        }

        return fallback;
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

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record DockerCommandResult(
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        int ElapsedMs,
        bool TimedOut);
}