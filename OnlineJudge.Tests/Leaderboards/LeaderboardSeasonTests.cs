using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Leaderboards;

public class LeaderboardSeasonTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-28T12:00:00Z");

    [Fact]
    public async Task AcceptedNormalProblem_AddsFrozenSeasonBaseScore()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 120, 2048);

        var score = await fixture.Db.LeaderboardUserProblemScores.SingleAsync();
        Assert.Equal(100, score.BestBaseScore);
        Assert.True(score.IsFullScore);
        Assert.Equal(Now, score.FirstFullScoreAt);
        var board = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();
        Assert.Equal(120, Assert.Single(board.Value!.Entries).TotalScore);
    }

    [Fact]
    public async Task ChallengeAndNormalSubmission_ForSameProblem_DoNotDoubleCount()
    {
        await using var fixture = await Fixture.CreateAsync();

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 120, 2048);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 110, 2300);

        Assert.Single(fixture.Db.LeaderboardUserProblemScores);
        var board = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();
        Assert.Equal(120, Assert.Single(board.Value!.Entries).TotalScore);
    }

    [Fact]
    public async Task ScheduledSeason_DoesNotCreateScore()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(1));
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task FreezeTime_StopsNewScoresWithoutBlockingSubmissionFact()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
        Assert.Null((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Season);
    }

    [Fact]
    public async Task ProblemSetter_CannotControlSeasonLifecycle()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Time.Set(fixture.Season.FreezeAt);
        var service = fixture.SeasonService(fixture.ProblemSetter);

        Assert.True((await service.FreezeSeasonAsync(fixture.Season.Id)).IsFailure);
        Assert.True((await service.FinalizeSeasonAsync(fixture.Season.Id)).IsFailure);
        Assert.True((await service.ArchiveSeasonAsync(fixture.Season.Id)).IsFailure);
    }

    [Theory]
    [InlineData(UserRole.ProblemSetter)]
    [InlineData(UserRole.Root)]
    public async Task NonAnswerer_DoesNotCreateScore(UserRole role)
    {
        await using var fixture = await Fixture.CreateAsync();
        var user = fixture.NewUser(role.ToString(), role);
        await fixture.Db.SaveChangesAsync();

        await fixture.ApplyAcceptedAsync(user.Id, fixture.Problem.Id, 100, 1000);

        Assert.Empty(fixture.Db.LeaderboardUserProblemScores.Where(score => score.UserId == user.Id));
    }

    [Fact]
    public async Task BlacklistedUser_IsExcludedWithoutDeletingScore_AndReappearsAfterUnblacklist()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        fixture.Answerer.IsBlacklisted = true;
        await fixture.Db.SaveChangesAsync();

        Assert.Empty((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Entries);
        Assert.Single(fixture.Db.LeaderboardUserProblemScores);

        fixture.Answerer.IsBlacklisted = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Single((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Entries);
    }

    [Fact]
    public async Task RoleChange_ReevaluatesEligibilityWithoutDeletingFact()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);

        fixture.Answerer.Role = UserRole.ProblemSetter;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Entries);

        fixture.Answerer.Role = UserRole.Answerer;
        await fixture.Db.SaveChangesAsync();
        Assert.Single((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Entries);
    }

    [Fact]
    public async Task ScheduledSeasonProblem_RefreshesCurrentScore_AndFreezesItOnActivation()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1), addSeasonProblem: false);
        var service = fixture.RootSeasonService();
        var result = await service.AddProblemAsync(fixture.Season.Id, new AddLeaderboardSeasonProblemRequest { ProblemId = fixture.Problem.Id });
        Assert.True(result.IsSuccess);
        Assert.Equal(100, Assert.Single(result.Value!.Problems).BaseScore);

        fixture.TestCase.Score = 999;
        await fixture.Db.SaveChangesAsync();
        var refreshed = await service.GetSeasonsAsync();
        Assert.Equal(999, Assert.Single(Assert.Single(refreshed.Value!).Problems).BaseScore);
        Assert.Equal(100, (await fixture.Db.LeaderboardSeasonProblems.SingleAsync()).BaseScore);

        fixture.Time.Set(fixture.Season.StartAt);
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(999, (await fixture.Db.LeaderboardSeasonProblems.SingleAsync()).BaseScore);
        Assert.Equal(LeaderboardSeasonStatus.Active, fixture.Season.Status);
    }

    [Fact]
    public async Task SeasonProblemScore_ExcludesSoftDeletedTestCases()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1), addSeasonProblem: false);
        fixture.Db.TestCases.Add(new TestCase
        {
            Id = Guid.NewGuid(), ProblemId = fixture.Problem.Id, Input = "2", ExpectedOutput = "2", Score = 80,
            Visibility = TestCaseVisibility.Hidden, IsDeleted = true, DeletedAt = Now, CreatedAt = Now, UpdatedAt = Now
        });
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.RootSeasonService().AddProblemAsync(
            fixture.Season.Id, new AddLeaderboardSeasonProblemRequest { ProblemId = fixture.Problem.Id });

        Assert.True(result.IsSuccess);
        Assert.Equal(100, Assert.Single(result.Value!.Problems).BaseScore);
    }

    [Theory]
    [InlineData(LeaderboardSeasonStatus.Active)]
    [InlineData(LeaderboardSeasonStatus.Public)]
    [InlineData(LeaderboardSeasonStatus.Archived)]
    public async Task StartedSeasonProblem_KeepsFrozenBaseScore(LeaderboardSeasonStatus status)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Season.Status = status;
        fixture.TestCase.Score = 999;
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.RootSeasonService().GetSeasonsAsync();

        Assert.Equal(100, Assert.Single(Assert.Single(result.Value!).Problems).BaseScore);
    }

    [Fact]
    public async Task FirstFullScoreAt_IsStableAndPerformanceUsesOneSubmissionFact()
    {
        await using var fixture = await Fixture.CreateAsync();
        var firstSubmissionId = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 120, 1000);
        fixture.Time.Advance(TimeSpan.FromMinutes(5));
        var fasterSubmissionId = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 2000);

        var score = await fixture.Db.LeaderboardUserProblemScores.SingleAsync();
        Assert.Equal(Now, score.FirstFullScoreAt);
        Assert.Equal(Now, score.LastScoreImprovedAt);
        Assert.NotEqual(firstSubmissionId, fasterSubmissionId);
        Assert.Equal(fasterSubmissionId, score.BestPerformanceSubmissionId);
        Assert.Equal(100, score.BestRuntimeMs);
        Assert.Equal(2000, score.BestMemoryKb);
    }

    [Fact]
    public async Task AnonymousAlias_IsStableUniqueAndHiddenFromAnswerer()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        var other = fixture.NewUser("other", UserRole.Answerer, anonymous: true);
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        await fixture.ApplyAcceptedAsync(other.Id, fixture.Problem.Id, 110, 1100);

        var first = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();
        var second = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();
        Assert.All(first.Value!.Entries, entry =>
        {
            Assert.Null(entry.UserId);
            Assert.Null(entry.UserName);
            Assert.StartsWith("NODE-", entry.DisplayName);
        });
        Assert.Equal(2, first.Value.Entries.Select(entry => entry.Alias).Distinct().Count());
        Assert.Equal(first.Value.Entries.Select(entry => entry.Alias), second.Value!.Entries.Select(entry => entry.Alias));
    }

    [Fact]
    public async Task ProblemSetterAndRoot_CanAuditAnonymousRealIdentity()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);

        var setterBoard = await fixture.SeasonService(fixture.ProblemSetter).GetCurrentAuditLeaderboardAsync();
        var rootBoard = await fixture.RootSeasonService().GetCurrentAuditLeaderboardAsync();
        Assert.Equal(fixture.Answerer.UserName, Assert.Single(setterBoard.Value!.Entries).UserName);
        Assert.Equal(fixture.Answerer.Id, Assert.Single(rootBoard.Value!.Entries).UserId);
    }

    [Fact]
    public async Task FinalizeCanRebuildBeforeArchive_AndArchiveIsImmutable()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        fixture.Time.Set(fixture.Season.FreezeAt);
        var service = fixture.RootSeasonService();

        var first = await service.FinalizeSeasonAsync(fixture.Season.Id);
        Assert.Single(first.Value!.Entries);

        fixture.Answerer.IsBlacklisted = true;
        await fixture.Db.SaveChangesAsync();
        var rebuilt = await service.FinalizeSeasonAsync(fixture.Season.Id);
        Assert.Empty(rebuilt.Value!.Entries);

        fixture.Time.Set(fixture.Season.PublicUntil);
        Assert.True((await service.ArchiveSeasonAsync(fixture.Season.Id)).IsSuccess);
        var rejected = await service.FinalizeSeasonAsync(fixture.Season.Id);
        Assert.True(rejected.IsFailure);
        Assert.Equal("Archived leaderboard snapshots are immutable.", rejected.ErrorMessage);
    }

    [Fact]
    public async Task FinalizedSnapshots_DoNotChangeAfterUserOrProblemRename()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 1000);
        fixture.Time.Set(fixture.Season.FreezeAt);
        var service = fixture.RootSeasonService();
        var finalized = await service.FinalizeSeasonAsync(fixture.Season.Id);
        var entry = Assert.Single(finalized.Value!.Entries);
        var problem = Assert.Single(entry.ProblemScores);

        fixture.Answerer.UserName = "renamed-user";
        fixture.Problem.Title = "renamed-problem";
        await fixture.Db.SaveChangesAsync();
        var archive = await service.GetArchiveAsync(fixture.Season.Id);

        Assert.Equal("answerer", Assert.Single(archive.Value!.Entries).DisplayNameSnapshot);
        Assert.Equal("Season Problem", Assert.Single(Assert.Single(archive.Value.Entries).ProblemScores).ProblemTitleSnapshot);
        Assert.NotEqual(fixture.Answerer.UserName, entry.DisplayNameSnapshot);
        Assert.NotEqual(fixture.Problem.Title, problem.ProblemTitleSnapshot);
    }

    [Fact]
    public async Task DynamicTop10_ShiftsForBlacklistAndRestoresFromFrozenFacts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var users = Enumerable.Range(1, 11).Select(index => fixture.NewUser($"answerer-{index:D2}", UserRole.Answerer)).ToList();
        await fixture.Db.SaveChangesAsync();
        foreach (var user in users)
        {
            await fixture.ApplyAcceptedAsync(user.Id, fixture.Problem.Id, 100, 100);
            fixture.Time.Advance(TimeSpan.FromSeconds(1));
        }

        var before = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.Equal(20, before.Value!.Entries.Single(entry => entry.UserId == users[0].Id).TimeBonus);
        Assert.Equal(0, before.Value.Entries.Single(entry => entry.UserId == users[10].Id).TimeBonus);

        users[0].IsBlacklisted = true;
        await fixture.Db.SaveChangesAsync();
        var shifted = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.DoesNotContain(shifted.Value!.Entries, entry => entry.UserId == users[0].Id);
        Assert.Equal(2, shifted.Value.Entries.Single(entry => entry.UserId == users[10].Id).TimeBonus);

        users[0].IsBlacklisted = false;
        await fixture.Db.SaveChangesAsync();
        var restored = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.Equal(20, restored.Value!.Entries.Single(entry => entry.UserId == users[0].Id).TimeBonus);
        Assert.Equal(0, restored.Value.Entries.Single(entry => entry.UserId == users[10].Id).TimeBonus);

        users[4].IsBlacklisted = true;
        await fixture.Db.SaveChangesAsync();
        var middleShift = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.Equal(8, middleShift.Value!.Entries.Single(entry => entry.UserId == users[5].Id).TimeBonus);
        Assert.Equal(2, middleShift.Value.Entries.Single(entry => entry.UserId == users[10].Id).TimeBonus);

        users[4].IsBlacklisted = false;
        users[0].Role = UserRole.ProblemSetter;
        await fixture.Db.SaveChangesAsync();
        var roleShift = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.Equal(20, roleShift.Value!.Entries.Single(entry => entry.UserId == users[1].Id).TimeBonus);
        Assert.Equal(2, roleShift.Value.Entries.Single(entry => entry.UserId == users[10].Id).TimeBonus);
    }

    [Fact]
    public async Task BestPerformance_UsesSameSubmissionAndCanSwitchLanguage()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddBenchmarkAsync(JudgeLanguage.Cpp17, 100, 100);
        await fixture.AddBenchmarkAsync(JudgeLanguage.CSharp, 200, 200);

        var first = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 50, 101, JudgeLanguage.Cpp17);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var second = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 80, 50, JudgeLanguage.Cpp17);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var third = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, JudgeLanguage.CSharp);
        fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var lowerCandidate = await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 400, 400, JudgeLanguage.CSharp);

        var score = await fixture.Db.LeaderboardUserProblemScores.SingleAsync();
        Assert.NotEqual(first, score.BestPerformanceSubmissionId);
        Assert.NotEqual(second, score.BestPerformanceSubmissionId);
        Assert.NotEqual(lowerCandidate, score.BestPerformanceSubmissionId);
        Assert.Equal(third, score.BestPerformanceSubmissionId);
        Assert.Equal(JudgeLanguage.CSharp, score.BestPerformanceLanguage);
        Assert.Equal(100, score.BestRuntimeMs);
        Assert.Equal(100, score.BestMemoryKb);
        Assert.Equal(Now, score.FirstFullScoreAt);
        Assert.Equal(Now.AddMinutes(2), score.LastScoreImprovedAt);

        var entry = Assert.Single((await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id)).Value!.Entries);
        Assert.Equal(6, entry.RuntimeBonus);
        Assert.Equal(4, entry.MemoryBonus);
        Assert.Equal(130, entry.TotalProblemScore);
    }

    [Fact]
    public async Task ProblemLeaderboard_ProtectsAnonymousIdentityAndAllowsAudit()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);

        var publicEntry = Assert.Single((await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id)).Value!.Entries);
        Assert.Null(publicEntry.UserId);
        Assert.Null(publicEntry.UserName);
        Assert.StartsWith("NODE-", publicEntry.DisplayName);

        var auditEntry = Assert.Single((await fixture.SeasonService(fixture.ProblemSetter).GetCurrentProblemLeaderboardAsync(fixture.Problem.Id)).Value!.Entries);
        Assert.Equal(fixture.Answerer.Id, auditEntry.UserId);
        Assert.Equal(fixture.Answerer.UserName, auditEntry.UserName);
    }

    [Fact]
    public async Task Finalize_SnapshotsAllBonusAndPerformanceFacts()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddBenchmarkAsync(JudgeLanguage.Cpp17, 200, 200);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);

        var archive = await fixture.RootSeasonService().FinalizeSeasonAsync(fixture.Season.Id);
        var entry = Assert.Single(archive.Value!.Entries);
        var problem = Assert.Single(entry.ProblemScores);
        Assert.Equal(130, entry.FinalScore);
        Assert.Equal(20, entry.FinalTimeBonus);
        Assert.Equal(6, entry.FinalRuntimeBonus);
        Assert.Equal(4, entry.FinalMemoryBonus);
        Assert.Equal(1, problem.TimeRank);
        Assert.Equal(JudgeLanguage.Cpp17, problem.PerformanceLanguage);
        Assert.Equal(200, problem.RuntimeBaselineMs);
        Assert.Equal(200, problem.MemoryBaselineKb);
        Assert.Equal(130, problem.FinalProblemScore);
    }

    [Fact]
    public async Task Benchmark_IsScheduledOnlyAndValidatesAllowedLanguage()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1));
        fixture.Problem.AllowedLanguagesMask = 1;
        await fixture.Db.SaveChangesAsync();
        var service = fixture.SeasonService(fixture.ProblemSetter);
        var invalidLanguage = await service.UpdateProblemBenchmarkAsync(
            fixture.Season.Id, fixture.Problem.Id, JudgeLanguage.CSharp,
            new UpdateLeaderboardSeasonProblemBenchmarkRequest { RuntimeBaselineMs = 100, MemoryBaselineKb = 100 });
        Assert.True(invalidLanguage.IsFailure);

        var valid = await service.UpdateProblemBenchmarkAsync(
            fixture.Season.Id, fixture.Problem.Id, JudgeLanguage.Cpp17,
            new UpdateLeaderboardSeasonProblemBenchmarkRequest { RuntimeBaselineMs = 100, MemoryBaselineKb = 100 });
        Assert.True(valid.IsSuccess);
        Assert.Single(valid.Value!.Problems.Single().Benchmarks);

        fixture.Time.Set(fixture.Season.StartAt);
        var frozen = await service.UpdateProblemBenchmarkAsync(
            fixture.Season.Id, fixture.Problem.Id, JudgeLanguage.Cpp17,
            new UpdateLeaderboardSeasonProblemBenchmarkRequest { RuntimeBaselineMs = 200, MemoryBaselineKb = 200 });
        Assert.True(frozen.IsFailure);
    }

    [Fact]
    public async Task SeasonRuleSnapshot_DrivesScoringIndependentlyOfCurrentOptions()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Season.ScoringRulesJson = LeaderboardScoringRulesSerializer.Serialize(new LeaderboardScoringRules
        {
            TimeBonusPercentages = [.. Enumerable.Repeat(0, 10)],
            RuntimeBonusTiers = [new(100, 2)],
            MemoryBonusTiers = [new(100, 1)]
        });
        await fixture.AddBenchmarkAsync(JudgeLanguage.Cpp17, 100, 100);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);

        var entry = Assert.Single((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Entries);
        Assert.Equal(0, entry.TimeBonus);
        Assert.Equal(2, entry.RuntimeBonus);
        Assert.Equal(1, entry.MemoryBonus);
        Assert.Equal(103, entry.TotalScore);
    }

    [Fact]
    public async Task MultipleProblems_AggregateAllScoreComponentsAndSortByTotal()
    {
        await using var fixture = await Fixture.CreateAsync();
        var secondProblem = new Problem
        {
            Id = Guid.NewGuid(), Title = "Second Problem", Description = "test", InputDescription = "", OutputDescription = "",
            TimeLimitMs = 1000, MemoryLimitMb = 128, IsPublished = true, CreatedByUserId = fixture.ProblemSetter.Id,
            CreatedAt = Now, UpdatedAt = Now
        };
        secondProblem.TestCases.Add(new TestCase
        {
            Id = Guid.NewGuid(), ProblemId = secondProblem.Id, Input = "", ExpectedOutput = "", Score = 50,
            Visibility = TestCaseVisibility.Hidden, CreatedAt = Now, UpdatedAt = Now
        });
        fixture.Db.Problems.Add(secondProblem);
        fixture.Db.LeaderboardSeasonProblems.Add(new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(), SeasonId = fixture.Season.Id, ProblemId = secondProblem.Id, BaseScore = 50, CreatedAt = Now
        });
        var other = fixture.NewUser("other-total", UserRole.Answerer);
        await fixture.Db.SaveChangesAsync();

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, secondProblem.Id, 100, 100);
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.ApplyAcceptedAsync(other.Id, fixture.Problem.Id, 100, 100);

        var board = (await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!;
        var first = board.Entries[0];
        Assert.Equal(fixture.Answerer.Id, first.UserId);
        Assert.Equal(2, first.SolvedCount);
        Assert.Equal(150, first.BaseScore);
        Assert.Equal(30, first.TimeBonus);
        Assert.Equal(180, first.TotalScore);
    }

    [Fact]
    public async Task PublicReFinalize_ShiftsAndRestoresFinalBonuses()
    {
        await using var fixture = await Fixture.CreateAsync();
        var second = fixture.NewUser("second-final", UserRole.Answerer);
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Advance(TimeSpan.FromSeconds(1));
        await fixture.ApplyAcceptedAsync(second.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        var service = fixture.RootSeasonService();
        await service.FinalizeSeasonAsync(fixture.Season.Id);

        fixture.Answerer.IsBlacklisted = true;
        await fixture.Db.SaveChangesAsync();
        var shifted = await service.FinalizeSeasonAsync(fixture.Season.Id);
        Assert.Equal(second.Id, Assert.Single(shifted.Value!.Entries).UserId);
        Assert.Equal(20, Assert.Single(shifted.Value.Entries).FinalTimeBonus);

        fixture.Answerer.IsBlacklisted = false;
        await fixture.Db.SaveChangesAsync();
        var restored = await service.FinalizeSeasonAsync(fixture.Season.Id);
        Assert.Equal(fixture.Answerer.Id, restored.Value!.Entries.OrderBy(entry => entry.FinalRank).First().UserId);
        Assert.Equal(20, restored.Value.Entries.OrderBy(entry => entry.FinalRank).First().FinalTimeBonus);
        Assert.Equal(16, restored.Value.Entries.OrderBy(entry => entry.FinalRank).Last().FinalTimeBonus);
    }

    [Fact]
    public void Worker_UsesUnifiedSeasonScoreServiceAfterJudgeResult()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.JudgeWorker", "JudgeJobProcessor.cs"));
        Assert.Contains("ISeasonScoreService", source, StringComparison.Ordinal);
        Assert.Contains("ApplySubmissionResultAsync", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "ApplySubmissionResultAsync"));
    }

    [Fact]
    public async Task Lifecycle_ReconcileAutomaticallyActivatesFinalizesAndArchives()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(1));
        var service = fixture.RootSeasonService();

        fixture.Time.Set(fixture.Season.StartAt);
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(LeaderboardSeasonStatus.Active, fixture.Season.Status);
        Assert.NotNull(fixture.Season.ActivatedAt);

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(LeaderboardSeasonStatus.Public, fixture.Season.Status);
        Assert.NotNull(fixture.Season.FrozenAt);
        Assert.NotNull(fixture.Season.FinalizedAt);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries);

        fixture.Time.Set(fixture.Season.PublicUntil);
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(LeaderboardSeasonStatus.Archived, fixture.Season.Status);
        Assert.False(fixture.Season.IsCurrent);
        Assert.NotNull(fixture.Season.ArchivedAt);
    }

    [Fact]
    public async Task SubmissionCreatedBeforeStart_IsNeverImportedAfterActivation()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(5));
        var createdAt = fixture.Time.GetUtcNow();
        fixture.Time.Set(fixture.Season.StartAt.AddMinutes(1));

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: createdAt);

        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task SubmissionCreatedBeforeFreeze_CanFinishAfterFreezeAndRefreshPublicArchive()
    {
        await using var fixture = await Fixture.CreateAsync();
        var createdAt = fixture.Season.FreezeAt.AddSeconds(-1);
        fixture.Time.Set(fixture.Season.FreezeAt);
        var service = fixture.RootSeasonService();
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(LeaderboardSeasonStatus.Public, fixture.Season.Status);

        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: createdAt);
        Assert.True(fixture.LastScoreResult!.RequiresArchiveRefresh);
        await service.RefreshPublicSeasonAsync(fixture.Season.Id);

        Assert.Equal(createdAt, (await fixture.Db.LeaderboardUserProblemScores.SingleAsync()).FirstFullScoreAt);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries);
    }

    [Fact]
    public async Task SubmissionCreatedAtFreeze_IsExcluded()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Time.Set(fixture.Season.FreezeAt.AddMinutes(1));
        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: fixture.Season.FreezeAt);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task ManualFreeze_RecordsActualCutoffWithoutChangingPlan()
    {
        await using var fixture = await Fixture.CreateAsync();
        var plannedFreeze = fixture.Season.FreezeAt;
        var result = await fixture.RootSeasonService().FreezeSeasonAsync(fixture.Season.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, fixture.Season.ManuallyFrozenAt);
        Assert.Equal(plannedFreeze, fixture.Season.FreezeAt);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: Now);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task RankSnapshot_DeduplicatesUnchangedRankAndFeedsPersonalStatistics()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.RootSeasonService();
        await service.ReconcileCurrentSeasonAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);

        fixture.Time.Advance(TimeSpan.FromHours(1));
        await service.ReconcileCurrentSeasonAsync();
        await service.RefreshPublicSeasonAsync(fixture.Season.Id);

        Assert.Single(fixture.Db.LeaderboardSeasonRankSnapshots);
        var personal = await fixture.PublicSeasonService().GetCurrentPersonalAsync();
        Assert.Equal(1, personal.Value!.CurrentRank);
        Assert.Equal(1, personal.Value.BestRank);
        Assert.Equal(1, personal.Value.FirstPlaceProblemCount);
    }

    [Fact]
    public async Task ArchivedHistory_UsesAnonymousSnapshotAndRemainsAvailable()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        var service = fixture.RootSeasonService();
        fixture.Time.Set(fixture.Season.FreezeAt);
        await service.ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);
        await service.ReconcileCurrentSeasonAsync();

        fixture.Answerer.IsLeaderboardAnonymous = false;
        fixture.Answerer.UserName = "renamed-after-archive";
        await fixture.Db.SaveChangesAsync();
        var history = await fixture.PublicSeasonService().GetHistoryAsync(fixture.Season.Id);

        var entry = Assert.Single(history.Value!.Entries);
        Assert.StartsWith("NODE-", entry.DisplayNameSnapshot);
        Assert.NotEqual(fixture.Answerer.UserName, entry.DisplayNameSnapshot);
    }

    [Fact]
    public async Task SubmissionCreatedExactlyAtStart_IsEligible()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(5));
        fixture.Time.Set(fixture.Season.StartAt.AddSeconds(10));

        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 100, 100,
            createdAt: fixture.Season.StartAt, finishedAt: fixture.Time.GetUtcNow());

        Assert.Single(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task SubmissionCreatedAfterFreeze_IsExcluded()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Time.Set(fixture.Season.FreezeAt.AddSeconds(10));

        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 100, 100,
            createdAt: fixture.Season.FreezeAt.AddTicks(1));

        Assert.False(fixture.LastScoreResult!.Applied);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task SubmissionCreatedBeforeManualFreeze_RemainsEligibleWhenFinishedAfterward()
    {
        await using var fixture = await Fixture.CreateAsync();
        var createdAt = Now.AddSeconds(-1);
        Assert.True((await fixture.RootSeasonService().FreezeSeasonAsync(fixture.Season.Id)).IsSuccess);
        fixture.Time.Advance(TimeSpan.FromSeconds(5));

        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 100, 100,
            createdAt: createdAt, finishedAt: fixture.Time.GetUtcNow());
        await fixture.RootSeasonService().RefreshPublicSeasonAsync(fixture.Season.Id);

        Assert.True(fixture.LastScoreResult!.Applied);
        Assert.Equal(createdAt, (await fixture.Db.LeaderboardUserProblemScores.SingleAsync()).FirstFullScoreAt);
        Assert.Equal(fixture.Answerer.Id, Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries).UserId);
    }

    [Fact]
    public async Task FirstFullTimeRank_UsesCreatedAtInsteadOfJudgeCompletionOrder()
    {
        await using var fixture = await Fixture.CreateAsync();
        var secondUser = fixture.NewUser("second", UserRole.Answerer);
        await fixture.Db.SaveChangesAsync();

        await fixture.ApplyAcceptedAsync(
            secondUser.Id, fixture.Problem.Id, 100, 100,
            createdAt: Now.AddSeconds(1), finishedAt: Now.AddSeconds(2));
        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 100, 100,
            createdAt: Now, finishedAt: Now.AddSeconds(10));

        var board = await fixture.PublicSeasonService().GetCurrentProblemLeaderboardAsync(fixture.Problem.Id);
        Assert.Equal(fixture.Answerer.Id, board.Value!.Entries[0].UserId);
        Assert.Equal(1, board.Value.Entries[0].TimeRank);
        Assert.Equal(secondUser.Id, board.Value.Entries[1].UserId);
        Assert.Equal(2, board.Value.Entries[1].TimeRank);
    }

    [Fact]
    public async Task PublicLateInvalidSubmission_DoesNotChangeScoreOrArchive()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        var before = JsonSerializer.Serialize((await fixture.RootSeasonService().GetArchiveAsync(fixture.Season.Id)).Value);
        var scoreCount = await fixture.Db.LeaderboardUserProblemScores.CountAsync();

        fixture.Time.Advance(TimeSpan.FromSeconds(5));
        await fixture.ApplyAcceptedAsync(
            fixture.Answerer.Id, fixture.Problem.Id, 1, 1,
            createdAt: fixture.Season.FreezeAt, finishedAt: fixture.Time.GetUtcNow());

        Assert.False(fixture.LastScoreResult!.Applied);
        Assert.Equal(scoreCount, await fixture.Db.LeaderboardUserProblemScores.CountAsync());
        Assert.Equal(before, JsonSerializer.Serialize((await fixture.RootSeasonService().GetArchiveAsync(fixture.Season.Id)).Value));
    }

    [Fact]
    public async Task LifecycleAndFinalize_AreIdempotentWithoutDuplicateSnapshots()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(5));
        var service = fixture.RootSeasonService();
        await service.ReconcileCurrentSeasonAsync();
        await service.ReconcileCurrentSeasonAsync();
        Assert.Equal(LeaderboardSeasonStatus.Scheduled, fixture.Season.Status);

        fixture.Time.Set(fixture.Season.StartAt);
        await service.ReconcileCurrentSeasonAsync();
        await service.ReconcileCurrentSeasonAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await service.ReconcileCurrentSeasonAsync();
        await service.RefreshPublicSeasonAsync(fixture.Season.Id);
        await service.RefreshPublicSeasonAsync(fixture.Season.Id);

        Assert.Equal(LeaderboardSeasonStatus.Public, fixture.Season.Status);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveProblemScores);
        Assert.Single(fixture.Db.LeaderboardSeasonRankSnapshots);

        fixture.Time.Set(fixture.Season.PublicUntil);
        await service.ReconcileCurrentSeasonAsync();
        await service.ReconcileCurrentSeasonAsync();
        Assert.True((await service.ArchiveSeasonAsync(fixture.Season.Id)).IsSuccess);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveProblemScores);
    }

    [Fact]
    public async Task FinalizeFailure_LeavesFrozenWithoutPartialArchive_AndCanRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        Assert.True((await fixture.RootSeasonService().FreezeSeasonAsync(fixture.Season.Id)).IsSuccess);

        var interceptor = new FailingSaveChangesInterceptor(context =>
            context.ChangeTracker.Entries<LeaderboardSeason>().Any(entry => entry.Entity.Status == LeaderboardSeasonStatus.Public));
        await using (var failingDb = fixture.CreateContext(interceptor))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.SeasonService(fixture.Root, failingDb).ReconcileCurrentSeasonAsync());
        }

        await using (var verifyDb = fixture.CreateContext())
        {
            Assert.Equal(LeaderboardSeasonStatus.Frozen, (await verifyDb.LeaderboardSeasons.SingleAsync()).Status);
            Assert.Empty(verifyDb.LeaderboardSeasonArchiveEntries);
            Assert.Empty(verifyDb.LeaderboardSeasonArchiveProblemScores);
        }

        await using (var retryDb = fixture.CreateContext())
        {
            await fixture.SeasonService(fixture.Root, retryDb).ReconcileCurrentSeasonAsync();
            Assert.Equal(LeaderboardSeasonStatus.Public, (await retryDb.LeaderboardSeasons.SingleAsync()).Status);
            Assert.Single(retryDb.LeaderboardSeasonArchiveEntries);
            Assert.Single(retryDb.LeaderboardSeasonArchiveProblemScores);
        }
    }

    [Fact]
    public async Task ArchiveFailure_LeavesPublicSnapshotReadable_AndCanRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);

        var interceptor = new FailingSaveChangesInterceptor(context =>
            context.ChangeTracker.Entries<LeaderboardSeason>().Any(entry => entry.Entity.Status == LeaderboardSeasonStatus.Archived));
        await using (var failingDb = fixture.CreateContext(interceptor))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.SeasonService(fixture.Root, failingDb).ReconcileCurrentSeasonAsync());
        }

        await using (var verifyDb = fixture.CreateContext())
        {
            Assert.Equal(LeaderboardSeasonStatus.Public, (await verifyDb.LeaderboardSeasons.SingleAsync()).Status);
            Assert.Single((await fixture.SeasonService(fixture.Answerer, verifyDb).GetCurrentLeaderboardAsync()).Value!.Entries);
        }

        await using (var retryDb = fixture.CreateContext())
        {
            await fixture.SeasonService(fixture.Root, retryDb).ReconcileCurrentSeasonAsync();
            Assert.Equal(LeaderboardSeasonStatus.Archived, (await retryDb.LeaderboardSeasons.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task ArchivedSnapshot_IsUnaffectedByAllLiveMutableFields()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        await fixture.AddBenchmarkAsync(JudgeLanguage.Cpp17, 100, 100);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 50, 50);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        var before = JsonSerializer.Serialize((await fixture.RootSeasonService().GetArchiveAsync(fixture.Season.Id)).Value);

        fixture.Answerer.UserName = "changed";
        fixture.Answerer.IsLeaderboardAnonymous = false;
        fixture.Answerer.Role = UserRole.ProblemSetter;
        fixture.Answerer.IsBlacklisted = true;
        fixture.Problem.Title = "changed problem";
        fixture.Season.ScoringRulesJson = "{}";
        var benchmark = await fixture.Db.LeaderboardSeasonProblemBenchmarks.SingleAsync();
        benchmark.RuntimeBaselineMs = 999;
        benchmark.MemoryBaselineKb = 999;
        await fixture.Db.SaveChangesAsync();

        var after = JsonSerializer.Serialize((await fixture.RootSeasonService().GetArchiveAsync(fixture.Season.Id)).Value);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task AnonymousArchive_HidesIdentityPubliclyButPreservesAuditSnapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        var publicViewer = fixture.NewUser("viewer", UserRole.Answerer);
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();

        fixture.Answerer.IsLeaderboardAnonymous = false;
        await fixture.Db.SaveChangesAsync();
        var publicEntry = Assert.Single((await fixture.SeasonService(publicViewer).GetHistoryAsync(fixture.Season.Id)).Value!.Entries);
        var auditEntry = Assert.Single((await fixture.SeasonService(fixture.ProblemSetter).GetHistoryAsync(fixture.Season.Id)).Value!.Entries);

        Assert.Null(publicEntry.UserId);
        Assert.StartsWith("NODE-", publicEntry.DisplayNameSnapshot);
        Assert.Equal(fixture.Answerer.Id, auditEntry.UserId);
        Assert.Equal("answerer", auditEntry.DisplayNameSnapshot);
    }

    [Fact]
    public async Task ArchiveClearsOnlyCurrentPointerAndPreservesAllSeasonFacts()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();

        Assert.Null((await fixture.PublicSeasonService().GetCurrentLeaderboardAsync()).Value!.Season);
        Assert.Single(fixture.Db.LeaderboardUserProblemScores);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveEntries);
        Assert.Single(fixture.Db.LeaderboardSeasonArchiveProblemScores);
        Assert.Single(fixture.Db.LeaderboardSeasonRankSnapshots);
        Assert.Single((await fixture.RootSeasonService().GetArchiveAsync(fixture.Season.Id)).Value!.Entries);
    }

    [Fact]
    public async Task NextSeasonStartsWithIndependentScoreAliasAndFirstFullFacts()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Answerer.IsLeaderboardAnonymous = true;
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Set(fixture.Season.PublicUntil);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        var seasonAAlias = Assert.Single(fixture.Db.LeaderboardSeasonAliases).Alias;

        var start = fixture.Time.GetUtcNow().AddMinutes(1);
        var created = await fixture.RootSeasonService().CreateSeasonAsync(new CreateLeaderboardSeasonRequest
        {
            Name = "Season 2", StartAt = start, FreezeAt = start.AddHours(1), PublicUntil = start.AddHours(2)
        });
        Assert.True(created.IsSuccess);
        var seasonB = created.Value!;
        Assert.True((await fixture.RootSeasonService().AddProblemAsync(
            seasonB.Id, new AddLeaderboardSeasonProblemRequest { ProblemId = fixture.Problem.Id })).IsSuccess);
        Assert.Empty(fixture.Db.LeaderboardUserProblemScores.Where(score => score.SeasonId == seasonB.Id));

        fixture.Time.Set(start);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: start);
        var board = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();
        var seasonBAlias = Assert.Single(board.Value!.Entries).Alias;

        Assert.NotEqual(seasonAAlias, seasonBAlias);
        Assert.Single(fixture.Db.LeaderboardUserProblemScores.Where(score => score.SeasonId == fixture.Season.Id));
        var seasonBScore = Assert.Single(fixture.Db.LeaderboardUserProblemScores.Where(score => score.SeasonId == seasonB.Id));
        Assert.Equal(start, seasonBScore.FirstFullScoreAt);
    }

    [Fact]
    public async Task RankSnapshot_InsertsWhenScoreChangesWithoutRankChange()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddBenchmarkAsync(JudgeLanguage.Cpp17, 100, 100);
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        fixture.Time.Advance(TimeSpan.FromMinutes(30));
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 50, 50);
        fixture.Time.Advance(TimeSpan.FromMinutes(30));
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();

        var snapshots = await fixture.Db.LeaderboardSeasonRankSnapshots.OrderBy(snapshot => snapshot.RecordedAt).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.All(snapshots, snapshot => Assert.Equal(1, snapshot.Rank));
        Assert.NotEqual(snapshots[0].TotalScore, snapshots[1].TotalScore);
    }

    [Fact]
    public async Task RankSnapshot_InsertsWhenRankChangesWithoutScoreChange()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        var initial = await fixture.Db.LeaderboardSeasonRankSnapshots.SingleAsync(snapshot => snapshot.UserId == fixture.Answerer.Id);

        var secondProblem = new Problem
        {
            Id = Guid.NewGuid(), Title = "Higher Score", Description = "test", InputDescription = "", OutputDescription = "",
            TimeLimitMs = 1000, MemoryLimitMb = 128, IsPublished = true, CreatedByUserId = fixture.ProblemSetter.Id,
            CreatedAt = Now, UpdatedAt = Now
        };
        fixture.Db.Problems.Add(secondProblem);
        fixture.Db.LeaderboardSeasonProblems.Add(new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(), SeasonId = fixture.Season.Id, ProblemId = secondProblem.Id, BaseScore = 200, CreatedAt = Now
        });
        var other = fixture.NewUser("higher", UserRole.Answerer);
        await fixture.Db.SaveChangesAsync();
        await fixture.ApplyAcceptedAsync(other.Id, secondProblem.Id, 100, 100);
        fixture.Time.Advance(TimeSpan.FromHours(1));
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();

        var answererSnapshots = await fixture.Db.LeaderboardSeasonRankSnapshots
            .Where(snapshot => snapshot.UserId == fixture.Answerer.Id).OrderBy(snapshot => snapshot.RecordedAt).ToListAsync();
        Assert.Equal([1, 2], answererSnapshots.Select(snapshot => snapshot.Rank));
        Assert.All(answererSnapshots, snapshot => Assert.Equal(initial.TotalScore, snapshot.TotalScore));
    }

    [Fact]
    public async Task PersonalBestRankAndRankChange_UseSeasonSnapshots()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (var index = 0; index < 6; index++)
        {
            var user = fixture.NewUser($"ahead-{index}", UserRole.Answerer);
            await fixture.Db.SaveChangesAsync();
            await fixture.ApplyAcceptedAsync(user.Id, fixture.Problem.Id, 100, 100, createdAt: Now.AddMinutes(-10 + index));
        }
        await fixture.ApplyAcceptedAsync(fixture.Answerer.Id, fixture.Problem.Id, 100, 100, createdAt: Now);
        fixture.Db.LeaderboardSeasonRankSnapshots.Add(new LeaderboardSeasonRankSnapshot
        {
            Id = Guid.NewGuid(), SeasonId = fixture.Season.Id, UserId = fixture.Answerer.Id,
            Rank = 10, TotalScore = 0, RecordedAt = Now.AddMinutes(-1)
        });
        await fixture.Db.SaveChangesAsync();

        var personal = await fixture.PublicSeasonService().GetCurrentPersonalAsync();
        Assert.Equal(7, personal.Value!.CurrentRank);
        Assert.Equal(7, personal.Value.BestRank);
        Assert.Equal(3, personal.Value.RankChange);
    }

    [Fact]
    public void LifecycleWorker_RetriesAfterPollAndHandlesHostCancellation()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.Api", "Services", "LeaderboardSeasonLifecycleWorker.cs"));
        Assert.Contains("PeriodicTimer", source, StringComparison.Ordinal);
        Assert.Contains("catch (Exception exception)", source, StringComparison.Ordinal);
        Assert.Contains("WaitForNextTickAsync(stoppingToken)", source, StringComparison.Ordinal);
        Assert.Contains("OperationCanceledException", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Task.Delay", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublicSummary_UsesEffectiveLifecycleStatusWithoutCompetitionData()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddMinutes(1));
        var service = fixture.PublicSeasonService();

        var scheduled = (await service.GetCurrentPublicSummaryAsync()).Value!.Season!;
        Assert.Equal(LeaderboardSeasonStatus.Scheduled, scheduled.Status);
        Assert.Equal(fixture.Season.Name, scheduled.Name);

        fixture.Time.Set(fixture.Season.StartAt);
        var active = (await service.GetCurrentPublicSummaryAsync()).Value!.Season!;
        Assert.Equal(LeaderboardSeasonStatus.Active, active.Status);

        fixture.Time.Set(fixture.Season.FreezeAt);
        await fixture.RootSeasonService().ReconcileCurrentSeasonAsync();
        var published = (await service.GetCurrentPublicSummaryAsync()).Value!.Season!;
        Assert.Equal(LeaderboardSeasonStatus.Public, published.Status);
        Assert.Equal(LeaderboardSeasonBoardType.Global, Assert.Single(published.Boards).BoardType);

        var json = JsonSerializer.Serialize(published);
        Assert.DoesNotContain("Problem", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Entry", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicSummary_ReturnsEmptyWhenThereIsNoCurrentSeason()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Season.IsCurrent = false;
        await fixture.Db.SaveChangesAsync();

        var summary = await fixture.PublicSeasonService().GetCurrentPublicSummaryAsync();

        Assert.True(summary.IsSuccess);
        Assert.Null(summary.Value!.Season);
    }

    [Fact]
    public async Task PublicLeaderboard_IsHiddenWhenGlobalBoardIsNotSelected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Db.LeaderboardSeasonBoards.RemoveRange(fixture.Db.LeaderboardSeasonBoards);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.PublicSeasonService().GetCurrentLeaderboardAsync();

        Assert.Null(result.Value!.Season);
        Assert.Empty(result.Value.Entries);
    }

    [Fact]
    public async Task ScheduledSeason_RejectsChallengeBoardOutsideSeasonRange()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1));
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(), Title = "Outside", Description = "test", StartAt = fixture.Season.StartAt,
            EndAt = fixture.Season.FreezeAt.AddMinutes(1), CreatedByUserId = fixture.Root.Id,
            IsPublished = true, CreatedAt = Now, UpdatedAt = Now
        };
        fixture.Db.Challenges.Add(challenge);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.RootSeasonService().UpdateSeasonAsync(fixture.Season.Id, new UpdateLeaderboardSeasonRequest
        {
            Name = fixture.Season.Name, StartAt = fixture.Season.StartAt, FreezeAt = fixture.Season.FreezeAt,
            PublicUntil = fixture.Season.PublicUntil, IncludeGlobalBoard = true, ChallengeIds = [challenge.Id]
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Challenge leaderboard must stay within the season time range.", result.ErrorMessage);
    }

    [Fact]
    public async Task UnlinkChallengeBoard_DoesNotDeleteChallenge()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1));
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(), Title = "Linked", Description = "test", StartAt = fixture.Season.StartAt,
            EndAt = fixture.Season.FreezeAt, CreatedByUserId = fixture.Root.Id, IsPublished = true,
            CreatedAt = Now, UpdatedAt = Now
        };
        fixture.Db.Challenges.Add(challenge);
        await fixture.Db.SaveChangesAsync();
        await using var context = fixture.CreateContext();
        var service = fixture.SeasonService(fixture.Root, context);
        var request = new UpdateLeaderboardSeasonRequest
        {
            Name = fixture.Season.Name, StartAt = fixture.Season.StartAt, FreezeAt = fixture.Season.FreezeAt,
            PublicUntil = fixture.Season.PublicUntil, IncludeGlobalBoard = true, ChallengeIds = [challenge.Id]
        };
        Assert.True((await service.UpdateSeasonAsync(fixture.Season.Id, request)).IsSuccess);

        request.ChallengeIds = [];
        Assert.True((await service.UpdateSeasonAsync(fixture.Season.Id, request)).IsSuccess);

        Assert.True(await fixture.Db.Challenges.AnyAsync(item => item.Id == challenge.Id));
        Assert.False(await fixture.Db.LeaderboardSeasonBoards.AnyAsync(board => board.ChallengeId == challenge.Id));
    }

    private static int Count(string value, string token) => (value.Length - value.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString();
        private readonly InMemoryDatabaseRoot databaseRoot = new();

        private Fixture()
        {
            Db = CreateContext();
            Time = new MutableTimeProvider(Now);
            Answerer = NewUser("answerer", UserRole.Answerer);
            ProblemSetter = NewUser("setter", UserRole.ProblemSetter);
            Root = NewUser("root", UserRole.Root);
            Problem = new Problem
            {
                Id = Guid.NewGuid(), Title = "Season Problem", Description = "test", InputDescription = "", OutputDescription = "",
                TimeLimitMs = 1000, MemoryLimitMb = 128, IsPublished = true, CreatedByUserId = ProblemSetter.Id,
                CreatedAt = Now, UpdatedAt = Now
            };
            TestCase = new TestCase
            {
                Id = Guid.NewGuid(), ProblemId = Problem.Id, Input = "1", ExpectedOutput = "1", Score = 100,
                Visibility = TestCaseVisibility.Hidden, CreatedAt = Now, UpdatedAt = Now
            };
            Problem.TestCases.Add(TestCase);
            Db.AddRange(Answerer, ProblemSetter, Root, Problem);
        }

        public OnlineJudgeDbContext Db { get; }
        public MutableTimeProvider Time { get; }
        public User Answerer { get; }
        public User ProblemSetter { get; }
        public User Root { get; }
        public Problem Problem { get; }
        public TestCase TestCase { get; }
        public LeaderboardSeason Season { get; private set; } = null!;
        public OnlineJudge.Application.Leaderboards.Services.SeasonScoreApplyResult? LastScoreResult { get; private set; }

        public static async Task<Fixture> CreateAsync(DateTimeOffset? seasonStart = null, bool addSeasonProblem = true)
        {
            var fixture = new Fixture();
            var start = seasonStart ?? Now.AddHours(-1);
            var freeze = start >= Now ? start.AddHours(1) : Now.AddHours(1);
            fixture.Season = new LeaderboardSeason
            {
                Id = Guid.NewGuid(), Name = "Season 1", StartAt = start, FreezeAt = freeze, PublicUntil = freeze.AddHours(1),
                Status = LeaderboardSeasonStatus.Scheduled, IsCurrent = true, CreatedByUserId = fixture.Root.Id, CreatedAt = Now, UpdatedAt = Now
            };
            fixture.Db.LeaderboardSeasons.Add(fixture.Season);
            fixture.Db.LeaderboardSeasonBoards.Add(new LeaderboardSeasonBoard
            {
                Id = Guid.NewGuid(), SeasonId = fixture.Season.Id,
                BoardType = LeaderboardSeasonBoardType.Global, CreatedAt = Now
            });
            if (addSeasonProblem)
            {
                fixture.Db.LeaderboardSeasonProblems.Add(new LeaderboardSeasonProblem
                {
                    Id = Guid.NewGuid(), SeasonId = fixture.Season.Id, ProblemId = fixture.Problem.Id, BaseScore = 100, CreatedAt = Now
                });
            }
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public User NewUser(string name, UserRole role, bool anonymous = false)
        {
            var user = new User
            {
                Id = Guid.NewGuid(), UserName = name, Email = $"{name}-{Guid.NewGuid():N}@example.test", PasswordHash = "test",
                Role = role, IsLeaderboardAnonymous = anonymous, CreatedAt = Now, UpdatedAt = Now
            };
            Db.Users.Add(user);
            return user;
        }

        public async Task AddBenchmarkAsync(JudgeLanguage language, int runtime, int memory)
        {
            var seasonProblem = await Db.LeaderboardSeasonProblems.SingleAsync(item => item.ProblemId == Problem.Id);
            Db.LeaderboardSeasonProblemBenchmarks.Add(new LeaderboardSeasonProblemBenchmark
            {
                Id = Guid.NewGuid(), SeasonProblemId = seasonProblem.Id, Language = language,
                RuntimeBaselineMs = runtime, MemoryBaselineKb = memory, CreatedAt = Time.GetUtcNow(), UpdatedAt = Time.GetUtcNow()
            });
            await Db.SaveChangesAsync();
        }

        public async Task<Guid> ApplyAcceptedAsync(
            Guid userId,
            Guid problemId,
            int? runtime,
            int? memory,
            JudgeLanguage language = JudgeLanguage.Cpp17,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? finishedAt = null)
        {
            var submission = new Submission
            {
                Id = Guid.NewGuid(), UserId = userId, ProblemId = problemId, SourceCode = "test", Status = JudgeStatus.Accepted,
                Language = language, TimeUsedMs = runtime, MemoryUsedKb = memory,
                CreatedAt = createdAt ?? Time.GetUtcNow(), FinishedAt = finishedAt ?? Time.GetUtcNow()
            };
            Db.Submissions.Add(submission);
            LastScoreResult = await new SeasonScoreService(Db, Time).ApplySubmissionResultAsync(new SeasonSubmissionResult(
                submission.Id, problemId, userId, submission.Language!.Value, JudgeStatus.Accepted, runtime, memory, submission.CreatedAt, submission.FinishedAt.Value));
            await Db.SaveChangesAsync();
            return submission.Id;
        }

        public LeaderboardSeasonService PublicSeasonService() => SeasonService(Answerer);
        public LeaderboardSeasonService RootSeasonService() => SeasonService(Root);
        public OnlineJudgeDbContext CreateContext(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
                .UseInMemoryDatabase(databaseName, databaseRoot)
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
            if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
            return new OnlineJudgeDbContext(builder.Options);
        }

        public LeaderboardSeasonService SeasonService(User user, OnlineJudgeDbContext? dbContext = null)
        {
            var context = dbContext ?? Db;
            var current = new TestCurrentUser(user);
            var identity = new LeaderboardIdentityService(context, current, Time);
            return new LeaderboardSeasonService(context, current, Time, identity);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
    }

    private sealed class FailingSaveChangesInterceptor(Func<DbContext, bool> shouldFail) : SaveChangesInterceptor
    {
        private bool hasFailed;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!hasFailed && eventData.Context is { } context && shouldFail(context))
            {
                hasFailed = true;
                throw new InvalidOperationException("Injected persistence failure.");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class TestCurrentUser(User user) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => user.Id;
        public string? UserName => user.UserName;
        public UserRole? Role => user.Role;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan duration) => value += duration;
        public void Set(DateTimeOffset time) => value = time;
    }
}
