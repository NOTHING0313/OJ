namespace OnlineJudge.Domain.Entities;

public class ChallengePeerReviewAssignment
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid ReviewerParticipantId { get; set; }
    public Guid TargetParticipantId { get; set; }
    public string ReviewerTeamNameSnapshot { get; set; } = string.Empty;
    public string TargetTeamNameSnapshot { get; set; } = string.Empty;
    public string TargetProjectNameSnapshot { get; set; } = string.Empty;
    public string TargetRepositoryUrlSnapshot { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Challenge? Challenge { get; set; }
    public ChallengeTeamParticipant? ReviewerParticipant { get; set; }
    public ChallengeTeamParticipant? TargetParticipant { get; set; }
    public ChallengePeerReview? Review { get; set; }
}
