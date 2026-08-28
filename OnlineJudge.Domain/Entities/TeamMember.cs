using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class TeamMember
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public TeamMemberRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
    public Team? Team { get; set; }
    public User? User { get; set; }
}
