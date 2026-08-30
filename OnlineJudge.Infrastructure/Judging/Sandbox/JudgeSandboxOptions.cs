using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

public sealed class JudgeSandboxOptions
{
    public const string SectionName = "JudgeSandbox";

    public int CpuLimit { get; init; } = 1;

    public int PidsLimit { get; init; } = 64;

    public int TempFileSystemSizeMb { get; init; } = 64;

    public int MaxCapturedOutputBytes { get; init; } = 1024 * 1024;

    public static JudgeSandboxOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new JudgeSandboxOptions
        {
            CpuLimit = ReadPositive(section, nameof(CpuLimit), 1),
            PidsLimit = ReadPositive(section, nameof(PidsLimit), 64),
            TempFileSystemSizeMb = ReadPositive(section, nameof(TempFileSystemSizeMb), 64),
            MaxCapturedOutputBytes = ReadPositive(section, nameof(MaxCapturedOutputBytes), 1024 * 1024)
        };
    }

    private static int ReadPositive(IConfiguration section, string key, int fallback) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : fallback;
}
