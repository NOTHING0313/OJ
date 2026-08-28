namespace OnlineJudge.Infrastructure.Teams;

public class TeamProjectOptions
{
    public const string SectionName = "TeamProjects";

    public string[] AllowedGitHosts { get; set; } = ["github.com", "gitee.com", "gitlab.com"];

    public string RepositoryStorageRoot { get; set; } = Path.Combine("App_Data", "team-repositories");

    public int MaxCommitHistory { get; set; } = 300;

    public int GitTimeoutSeconds { get; set; } = 30;

    public int MaxRepositorySizeMb { get; set; } = 100;

    public int SyncCooldownSeconds { get; set; } = 10;
}
