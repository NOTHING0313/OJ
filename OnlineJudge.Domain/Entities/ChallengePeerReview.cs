using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class ChallengePeerReview
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid ReviewerParticipantId { get; set; }
    public Guid TargetParticipantId { get; set; }
    public ChallengePeerReviewStatus Status { get; set; } = ChallengePeerReviewStatus.Draft;
    public int? OverallScore { get; set; }
    public string? Summary { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ChallengePeerReviewAssignment? Assignment { get; set; }
    public Challenge? Challenge { get; set; }
    public ChallengeTeamParticipant? ReviewerParticipant { get; set; }
    public ChallengeTeamParticipant? TargetParticipant { get; set; }
}
