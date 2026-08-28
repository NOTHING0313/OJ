using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamProjectAuditDto : TeamProjectDto
{
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? LastSyncAttemptAt { get; set; }
    public TeamProjectSyncStatus LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public string? DefaultBranch { get; set; }
}
