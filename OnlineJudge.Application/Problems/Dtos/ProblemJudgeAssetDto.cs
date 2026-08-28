using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class ProblemJudgeAssetDto
{
    public Guid Id { get; set; }

    public JudgeLanguage Language { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
