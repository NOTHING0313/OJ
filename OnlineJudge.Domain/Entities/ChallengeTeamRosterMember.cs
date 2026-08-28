using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ChallengeTeamRosterMember
{
    public Guid Id { get; set; }
    public Guid ChallengeTeamParticipantId { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public string UserNameSnapshot { get; set; } = string.Empty;
    public TeamMemberRole TeamMemberRoleSnapshot { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public ChallengeTeamParticipant? ChallengeTeamParticipant { get; set; }
    public Challenge? Challenge { get; set; }
    public Team? Team { get; set; }
    public User? User { get; set; }
}
