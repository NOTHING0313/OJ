using System.Diagnostics;

const string launcherTitle = "OnlineJudge Launcher";
const string frontendUrl = "http://localhost:5173";

Console.Title = launcherTitle;
WriteHeader();

var options = LauncherOptions.Parse(args);
if (options.ShowHelp)
{
    WriteUsage();
    WaitForExit();
    return 0;
}

try
{
    var projectRoot = FindProjectRoot();
    if (projectRoot is null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("scripts/start-dev.ps1 was not found.");
        Console.ResetColor();
        Console.WriteLine("Put OJLauncher.exe under the OnlineJudge repository root, or run it from a folder inside the repository.");
        WaitForExit();
        return 1;
    }

    Console.WriteLine($"Project root: {projectRoot}");
    Console.WriteLine();

    CheckCommand("docker");
    CheckCommand("dotnet");
    CheckCommand("npm");
    Console.WriteLine();

    var scriptPath = Path.Combine(projectRoot, "scripts", "start-dev.ps1");
    var scriptSupportsLan = ScriptSupportsLan(scriptPath);
    var powerShellArguments = BuildPowerShellArguments(scriptPath, options.UseLan && scriptSupportsLan);

    if (options.UseLan && !scriptSupportsLan)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("scripts/start-dev.ps1 does not support -Lan yet. Starting in normal mode.");
        Console.ResetColor();
        Console.WriteLine();
    }

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Starting development environment...");
    Console.ResetColor();
    Console.WriteLine($"powershell.exe {powerShellArguments}");
    Console.WriteLine();

    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = powerShellArguments,
        WorkingDirectory = projectRoot,
        UseShellExecute = false
    });

    if (process is null)
    {
        throw new InvalidOperationException("Failed to start powershell.exe.");
    }

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine();
        Console.WriteLine("The startup script failed. Check the log above for details.");
        Console.ResetColor();
        WaitForExit();
        return process.ExitCode;
    }

    if (!options.NoBrowser)
    {
        Console.WriteLine();
        Console.WriteLine("Waiting 5 seconds before opening browser...");
        Thread.Sleep(TimeSpan.FromSeconds(5));
        OpenBrowser(frontendUrl);
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine();
    Console.WriteLine("OnlineJudge development environment has been requested to start.");
    Console.ResetColor();
    WaitForExit();
    return 0;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Startup failed.");
    Console.ResetColor();
    Console.WriteLine(ex.Message);
    WaitForExit();
    return 1;
}

static void WriteHeader()
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("========================================");
    Console.WriteLine(" OnlineJudge Launcher");
    Console.WriteLine("========================================");
    Console.ResetColor();
    Console.WriteLine();
}

static void WriteUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  OJLauncher.exe [--no-browser] [--lan]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --no-browser   Do not open http://localhost:5173 automatically.");
    Console.WriteLine("  --lan          Pass -Lan to start-dev.ps1 if the script supports it.");
}

static string? FindProjectRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    for (var depth = 0; directory is not null && depth <= 8; depth++)
    {
        var scriptPath = Path.Combine(directory.FullName, "scripts", "start-dev.ps1");
        if (File.Exists(scriptPath))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return null;
}

static void CheckCommand(string command)
{
    var exists = CommandExists(command);
    if (exists)
    {
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write("[OK] ");
        Console.ResetColor();
        Console.WriteLine($"{command} found");
        return;
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("[WARN] ");
    Console.ResetColor();
    Console.WriteLine($"{command} was not found in PATH. start-dev.ps1 may still show more details.");
}

static bool CommandExists(string command)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "where.exe",
            Arguments = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return false;
        }

        process.WaitForExit(2000);
        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

static bool ScriptSupportsLan(string scriptPath)
{
    try
    {
        var content = File.ReadAllText(scriptPath);
        return content.Contains("$Lan", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Lan]", StringComparison.OrdinalIgnoreCase)
            || content.Contains("-Lan", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static string BuildPowerShellArguments(string scriptPath, bool useLan)
{
    var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
    return useLan ? $"{arguments} -Lan" : arguments;
}

static void OpenBrowser(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
        Console.WriteLine($"Browser opened: {url}");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Could not open the browser automatically: {ex.Message}");
        Console.ResetColor();
        Console.WriteLine($"Open this URL manually: {url}");
    }
}

static void WaitForExit()
{
    if (Console.IsInputRedirected)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey(intercept: true);
}

internal sealed record LauncherOptions(bool NoBrowser, bool UseLan, bool ShowHelp)
{
    public static LauncherOptions Parse(string[] args)
    {
        var noBrowser = false;
        var useLan = false;
        var showHelp = false;

        foreach (var arg in args)
        {
            switch (arg.Trim().ToLowerInvariant())
            {
                case "--no-browser":
                    noBrowser = true;
                    break;
                case "--lan":
                    useLan = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
            }
        }

        return new LauncherOptions(noBrowser, useLan, showHelp);
    }
}
