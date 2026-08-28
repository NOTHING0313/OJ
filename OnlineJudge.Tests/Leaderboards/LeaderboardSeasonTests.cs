using Microsoft.EntityFrameworkCore;
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
        Assert.Equal(100, Assert.Single(board.Value!.Entries).TotalScore);
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
        Assert.Equal(100, Assert.Single(board.Value!.Entries).TotalScore);
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
    public async Task SeasonProblemBaseScore_IsFrozenWhenTestCasesChange()
    {
        await using var fixture = await Fixture.CreateAsync(seasonStart: Now.AddHours(1), addSeasonProblem: false);
        var service = fixture.RootSeasonService();
        var result = await service.AddProblemAsync(fixture.Season.Id, new AddLeaderboardSeasonProblemRequest { ProblemId = fixture.Problem.Id });
        Assert.True(result.IsSuccess);
        Assert.Equal(100, Assert.Single(result.Value!.Problems).BaseScore);

        fixture.TestCase.Score = 999;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(100, (await fixture.Db.LeaderboardSeasonProblems.SingleAsync()).BaseScore);
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
    public void Worker_UsesUnifiedSeasonScoreServiceAfterJudgeResult()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.JudgeWorker", "Worker.cs"));
        Assert.Contains("ISeasonScoreService", source, StringComparison.Ordinal);
        Assert.Contains("ApplySubmissionResultAsync", source, StringComparison.Ordinal);
        Assert.Equal(1, Count(source, "ApplySubmissionResultAsync"));
    }

    private static int Count(string value, string token) => (value.Length - value.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        private Fixture()
        {
            Db = new OnlineJudgeDbContext(new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseInMemoryDatabase(databaseName).Options);
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

        public async Task<Guid> ApplyAcceptedAsync(Guid userId, Guid problemId, int runtime, int memory)
        {
            var submission = new Submission
            {
                Id = Guid.NewGuid(), UserId = userId, ProblemId = problemId, SourceCode = "test", Status = JudgeStatus.Accepted,
                Language = JudgeLanguage.Cpp17, TimeUsedMs = runtime, MemoryUsedKb = memory, CreatedAt = Time.GetUtcNow(), FinishedAt = Time.GetUtcNow()
            };
            Db.Submissions.Add(submission);
            await new SeasonScoreService(Db, Time).ApplySubmissionResultAsync(new SeasonSubmissionResult(
                submission.Id, problemId, userId, JudgeStatus.Accepted, runtime, memory));
            await Db.SaveChangesAsync();
            return submission.Id;
        }

        public LeaderboardSeasonService PublicSeasonService() => SeasonService(Answerer);
        public LeaderboardSeasonService RootSeasonService() => SeasonService(Root);
        public LeaderboardSeasonService SeasonService(User user)
        {
            var current = new TestCurrentUser(user);
            var identity = new LeaderboardIdentityService(Db, current, Time);
            return new LeaderboardSeasonService(Db, current, Time, identity);
        }

        public async ValueTask DisposeAsync() => await Db.DisposeAsync();
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
