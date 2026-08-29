namespace OnlineJudge.Application.Teams.Dtos;

public class TeamProjectAuditDto : TeamProjectDto
{
    public DateTimeOffset? LastSyncAttemptAt { get; set; }
    public string? LastSyncError { get; set; }
    public string? DefaultBranch { get; set; }
}
