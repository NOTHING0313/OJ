namespace OnlineJudge.Tests.Leaderboards;

public sealed class SeasonLeaderboardUx06CContractTests
{
    public static TheoryData<string, string> RequiredContracts => new()
    {
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "season-overview" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "榜单设置" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "挑战管理" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "题目管理" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "奖励与性能基准" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "openSections" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "搜索题目" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "已加入" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "未加入" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "批量加入" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "批量移除" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "题目总分" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "创建并关联挑战" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "/admin/challenges/${challenge.id}/edit" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "榜单关联已移除，挑战本身保持不变" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "未启用额外性能奖励" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "BenchmarkModal" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "runtimeBonusEnabled && <th>运行基准</th>" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "memoryBonusEnabled && <th>内存基准</th>" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "enabledChallengeIds" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "enabledChallenges.map" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "slice(0, 3)" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "暂无成绩" },
        { "frontend/src/pages/TeamPage.tsx", "team-audit-summary" },
        { "frontend/src/pages/TeamPage.tsx", "Repository Host" }
    };

    [Theory]
    [MemberData(nameof(RequiredContracts))]
    public void RequiredUxContract_IsPresent(string relativePath, string expected)
    {
        Assert.Contains(expected, Read(relativePath), StringComparison.Ordinal);
    }

    [Fact]
    public void ManualBaseScoreEditing_IsRemovedFromApiAndUi()
    {
        var controller = Read("OnlineJudge.Api/Controllers/AdminLeaderboardSeasonsController.cs");
        var contract = Read("OnlineJudge.Application/Leaderboards/Services/ILeaderboardSeasonService.cs");
        var api = Read("frontend/src/api/leaderboardsApi.ts");
        var page = Read("frontend/src/pages/AdminLeaderboardSeasonPage.tsx");

        Assert.DoesNotContain("UpdateLeaderboardSeasonProblemRequest", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateProblemAsync", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("updateLeaderboardSeasonProblem(", api, StringComparison.Ordinal);
        Assert.DoesNotContain("season-base-score", page, StringComparison.Ordinal);
        Assert.DoesNotContain("保存分数", page, StringComparison.Ordinal);
    }

    [Fact]
    public void BoardCenter_FiltersToEnabledBoards_AndKeepsManagementAuthorized()
    {
        var container = Read("frontend/src/pages/LeaderboardHomePage.tsx");
        var view = Read("frontend/src/components/leaderboards/LeaderboardHomeView.tsx");
        Assert.Contains("enabledChallengeIds.has", view, StringComparison.Ordinal);
        Assert.Contains("canManageContent(currentUser?.role)", container, StringComparison.Ordinal);
        Assert.Contains("榜单管理", view, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(ProjectRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
