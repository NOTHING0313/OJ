namespace OnlineJudge.Application.Teams.Dtos;

public class TeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TeamUserDto Owner { get; set; } = new();
    public IReadOnlyList<TeamMemberDto> Members { get; set; } = [];
    public IReadOnlyList<TeamProjectDto> Projects { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}
