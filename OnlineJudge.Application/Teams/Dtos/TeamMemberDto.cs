using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Teams.Dtos;

public class TeamMemberDto
{
    public Guid Id { get; set; }
    public TeamUserDto User { get; set; } = new();
    public TeamMemberRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
