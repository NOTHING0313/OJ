namespace OnlineJudge.Application.Teams.Requests;

public class UpdateTeamProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
}
