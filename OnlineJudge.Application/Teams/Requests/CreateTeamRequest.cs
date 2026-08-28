namespace OnlineJudge.Application.Teams.Requests;

public class CreateTeamRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
