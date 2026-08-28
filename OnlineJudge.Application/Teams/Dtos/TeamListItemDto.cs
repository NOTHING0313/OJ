namespace OnlineJudge.Application.Teams.Dtos;

public class TeamListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TeamUserDto Owner { get; set; } = new();
    public int MemberCount { get; set; }
    public int ProjectCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
