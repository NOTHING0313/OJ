namespace OnlineJudge.Tests.Teams;

public class TeamGitFrontendSourceTests
{
    [Fact]
    public void AdminUiHasSyncAndHistoryWhileBasicProjectDtoHasNoAuditFields()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var basicDto = File.ReadAllText(Path.Combine(root, "OnlineJudge.Application", "Teams", "Dtos", "TeamProjectDto.cs"));

        Assert.Contains("同步仓库", page);
        Assert.Contains("查看提交历史", page);
        Assert.Contains("同步中...", page);
        Assert.Contains("team-commit-history", page);
        Assert.DoesNotContain("dangerouslySetInnerHTML", page);
        Assert.Contains("/api/admin/teams/", api);
        Assert.DoesNotContain("LastSyncStatus", basicDto);
        Assert.DoesNotContain("LastSyncError", basicDto);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
