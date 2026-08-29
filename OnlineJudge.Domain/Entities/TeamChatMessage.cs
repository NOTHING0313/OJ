using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class TeamChatMessage
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid? SenderUserId { get; set; }
    public TeamChatMessageType Type { get; set; }
    public string? Content { get; set; }
    public Guid? RelatedChallengeId { get; set; }
    public Guid? RelatedPeerReviewAssignmentId { get; set; }
    public string? EventKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Team? Team { get; set; }
    public User? SenderUser { get; set; }
    public Challenge? RelatedChallenge { get; set; }
    public ChallengePeerReviewAssignment? RelatedPeerReviewAssignment { get; set; }
}
