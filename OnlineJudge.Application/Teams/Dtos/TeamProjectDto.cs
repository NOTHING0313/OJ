using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public TeamProjectSyncStatus LastSyncStatus { get; set; }
}
