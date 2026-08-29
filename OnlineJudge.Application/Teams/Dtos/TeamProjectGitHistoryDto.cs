using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamProjectGitHistoryDto
{
    public TeamProjectSyncStatus LastSyncStatus { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public IReadOnlyList<TeamGitCommitDto> Commits { get; set; } = [];
}
