using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Models;

public class LanguageJudgeProfile
{
    public JudgeLanguage Language { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string SourceFileName { get; set; } = string.Empty;

    public string CompileCommand { get; set; } = string.Empty;

    public int CompileMemoryLimitMb { get; set; } = 512;

    public string RunCommand { get; set; } = string.Empty;

    public string DockerImageName { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> ExtraFiles { get; set; } = new Dictionary<string, string>();
}
