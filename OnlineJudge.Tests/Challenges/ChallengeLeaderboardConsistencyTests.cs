using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Challenges;

public class ChallengeLeaderboardConsistencyTests
{
    [Fact]
    public async Task Leaderboard_SumsOneBestCompletionPerTask()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var user = NewUser("best-score-user", now);
        var challenge = NewChallenge(user.Id, now);
        SelectChallengeBoard(db, challenge, user.Id, now);
        var tasks = Enumerable.Range(0, 3).Select(_ => NewTask(challenge.Id, now)).ToArray();
        var scores = new[] { 70, 60, 100 };
        db.AddRange(user, challenge);
        db.AddRange(tasks);
        db.ChallengeParticipants.Add(new ChallengeParticipant { Id = Guid.NewGuid(), ChallengeId = challenge.Id, UserId = user.Id, JoinedAt = now });
        db.ChallengeTaskCompletions.AddRange(tasks.Select((task, index) => new ChallengeTaskCompletion
        {
            Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = user.Id,
            Score = scores[index], IsCompleted = index == 2, CompletedAt = now, UpdatedAt = now
        }));
        await db.SaveChangesAsync();

        var leaderboard = await new ChallengeService(db, new TestCurrentUser(user)).GetLeaderboardAsync(challenge.Id);

