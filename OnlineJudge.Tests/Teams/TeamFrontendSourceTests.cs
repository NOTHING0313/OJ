namespace OnlineJudge.Tests.Teams;

public class TeamFrontendSourceTests
{
    [Fact]
    public void TeamUi_HasRoutesNavigationOwnerMemberInvitationAndProjectBinding()
    {
        var root = FindRepositoryRoot();
        var main = File.ReadAllText(Path.Combine(root, "frontend", "src", "main.tsx"));
        var layout = File.ReadAllText(Path.Combine(root, "frontend", "src", "components", "AppHeaderView.tsx"));
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var styles = File.ReadAllText(Path.Combine(root, "frontend", "src", "styles.css"));

        Assert.Contains("/teams", main);
        Assert.Contains("/admin/teams", main);
        Assert.Contains("战队", layout);
        Assert.Contains("创建战队", page);
        Assert.Contains("待处理邀请", page);
        Assert.Contains("转让", page);
        Assert.Contains("退出战队", page);
        Assert.Contains("绑定项目", page);
        Assert.DoesNotContain("credential", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("team-page", styles);
        Assert.Contains("/api/team-invitations/my", api);
    }

    [Fact]
    public void TeamWorkspace_HasChatFirstProjectsCompactSidebarAndResponsiveLayout()
    {
        var root = FindRepositoryRoot();
        var main = File.ReadAllText(Path.Combine(root, "frontend", "src", "main.tsx"));
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var styles = File.ReadAllText(Path.Combine(root, "frontend", "src", "styles.css"));

        Assert.Contains("team-workspace-layout", page);
        Assert.True(page.IndexOf("聊天", StringComparison.Ordinal) < page.IndexOf("team.projects.map", StringComparison.Ordinal));
        Assert.Contains("/projects/${project.id}/history", page);
        Assert.Contains("管理项目", page);
        Assert.Contains("team-danger-zone", page);
        Assert.Contains("window.confirm", page);
        Assert.Contains("team-mobile-info-toggle", page);
        Assert.Contains("team-new-message-indicator", page);
        Assert.Contains("relatedPeerReviewAssignmentId", page);
        Assert.Contains("visibilitychange", page);
        Assert.Contains("3000", page);
        Assert.Contains("/api/teams/${teamId}/chat", api);
        Assert.Contains("/teams/:teamId/projects/:projectId/history", main);
        Assert.Contains("grid-template-columns: minmax(0, 3fr) minmax(250px, 1fr)", styles);
        Assert.Contains(".team-workspace-sidebar.mobile-open", styles);
        Assert.DoesNotContain("dangerouslySetInnerHTML", page);
        Assert.DoesNotContain("WebSocket", page);
        Assert.DoesNotContain("SignalR", page);
    }

    [Fact]
    public void NoTeamUi_IsNormalCompactValidatedAndEntersWorkspaceAfterCreate()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "TeamPage.tsx"));
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "teamsApi.ts"));
        var styles = File.ReadAllText(Path.Combine(root, "frontend", "src", "styles.css"));

        Assert.Contains("team-onboarding-page", page);
        Assert.Contains("与队友协作挑战、共享项目与代码历史。", page);
        Assert.Contains("if (isCreating) return", page);
        Assert.Contains("正在创建...", page);
        Assert.Contains("team-field-error", page);
        Assert.Contains("setTeam(createdTeam)", page);
        Assert.Contains("navigate(\"/teams\", { replace: true })", page);
        Assert.Contains("invitations.length > 0", page);
        Assert.Contains("team-invitation-row", page);
        Assert.DoesNotContain("暂无待处理邀请。", page);
        Assert.Contains("{error && <div className=\"alert error\">{error}</div>}", page);
        Assert.Contains("request<TeamDto | null>(\"/api/teams/my\")", api);
        Assert.Contains("width: min(100%, 680px)", styles);
        Assert.Contains("overflow-x: clip", styles);
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
