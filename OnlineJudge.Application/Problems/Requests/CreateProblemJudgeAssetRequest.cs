using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class CreateProblemJudgeAssetRequest
{
    public JudgeLanguage Language { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public Stream Content { get; set; } = Stream.Null;
}
