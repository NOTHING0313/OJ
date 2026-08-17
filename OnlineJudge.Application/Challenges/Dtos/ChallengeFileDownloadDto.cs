namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeFileDownloadDto
{
    public string FilePath { get; set; } = string.Empty;

    public string DownloadFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
}
