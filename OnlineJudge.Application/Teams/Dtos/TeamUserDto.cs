namespace OnlineJudge.Application.Teams.Dtos;

public class TeamUserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
