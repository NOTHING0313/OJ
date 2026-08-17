namespace OnlineJudge.Application.Challenges.Requests;

public class SubmitChallengeTaskFileRequest
{
    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public Stream FileStream { get; set; } = Stream.Null;
}
