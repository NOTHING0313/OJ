namespace OnlineJudge.Domain.Entities;

public class ChallengeTeamParticipant
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid TeamId { get; set; }
    public string TeamNameSnapshot { get; set; } = string.Empty;
    public Guid RegisteredByUserId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public Challenge? Challenge { get; set; }
    public Team? Team { get; set; }
    public User? RegisteredByUser { get; set; }
    public List<ChallengeTeamRosterMember> RosterMembers { get; set; } = [];
    public List<ChallengeTeamTaskCompletion> TaskCompletions { get; set; } = [];
    public List<Submission> Submissions { get; set; } = [];
}
