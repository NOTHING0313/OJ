namespace OnlineJudge.Application.Teams.Dtos;

public class TeamGitCommitDto
{
    public string Sha { get; set; } = string.Empty;
    public string ShortSha { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorEmail { get; set; } = string.Empty;
    public DateTimeOffset AuthoredAt { get; set; }
    public string CommitterName { get; set; } = string.Empty;
    public string CommitterEmail { get; set; } = string.Empty;
    public DateTimeOffset CommittedAt { get; set; }
    public string Subject { get; set; } = string.Empty;
}
