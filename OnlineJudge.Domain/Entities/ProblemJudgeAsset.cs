using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ProblemJudgeAsset
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public JudgeLanguage Language { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string NormalizedFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string StorageRelativePath { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Problem? Problem { get; set; }
}
