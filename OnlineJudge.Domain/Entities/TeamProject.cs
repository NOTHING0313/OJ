using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class TeamProject
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string NormalizedRepositoryUrl { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public DateTimeOffset? LastSyncAttemptAt { get; set; }
    public TeamProjectSyncStatus LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public string? DefaultBranch { get; set; }
    public Team? Team { get; set; }
    public User? CreatedByUser { get; set; }
    public List<ChallengeTeamParticipant> ChallengeParticipations { get; set; } = [];
}
