namespace OnlineJudge.Tests.Leaderboards;

public sealed class OjUx06AContractTests
{
    public static TheoryData<string, string> RequiredContracts => new()
    {
        { "frontend/src/components/AppHeaderView.tsx", "renderIcon(\"problem\")}题目" },
        { "frontend/src/components/AppHeaderView.tsx", "renderIcon(\"challenge\")}挑战" },
        { "frontend/src/AppLayout.tsx", "hasPublicLeaderboard" },
        { "frontend/src/components/AppHeaderView.tsx", "management-menu" },
        { "frontend/src/components/AppHeaderView.tsx", "内容管理" },
        { "frontend/src/components/AppHeaderView.tsx", "竞赛管理" },
        { "frontend/src/components/AppHeaderView.tsx", "系统管理" },
        { "frontend/src/components/AppHeaderView.tsx", "榜单管理" },
        { "frontend/src/main.tsx", "allowedRoles={[3]}><LeaderboardSeasonHistoryPage" },
        { "frontend/src/pages/AccountCompetitionPage.tsx", "getCurrentSeasonPersonal" },
        { "frontend/src/pages/LeaderboardHomePage.tsx", "isRoot ? getCurrentSeasonLeaderboard()" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "hasGlobalBoard" },
        { "frontend/src/components/leaderboards/LeaderboardHomeView.tsx", "hasChallengeBoards" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "创建并关联挑战" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "选择当前结果" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "批量加入" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "批量移除" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "抢先奖励" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "运行时间奖励" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "内存奖励" },
        { "frontend/src/pages/AdminLeaderboardSeasonPage.tsx", "BenchmarkTable" },
        { "frontend/src/pages/AdminChallengeEditorPage.tsx", "seasonId" },
        { "OnlineJudge.Domain/Entities/LeaderboardSeasonBoard.cs", "LeaderboardSeasonBoardType" },
        { "OnlineJudge.Infrastructure/Persistence/Configurations/LeaderboardSeasonBoardConfiguration.cs", "CK_LeaderboardSeasonBoards_Target" },
        { "OnlineJudge.Infrastructure/Leaderboards/LeaderboardSeasonService.cs", "SynchronizeBoardsAsync" },
        { "OnlineJudge.Infrastructure/Leaderboards/LeaderboardSeasonService.cs", "Challenge leaderboard must stay within the season time range." },
        { "OnlineJudge.Infrastructure/Challenges/ChallengeService.cs", "CanViewSelectedLeaderboardAsync" },
        { "OnlineJudge.Api/Controllers/LeaderboardSeasonHistoryController.cs", "Authorize(Policy = \"RequireRoot\")" },
        { "OnlineJudge.Application/Leaderboards/Models/LeaderboardScoringRules.cs", "FirstCompletionBonusEnabled" },
        { "OnlineJudge.Application/Account/Requests/UpdateLeaderboardAnonymityRequest.cs", "IsLeaderboardAnonymous" },
        { "OnlineJudge.Infrastructure/Persistence/Migrations/20260828120113_AddEditableTestCasesAndResultSnapshots.cs", "SET \\\"UpdatedAt\\\" = \\\"CreatedAt\\\";" }
    };

    [Theory]
    [MemberData(nameof(RequiredContracts))]
    public void RequiredUxContract_IsPresent(string relativePath, string expected)
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains(expected, source, StringComparison.Ordinal);
    }

    [Fact]
    public void AnswererNavigation_PreservesRequiredOrder()
    {
        var source = Read("frontend/src/components/AppHeaderView.tsx");
        var problem = source.IndexOf("to=\"/problems\"", StringComparison.Ordinal);
        var challenge = source.IndexOf("to=\"/challenges\"", problem, StringComparison.Ordinal);
        var leaderboard = source.IndexOf("to=\"/leaderboards\"", StringComparison.Ordinal);
        var teams = source.IndexOf("to=\"/teams\"", StringComparison.Ordinal);
        var submissions = source.IndexOf("to=\"/submissions/my\"", StringComparison.Ordinal);
        var profile = source.IndexOf("to=\"/profile/me\"", StringComparison.Ordinal);
        Assert.True(problem < challenge && challenge < leaderboard && leaderboard < teams && teams < submissions && submissions < profile);
    }

    [Fact]
    public void AnswererCompetitionPage_RequestsOnlyPersonalHistory()
    {
        var source = Read("frontend/src/pages/AccountCompetitionPage.tsx");
        Assert.Contains("getSeasonPersonalHistory", source, StringComparison.Ordinal);
        Assert.Contains("我的历史赛季", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicLeaderboard_RemovesLiveBadgeAndHistoryEntry()
    {
        var home = Read("frontend/src/components/leaderboards/LeaderboardHomeView.tsx");
        var season = Read("frontend/src/pages/SeasonLeaderboardPage.tsx");
        Assert.DoesNotContain("实时榜单", home, StringComparison.Ordinal);
        Assert.DoesNotContain("/leaderboards/history", season, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(ProjectRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
