namespace OnlineJudge.Domain.Entities;

public class ChallengeTeamTaskCompletion
{
    public Guid Id { get; set; }
    public Guid ChallengeId { get; set; }
    public Guid ChallengeTaskId { get; set; }
    public Guid ChallengeTeamParticipantId { get; set; }
    public Guid? BestSubmissionId { get; set; }
    public Guid? ContributorUserId { get; set; }
    public int Score { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Challenge? Challenge { get; set; }
    public ChallengeTask? ChallengeTask { get; set; }
    public ChallengeTeamParticipant? ChallengeTeamParticipant { get; set; }
    public Submission? BestSubmission { get; set; }
    public User? ContributorUser { get; set; }
}
