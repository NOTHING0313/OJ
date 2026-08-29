namespace OnlineJudge.Domain.Entities;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public User? OwnerUser { get; set; }
    public List<TeamMember> Members { get; set; } = [];
    public List<TeamProject> Projects { get; set; } = [];
    public List<TeamInvitation> Invitations { get; set; } = [];
    public List<TeamChatMessage> ChatMessages { get; set; } = [];
    public List<ChallengeTeamParticipant> ChallengeParticipations { get; set; } = [];
}
