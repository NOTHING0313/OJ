using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
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
            await WriteCompileAssetsAsync(tempDirectory, request.CompileAssets, profile, request.TestCases, cancellationToken);

            var compileResult = await RunDockerCommandAsync(
                tempDirectory,
                JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile),
                profile.DockerImageName,
                BuildCompileCommand(profile, request.CompileAssets),
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
                    ErrorMessage = GetCompileErrorMessage(compileResult, request.CompileAssets)
                };
            }

            DeleteCompileAssets(tempDirectory, request.CompileAssets);
            DeleteCompilerMetadata(tempDirectory, request.CompileAssets.Count > 0);
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
                ErrorMessage = request.CompileAssets.Count > 0 ? "Judge execution failed." : ex.Message
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

    internal static async Task WriteCompileAssetsAsync(
        string tempDirectory,
        IReadOnlyList<JudgeCompileAsset> compileAssets,
        LanguageJudgeProfile profile,
        IReadOnlyList<JudgeCaseRequest> testCases,
        CancellationToken cancellationToken)
    {
        var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            profile.SourceFileName
        };
        reservedNames.UnionWith(profile.ExtraFiles.Keys);
        reservedNames.UnionWith(testCases.Select(testCase => GetInputFileName(testCase.TestCaseId)));

        foreach (var asset in compileAssets)
        {
            if (!IsSafeWorkspaceFileName(asset.FileName) || !reservedNames.Add(asset.FileName))
            {
                throw new InvalidDataException("Judge compile asset file name is invalid or reserved.");
            }

            await File.WriteAllTextAsync(ResolveWorkspaceFile(tempDirectory, asset.FileName), asset.Content, new UTF8Encoding(false), cancellationToken);
        }
    }

    internal static string BuildCompileCommand(LanguageJudgeProfile profile, IReadOnlyList<JudgeCompileAsset> compileAssets)
    {
        if (profile.IncludesCompileAssetsByDefault)
        {
            return profile.CompileCommand;
        }

        var sourceFiles = compileAssets
            .Where(asset => profile.CompileAssetSourceExtensions.Contains(Path.GetExtension(asset.FileName)))
            .Select(asset => ShellQuote($"./{asset.FileName}"))
            .ToList();

        return sourceFiles.Count == 0
            ? profile.CompileCommand
            : $"{profile.CompileCommand} {string.Join(' ', sourceFiles)}";
    }

    internal static void DeleteCompileAssets(string tempDirectory, IReadOnlyList<JudgeCompileAsset> compileAssets)
    {
        foreach (var asset in compileAssets)
        {
            File.Delete(ResolveWorkspaceFile(tempDirectory, asset.FileName));
        }
    }

    internal static string GetCompileErrorMessage(DockerCommandResult result, IReadOnlyList<JudgeCompileAsset> compileAssets)
    {
        var message = GetErrorMessage(result, "Compilation failed.");
        if (ContainsStorageDiagnostic(message)
            || compileAssets.Any(asset => ContainsHiddenAssetDiagnostic(message, asset)))
        {
            return "Judge support source compilation failed.";
        }

        return message.Replace(ContainerWorkspace + "/", string.Empty, StringComparison.Ordinal);
    }

    private static bool ContainsHiddenAssetDiagnostic(string message, JudgeCompileAsset asset)
    {
        if (message.Contains(asset.FileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return asset.Content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length >= 8)
            .Any(line => message.Contains(line, StringComparison.Ordinal));
    }

    private static bool ContainsStorageDiagnostic(string message)
    {
        return message.Contains("judge-assets", StringComparison.OrdinalIgnoreCase)
            || message.Contains("StoredFileName", StringComparison.OrdinalIgnoreCase)
            || message.Contains("StorageRelativePath", StringComparison.OrdinalIgnoreCase)
            || message.Contains("/var/lib/", StringComparison.OrdinalIgnoreCase)
            || message.Contains("/opt/onlinejudge/", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(
                message,
                @"\b[0-9a-f]{32}\.(?:cpp|cc|cxx|h|hpp|c|cs)\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void DeleteCompilerMetadata(string tempDirectory, bool hasCompileAssets)
    {
        if (!hasCompileAssets)
        {
            return;
        }

        foreach (var pdbPath in Directory.EnumerateFiles(tempDirectory, "*.pdb", SearchOption.AllDirectories))
        {
            File.Delete(pdbPath);
        }
    }

    private static bool IsSafeWorkspaceFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && !Path.IsPathRooted(fileName)
            && !fileName.Contains('/')
            && !fileName.Contains('\\')
            && !fileName.Any(char.IsControl)
            && HasSafeFileNameCharacters(fileName)
            && string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal);
    }

    private static bool HasSafeFileNameCharacters(string fileName)
    {
        if (!char.IsLetterOrDigit(fileName[0]) && fileName[0] != '_')
        {
            return false;
        }

        return fileName.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.' or ' ');
    }

    private static string ResolveWorkspaceFile(string tempDirectory, string fileName)
    {
        if (!IsSafeWorkspaceFileName(fileName))
        {
            throw new InvalidDataException("Judge compile asset file name is invalid.");
        }

        var workspaceRoot = Path.GetFullPath(tempDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, fileName));
        var rootPrefix = workspaceRoot.EndsWith(Path.DirectorySeparatorChar)
            ? workspaceRoot
            : workspaceRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidDataException("Judge compile asset path escapes the workspace.");
        }

        return fullPath;
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
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

    internal sealed record DockerCommandResult(
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        int ElapsedMs,
        bool TimedOut);
}
