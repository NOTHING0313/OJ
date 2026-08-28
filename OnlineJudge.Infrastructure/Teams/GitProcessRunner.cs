using System.Diagnostics;
using System.Text;

namespace OnlineJudge.Infrastructure.Teams;

public sealed record GitProcessRequest(IReadOnlyList<string> Arguments, TimeSpan Timeout);

public sealed record GitProcessResult(int? ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool OutputTruncated)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}

public interface IGitProcessRunner
{
    Task<GitProcessResult> RunAsync(GitProcessRequest request, CancellationToken cancellationToken = default);
}

public sealed class GitProcessRunner(ITeamGitRepositoryStorage storage) : IGitProcessRunner
{
    public const int OutputLimitCharacters = 64 * 1024;

    public async Task<GitProcessResult> RunAsync(GitProcessRequest request, CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = CreateStartInfo(request.Arguments) };
        try
        {
            process.Start();
            process.StandardInput.Close();
        }
        catch
        {
            return new GitProcessResult(null, string.Empty, string.Empty, false, false);
        }

        var stdoutTask = ReadCappedAsync(process.StandardOutput, cancellationToken);
        var stderrTask = ReadCappedAsync(process.StandardError, cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(request.Timeout, CancellationToken.None);

        try
        {
            if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
            {
                TryKill(process);
                await process.WaitForExitAsync(CancellationToken.None);
                var timedOutOutput = await stdoutTask;
                var timedOutError = await stderrTask;
                return new GitProcessResult(null, timedOutOutput.Text, timedOutError.Text, true, timedOutOutput.Truncated || timedOutError.Truncated);
            }

            await waitTask;
            var output = await stdoutTask;
            var error = await stderrTask;
            return new GitProcessResult(process.ExitCode, output.Text, error.Text, false, output.Truncated || error.Truncated);
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    public ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var key in startInfo.Environment.Keys.Where(key => key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            startInfo.Environment.Remove(key);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] = storage.GlobalConfigPath;
        startInfo.Environment["GIT_LFS_SKIP_SMUDGE"] = "1";
        startInfo.Environment["HOME"] = storage.HomeDirectory;
        startInfo.Environment["XDG_CONFIG_HOME"] = storage.HomeDirectory;
        foreach (var proxyVariable in new[] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY", "http_proxy", "https_proxy", "all_proxy", "no_proxy" })
        {
            startInfo.Environment.Remove(proxyVariable);
        }

        foreach (var argument in SecurityArguments(storage.HooksDirectory).Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static IReadOnlyList<string> SecurityArguments(string hooksDirectory) =>
    [
        "-c", "credential.helper=",
        "-c", $"core.hooksPath={hooksDirectory}",
        "-c", "protocol.allow=never",
        "-c", "protocol.https.allow=always",
        "-c", "protocol.file.allow=never",
        "-c", "protocol.ext.allow=never",
        "-c", "http.followRedirects=false"
    ];

    internal static async Task<CappedText> ReadCappedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(Math.Min(OutputLimitCharacters, 4096));
        var buffer = new char[4096];
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0) break;
            var remaining = OutputLimitCharacters - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(read, remaining));
            }

            truncated |= read > remaining;
        }

        return new CappedText(builder.ToString(), truncated);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    internal sealed record CappedText(string Text, bool Truncated);
}
