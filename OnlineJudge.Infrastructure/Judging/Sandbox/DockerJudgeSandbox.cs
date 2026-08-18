using System.Diagnostics;
using System.Text;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

public class DockerJudgeSandbox : IJudgeSandbox
{
    private const string ContainerWorkspace = "/workspace";
    private const string ContainerInputFile = "/judge-input.txt";

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

    private const UnixFileMode WorkspaceFileMode =
        UnixFileMode.UserRead |
        UnixFileMode.UserWrite |
        UnixFileMode.GroupRead |
        UnixFileMode.OtherRead;

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
            await WriteWorkspaceFileAsync(
                Path.Combine(tempDirectory, profile.SourceFileName),
                request.SourceCode,
                cancellationToken);

            await WriteExtraFilesAsync(tempDirectory, profile.ExtraFiles, cancellationToken);

            var compileResult = await RunDockerCommandAsync(
                tempDirectory,
                request.MemoryLimitMb,
                profile.DockerImageName,
                profile.CompileCommand,
                timeout: TimeSpan.FromSeconds(30),
                workspaceReadOnly: false,
                inputFilePath: null,
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

    private async Task<JudgeResult> RunTestCasesAsync(
        JudgeRequest request,
        LanguageJudgeProfile profile,
        string tempDirectory,
        CancellationToken cancellationToken)
    {
        var totalTimeUsedMs = 0;
        var caseResults = new List<JudgeCaseResult>();

        foreach (var testCase in request.TestCases)
        {
            var inputFilePath = GetInputFilePath(request.SubmissionId, testCase.TestCaseId);

            try
            {
                await WriteWorkspaceFileAsync(inputFilePath, testCase.Input, cancellationToken);

                var runResult = await RunDockerCommandAsync(
                    tempDirectory,
                    request.MemoryLimitMb,
                    profile.DockerImageName,
                    $"{profile.RunCommand} < {ContainerInputFile}",
                    timeout: TimeSpan.FromMilliseconds(Math.Max(request.TimeLimitMs, 1)),
                    workspaceReadOnly: true,
                    inputFilePath: inputFilePath,
                    cancellationToken: cancellationToken);

                totalTimeUsedMs += runResult.ElapsedMs;
                var actualOutput = NormalizeOutput(runResult.StandardOutput);

                if (runResult.TimedOut)
                {
                    caseResults.Add(new JudgeCaseResult
                    {
                        TestCaseId = testCase.TestCaseId,
                        Status = JudgeStatus.TimeLimitExceeded,
                        TimeUsedMs = runResult.ElapsedMs
                    });

                    return new JudgeResult
                    {
                        Status = JudgeStatus.TimeLimitExceeded,
                        TimeUsedMs = totalTimeUsedMs,
                        CaseResults = caseResults
                    };
                }

                if (runResult.ExitCode != 0)
                {
                    var errorMessage = GetErrorMessage(
                        runResult,
                        $"Process exited with code {runResult.ExitCode}.");

                    caseResults.Add(new JudgeCaseResult
                    {
                        TestCaseId = testCase.TestCaseId,
                        Status = JudgeStatus.RuntimeError,
                        TimeUsedMs = runResult.ElapsedMs,
                        ActualOutput = actualOutput,
                        ErrorMessage = errorMessage
                    });

                    return new JudgeResult
                    {
                        Status = JudgeStatus.RuntimeError,
                        TimeUsedMs = totalTimeUsedMs,
                        ErrorMessage = errorMessage,
                        CaseResults = caseResults
                    };
                }

                if (actualOutput != NormalizeOutput(testCase.ExpectedOutput))
                {
                    caseResults.Add(new JudgeCaseResult
                    {
                        TestCaseId = testCase.TestCaseId,
                        Status = JudgeStatus.WrongAnswer,
                        TimeUsedMs = runResult.ElapsedMs,
                        ActualOutput = actualOutput,
                        ErrorMessage = "Output does not match expected output."
                    });

                    return new JudgeResult
                    {
                        Status = JudgeStatus.WrongAnswer,
                        TimeUsedMs = totalTimeUsedMs,
                        ErrorMessage = "Output does not match expected output.",
                        CaseResults = caseResults
                    };
                }

                caseResults.Add(new JudgeCaseResult
                {
                    TestCaseId = testCase.TestCaseId,
                    Status = JudgeStatus.Accepted,
                    TimeUsedMs = runResult.ElapsedMs,
                    ActualOutput = actualOutput
                });
            }
            finally
            {
                TryDeleteFile(inputFilePath);
            }
        }

        return new JudgeResult
        {
            Status = JudgeStatus.Accepted,
            TimeUsedMs = totalTimeUsedMs,
            CaseResults = caseResults
        };
    }

    private async Task<DockerCommandResult> RunDockerCommandAsync(
        string workspaceDirectory,
        int memoryLimitMb,
        string dockerImageName,
        string command,
        TimeSpan timeout,
        bool workspaceReadOnly,
        string? inputFilePath,
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
        process.StartInfo.ArgumentList.Add(
            workspaceReadOnly
                ? $"{workspaceDirectory}:{ContainerWorkspace}:ro"
                : $"{workspaceDirectory}:{ContainerWorkspace}");

        if (!string.IsNullOrWhiteSpace(inputFilePath))
        {
            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.ArgumentList.Add(
                $"{inputFilePath}:{ContainerInputFile}:ro");
        }

        process.StartInfo.ArgumentList.Add("-w");
        process.StartInfo.ArgumentList.Add(ContainerWorkspace);

        process.StartInfo.ArgumentList.Add(dockerImageName);
        process.StartInfo.ArgumentList.Add("bash");
        process.StartInfo.ArgumentList.Add("-lc");

        process.StartInfo.ArgumentList.Add(
            workspaceReadOnly
                ? command
                : $"umask 000; {command}");

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

    private static async Task WriteExtraFilesAsync(
        string tempDirectory,
        IReadOnlyDictionary<string, string> extraFiles,
        CancellationToken cancellationToken)
    {
        foreach (var extraFile in extraFiles)
        {
            var path = Path.Combine(tempDirectory, extraFile.Key);
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                EnsureContainerWritableDirectory(directory);
            }

            await WriteWorkspaceFileAsync(
                path,
                extraFile.Value,
                cancellationToken);
        }
    }

    private static async Task WriteWorkspaceFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, content, cancellationToken);
        SetUnixMode(path, WorkspaceFileMode);
    }

    private static string CreateTempDirectory(Guid submissionId)
    {
        var parentDirectory = Path.Combine(
            Path.GetTempPath(),
            "onlinejudge");

        Directory.CreateDirectory(parentDirectory);
        SetUnixMode(parentDirectory, WorkspaceParentMode);

        var directory = Path.Combine(
            parentDirectory,
            submissionId.ToString("N"));

        Directory.CreateDirectory(directory);
        SetUnixMode(directory, WorkspaceDirectoryMode);

        return directory;
    }

    private static void EnsureContainerWritableDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        SetUnixMode(directory, WorkspaceDirectoryMode);
    }

    private static void SetUnixMode(string path, UnixFileMode mode)
    {
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, mode);
        }
    }

    private static string GetInputFilePath(
        Guid submissionId,
        Guid testCaseId)
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"onlinejudge-input-{submissionId:N}-{testCaseId:N}.txt");
    }

    private static string NormalizeOutput(string output)
    {
        return output.Replace("\r\n", "\n").TrimEnd();
    }

    private static string GetErrorMessage(
        DockerCommandResult result,
        string fallback)
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

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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