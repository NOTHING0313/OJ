using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamChatMessageDto
{
    public Guid Id { get; set; }
    public TeamChatMessageType Type { get; set; }
    public string? Content { get; set; }
    public TeamUserDto? Sender { get; set; }
    public Guid? RelatedChallengeId { get; set; }
    public Guid? RelatedPeerReviewAssignmentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
