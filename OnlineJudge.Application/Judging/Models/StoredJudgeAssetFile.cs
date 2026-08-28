namespace OnlineJudge.Application.Judging.Models;

public class StoredJudgeAssetFile
{
    public string StoredFileName { get; set; } = string.Empty;

    public string StorageRelativePath { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
