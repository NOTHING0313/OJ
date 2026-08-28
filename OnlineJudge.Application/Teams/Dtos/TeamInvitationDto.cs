using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamInvitationDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public TeamUserDto InvitedUser { get; set; } = new();
    public TeamUserDto InvitedByUser { get; set; } = new();
    public TeamInvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