        Assert.Equal(230, Assert.Single(leaderboard.Value!.Entries).TotalScore);
    }

    [Fact]
    public async Task PartialScoreUser_HasSameRankInLeaderboardAndProgress()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var user = NewUser("partial-user", now);
        var challenge = NewChallenge(user.Id, now);
        SelectChallengeBoard(db, challenge, user.Id, now);
        var task = NewTask(challenge.Id, now);
        db.AddRange(user, challenge, task,
            new ChallengeParticipant { Id = Guid.NewGuid(), ChallengeId = challenge.Id, UserId = user.Id, JoinedAt = now },
            new ChallengeTaskCompletion { Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = user.Id, Score = 150, IsCompleted = false, CompletedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var service = new ChallengeService(db, new TestCurrentUser(user));
        var leaderboard = await service.GetLeaderboardAsync(challenge.Id);
        var progress = await service.GetLeaderboardProgressAsync(challenge.Id);

        Assert.True(leaderboard.IsSuccess);
        Assert.True(progress.IsSuccess);
        var leaderboardUser = Assert.Single(leaderboard.Value!.Entries);
        var progressUser = Assert.Single(progress.Value!.Users);
        Assert.Equal(1, leaderboardUser.Rank);
        Assert.Equal(1, progressUser.Rank);
        Assert.Equal(150, leaderboardUser.TotalScore);
        Assert.Equal(150, progressUser.TotalScore);
        Assert.Equal(150, progressUser.TaskScores[task.Id]);
        Assert.Equal(0, progressUser.CompletedTaskCount);
    }

    [Fact]
    public async Task ZeroScoreUncompletedRecord_RemainsVisibleAsProgressButIsNotRanked()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var user = NewUser("pending-user", now);
        var challenge = NewChallenge(user.Id, now);
        SelectChallengeBoard(db, challenge, user.Id, now);
        var task = NewTask(challenge.Id, now);
        db.AddRange(user, challenge, task,
            new ChallengeParticipant { Id = Guid.NewGuid(), ChallengeId = challenge.Id, UserId = user.Id, JoinedAt = now },
            new ChallengeTaskCompletion { Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = user.Id, Score = 0, IsCompleted = false, CompletedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        var service = new ChallengeService(db, new TestCurrentUser(user));
        var leaderboard = await service.GetLeaderboardAsync(challenge.Id);
        var progress = await service.GetLeaderboardProgressAsync(challenge.Id);

        Assert.True(leaderboard.IsSuccess);
        Assert.True(progress.IsSuccess);
        Assert.Empty(leaderboard.Value!.Entries);
        var progressUser = Assert.Single(progress.Value!.Users);
        Assert.Null(progressUser.Rank);
        Assert.Equal(0, progressUser.TotalScore);
    }

    [Fact]
    public async Task AnonymousAnswerer_IsHiddenAcrossLegacyLeaderboardEndpoints()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var anonymousUser = NewUser("private-user", now);
        anonymousUser.IsLeaderboardAnonymous = true;
        var viewer = NewUser("viewer", now);
        var challenge = NewChallenge(viewer.Id, now);
        var task = NewTask(challenge.Id, now);
        var season = new LeaderboardSeason
        {
            Id = Guid.NewGuid(), Name = "Current", StartAt = now.AddHours(-1), FreezeAt = now.AddHours(1), PublicUntil = now.AddHours(2),
            Status = LeaderboardSeasonStatus.Active, IsCurrent = true, CreatedByUserId = viewer.Id, CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(anonymousUser, viewer, challenge, task, season,
            new ChallengeParticipant { Id = Guid.NewGuid(), ChallengeId = challenge.Id, UserId = anonymousUser.Id, JoinedAt = now },
            new ChallengeTaskCompletion
            {
                Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = anonymousUser.Id,
                Score = 300, IsCompleted = true, CompletedAt = now, UpdatedAt = now
            });
        db.LeaderboardSeasonBoards.Add(new LeaderboardSeasonBoard { Id = Guid.NewGuid(), SeasonId = season.Id, BoardType = LeaderboardSeasonBoardType.Challenge, ChallengeId = challenge.Id, CreatedAt = now });
        await db.SaveChangesAsync();

        var current = new TestCurrentUser(viewer);
        var challengeService = new ChallengeService(db, current);
        var globalService = new LeaderboardService(db, current);
        var challengeBoard = await challengeService.GetLeaderboardAsync(challenge.Id);
        var progress = await challengeService.GetLeaderboardProgressAsync(challenge.Id);
        var history = await challengeService.GetLeaderboardHistoryAsync(challenge.Id);
        var global = await globalService.GetGlobalUserLeaderboardAsync();
        var globalHistory = await globalService.GetGlobalUserRankHistoryAsync();

        AssertHidden(Assert.Single(challengeBoard.Value!.Entries).UserId, Assert.Single(challengeBoard.Value.Entries).UserName);
        AssertHidden(Assert.Single(progress.Value!.Users).UserId, Assert.Single(progress.Value.Users).UserName);
        Assert.All(history.Value!.Days.SelectMany(day => day.Entries), entry => AssertHidden(entry.UserId, entry.UserName));
        AssertHidden(Assert.Single(global.Value!.Entries).UserId, Assert.Single(global.Value.Entries).UserName);
        Assert.All(globalHistory.Value!.Days.SelectMany(day => day.Entries), entry => AssertHidden(entry.UserId, entry.UserName));
    }

    private static void AssertHidden(Guid? userId, string displayName)
    {
        Assert.Null(userId);
        Assert.StartsWith("NODE-", displayName);
        Assert.DoesNotContain("private-user", displayName, StringComparison.Ordinal);
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static User NewUser(string name, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.test", PasswordHash = "test", Role = UserRole.Answerer, CreatedAt = now, UpdatedAt = now
    };

    private static Challenge NewChallenge(Guid ownerId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Title = "Score Consistency", Description = "test", StartAt = now.AddHours(-1), EndAt = now.AddHours(1), CreatedByUserId = ownerId, IsPublished = true, CreatedAt = now, UpdatedAt = now
    };

    private static ChallengeTask NewTask(Guid challengeId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), ChallengeId = challengeId, Title = "Partial", Description = "test", TaskType = ChallengeTaskType.Algorithm, Difficulty = ChallengeTaskDifficulty.Pawn, Score = 300, IsPublished = true, CreatedAt = now, UpdatedAt = now
    };

    private static void SelectChallengeBoard(OnlineJudgeDbContext db, Challenge challenge, Guid creatorId, DateTimeOffset now)
    {
        var season = new LeaderboardSeason
        {
            Id = Guid.NewGuid(), Name = "Current", StartAt = now.AddDays(-1), FreezeAt = now.AddDays(1),
            PublicUntil = now.AddDays(2), Status = LeaderboardSeasonStatus.Active, IsCurrent = true,
            CreatedByUserId = creatorId, CreatedAt = now, UpdatedAt = now
        };
        db.LeaderboardSeasons.Add(season);
        db.LeaderboardSeasonBoards.Add(new LeaderboardSeasonBoard { Id = Guid.NewGuid(), SeasonId = season.Id, BoardType = LeaderboardSeasonBoardType.Challenge, ChallengeId = challenge.Id, CreatedAt = now });
    }

    private sealed class TestCurrentUser(User user) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => user.Id;
        public string? UserName => user.UserName;
        public UserRole? Role => user.Role;
    }
}
