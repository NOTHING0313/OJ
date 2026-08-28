using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class LanguageJudgeProfile
{
    public JudgeLanguage Language { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string CompileCommand { get; set; } = string.Empty;

    /// <summary>
    /// Hidden asset extensions that must be added as explicit compiler translation units.
    /// </summary>
    public IReadOnlySet<string> CompileAssetSourceExtensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the language build system includes workspace source files automatically.
    /// </summary>
    public bool IncludesCompileAssetsByDefault { get; set; }

    public int CompileMemoryLimitMb { get; set; } = 512;

    public string RunCommand { get; set; } = string.Empty;

    public string DockerImageName { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> ExtraFiles { get; set; } = new Dictionary<string, string>();
}
