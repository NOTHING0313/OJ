namespace OnlineJudge.Tests.Leaderboards;

public class LeaderboardSeasonFrontendSourceTests
{
    [Fact]
    public void MainLeaderboardRoute_UsesSeasonLeaderboard()
    {
        var main = Read("frontend", "src", "main.tsx");
        var home = Read("frontend", "src", "pages", "LeaderboardHomePage.tsx");
        Assert.Contains("SeasonLeaderboardPage", main, StringComparison.Ordinal);
        Assert.Contains("getCurrentSeasonLeaderboard", home, StringComparison.Ordinal);
        Assert.DoesNotContain("getGlobalUserLeaderboard", home, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfileAndAdminUi_ExposeOnlyAuthorizedSeasonControls()
    {
        var account = Read("frontend", "src", "pages", "AccountSettingsPage.tsx");
        var admin = Read("frontend", "src", "pages", "AdminLeaderboardSeasonPage.tsx");
        var main = Read("frontend", "src", "main.tsx");
        Assert.Contains("排行榜匿名", account, StringComparison.Ordinal);
        Assert.Contains("updateLeaderboardAnonymity", account, StringComparison.Ordinal);
        Assert.Contains("const isRoot = currentUser?.role === 3", admin, StringComparison.Ordinal);
        Assert.Contains("榜单管理", admin, StringComparison.Ordinal);
        Assert.Contains("allowedRoles={[2, 3]}", main, StringComparison.Ordinal);
    }

    [Fact]
    public void LeaderboardPrivacy_UsesAccessibleCompactSwitchAndTransientFeedback()
    {
        var account = Read("frontend", "src", "pages", "AccountSettingsPage.tsx");
        var api = Read("frontend", "src", "api", "accountApi.ts");
        var styles = Read("frontend", "src", "styles.css");

        Assert.Contains("role=\"switch\"", account, StringComparison.Ordinal);
        Assert.Contains("aria-checked={account.isLeaderboardAnonymous}", account, StringComparison.Ordinal);
        Assert.Contains("updateLeaderboardAnonymity(enabled)", account, StringComparison.Ordinal);
        Assert.Contains("公开榜单将显示匿名代号，管理账号仍可查看真实身份。", account, StringComparison.Ordinal);
        Assert.Contains("role=\"status\">已保存", account, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", account, StringComparison.Ordinal);
        Assert.DoesNotContain("排行榜匿名已开启", account, StringComparison.Ordinal);
        Assert.Contains("/api/account/leaderboard-anonymity", api, StringComparison.Ordinal);
        Assert.Contains("site-settings-switch", account, StringComparison.Ordinal);
        Assert.Contains(".site-settings-switch.active", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 640px)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicSeasonUi_DoesNotRenderRealUserNameField()
    {
        var page = Read("frontend", "src", "pages", "SeasonLeaderboardPage.tsx");
        Assert.Contains("entry.displayName", page, StringComparison.Ordinal);
        Assert.DoesNotContain("entry.userName", page, StringComparison.Ordinal);
        Assert.DoesNotContain("dangerouslySetInnerHTML", page, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
