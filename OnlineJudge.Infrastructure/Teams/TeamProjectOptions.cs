namespace OnlineJudge.Infrastructure.Teams;

public class TeamProjectOptions
{
    public const string SectionName = "TeamProjects";

    public string[] AllowedGitHosts { get; set; } = ["github.com", "gitee.com", "gitlab.com"];
}
