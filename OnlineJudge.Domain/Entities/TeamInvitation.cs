using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class TeamInvitation
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid InvitedUserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public TeamInvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public Team? Team { get; set; }
    public User? InvitedUser { get; set; }
    public User? InvitedByUser { get; set; }
}
