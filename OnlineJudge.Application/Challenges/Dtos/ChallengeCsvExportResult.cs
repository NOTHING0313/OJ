namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeCsvExportResult
{
    public byte[] Content { get; set; } = [];

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "text/csv; charset=utf-8";
}
