namespace OnlineJudge.Tests.Teams;

public class TeamGitFrontendSourceTests
{
    [Fact]
    public void TeamWorkspaceShowsReadOnlySyncStatusWhileAuditErrorRemainsAdminOnly()
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
        Assert.Contains("LastSyncStatus", basicDto);
        Assert.Contains("LastSyncedAt", basicDto);
        Assert.DoesNotContain("LastSyncError", basicDto);
    }

    [Fact]
    public void MemberHistoryUsesStatefulWorkspaceContractAndOwnerOnlySyncAction()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamProjectHistoryPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var controller = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Controllers", "TeamsController.cs"));

        Assert.Contains("getTeamProjectHistory", page);
        Assert.Contains("仓库尚未同步", page);
        Assert.Contains("等待队长同步", page);
        Assert.Contains("syncTeamProject", page);
        Assert.Contains("isOwner &&", page);
        Assert.Contains("同步中...", page);
        Assert.Contains("同步失败", page);
        Assert.Contains("history.lastSyncError", page);
        Assert.DoesNotContain("Repository has not been synchronized", page);
        Assert.Contains("TeamProjectGitHistoryDto", api);
        Assert.Contains("/api/teams/${teamId}/projects/${projectId}/sync", api);
        Assert.Contains("GetHistoryAsync", controller);
        Assert.Contains("Ok(result.Value)", controller);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
