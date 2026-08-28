namespace OnlineJudge.Tests.Teams;

public class TeamFrontendSourceTests
{
    [Fact]
    public void TeamUi_HasRoutesNavigationOwnerMemberInvitationAndProjectBinding()
    {
        var root = FindRepositoryRoot();
        var main = File.ReadAllText(Path.Combine(root, "frontend", "src", "main.tsx"));
        var layout = File.ReadAllText(Path.Combine(root, "frontend", "src", "AppLayout.tsx"));
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var styles = File.ReadAllText(Path.Combine(root, "frontend", "src", "styles.css"));

        Assert.Contains("/teams", main);
        Assert.Contains("/admin/teams", main);
        Assert.Contains("战队", layout);
        Assert.Contains("创建战队", page);
        Assert.Contains("待处理邀请", page);
        Assert.Contains("转让队长", page);
        Assert.Contains("退出战队", page);
        Assert.Contains("绑定项目", page);
        Assert.Contains("公开 HTTPS Git 仓库", page);
        Assert.DoesNotContain("credential", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("team-page", styles);
        Assert.Contains("/api/team-invitations/my", api);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
