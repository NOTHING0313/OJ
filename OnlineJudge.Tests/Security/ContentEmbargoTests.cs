using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Security;

public class ContentEmbargoTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Answerer_ChallengeListAndDetail_RespectPublishAndStartBoundaries()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var draft = AddChallenge(db, users.SetterId, "draft", Now.AddHours(-1), Now.AddHours(1), false);
        var future = AddChallenge(db, users.SetterId, "future", Now.AddTicks(1), Now.AddHours(1), true);
        var exact = AddChallenge(db, users.SetterId, "exact", Now, Now.AddHours(1), true);
        var running = AddChallenge(db, users.SetterId, "running", Now.AddHours(-1), Now.AddHours(1), true);
        var ended = AddChallenge(db, users.SetterId, "ended", Now.AddHours(-2), Now.AddHours(-1), true);
        await db.SaveChangesAsync();
        var policy = Policy();
        var answerer = new ChallengeService(db, Current(users.AnswererId, UserRole.Answerer), policy);

        var list = await answerer.GetChallengesAsync();

        Assert.DoesNotContain(list.Value!, item => item.Id == draft.Id || item.Id == future.Id);
        Assert.Contains(list.Value!, item => item.Id == exact.Id);
        Assert.Contains(list.Value!, item => item.Id == running.Id);
        Assert.Contains(list.Value!, item => item.Id == ended.Id);
        Assert.Equal("Challenge not found.", (await answerer.GetChallengeAsync(future.Id)).ErrorMessage);
        Assert.Equal("Challenge not found.", (await answerer.GetLeaderboardAsync(future.Id)).ErrorMessage);
        Assert.True((await answerer.GetChallengeAsync(exact.Id)).IsSuccess);
    }

    [Fact]
    public async Task ManagementRoles_CanViewFutureChallenge_WhileFutureJoinIsRejected()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var future = AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true);
        await db.SaveChangesAsync();
        var policy = Policy();

        Assert.True((await new ChallengeService(db, Current(users.SetterId, UserRole.ProblemSetter), policy).GetChallengeAsync(future.Id)).IsSuccess);
        Assert.True((await new ChallengeService(db, Current(users.RootId, UserRole.Root), policy).GetChallengeAsync(future.Id)).IsSuccess);
        var answererJoin = await new ChallengeService(db, Current(users.AnswererId, UserRole.Answerer), policy).JoinChallengeAsync(future.Id);
        Assert.Equal("Challenge not found.", answererJoin.ErrorMessage);
    }

    [Fact]
    public async Task EndedChallenge_RemainsVisibleButCannotBeJoined()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var ended = AddChallenge(db, users.SetterId, "ended", Now.AddHours(-2), Now.AddTicks(-1), true);
        await db.SaveChangesAsync();
        var service = new ChallengeService(db, Current(users.AnswererId, UserRole.Answerer), Policy());

        Assert.True((await service.GetChallengeAsync(ended.Id)).IsSuccess);
        Assert.Equal("Challenge is not open.", (await service.JoinChallengeAsync(ended.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Answerer_ProblemListAndDetail_EnforcePublicationAndDynamicEmbargo()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var normal = AddProblem(db, users.SetterId, "normal", true);
        var unpublished = AddProblem(db, users.SetterId, "unpublished", false);
        var future = AddProblem(db, users.SetterId, "future", true);
        var draft = AddProblem(db, users.SetterId, "draft-reference", true);
        var running = AddProblem(db, users.SetterId, "running", true);
        var ended = AddProblem(db, users.SetterId, "ended", true);
        var multiple = AddProblem(db, users.SetterId, "multiple", true);
        Link(db, AddChallenge(db, users.SetterId, "future", Now.AddTicks(1), Now.AddHours(1), true), future);
        Link(db, AddChallenge(db, users.SetterId, "draft", Now.AddTicks(1), Now.AddHours(1), false), draft);
        Link(db, AddChallenge(db, users.SetterId, "running", Now.AddHours(-1), Now.AddHours(1), true), running);
        Link(db, AddChallenge(db, users.SetterId, "ended", Now.AddHours(-2), Now.AddHours(-1), true), ended);
        Link(db, AddChallenge(db, users.SetterId, "multiple-ended", Now.AddHours(-2), Now.AddHours(-1), true), multiple);
        Link(db, AddChallenge(db, users.SetterId, "multiple-future", Now.AddMinutes(1), Now.AddHours(1), true), multiple);
        await db.SaveChangesAsync();
        var service = new ProblemService(db, Current(users.AnswererId, UserRole.Answerer), Policy());

        var list = await service.GetProblemsAsync();

        Assert.Contains(list.Value!, item => item.Id == normal.Id);
        Assert.Contains(list.Value!, item => item.Id == draft.Id);
        Assert.Contains(list.Value!, item => item.Id == running.Id);
        Assert.Contains(list.Value!, item => item.Id == ended.Id);
        Assert.DoesNotContain(list.Value!, item => item.Id == unpublished.Id || item.Id == future.Id || item.Id == multiple.Id);
        Assert.Equal("Problem not found.", (await service.GetProblemAsync(future.Id)).ErrorMessage);
    }

    [Fact]
    public async Task StartAtExact_MakesChallengeProblemVisible()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var problem = AddProblem(db, users.SetterId, "exact", true);
        Link(db, AddChallenge(db, users.SetterId, "exact", Now, Now.AddHours(1), true), problem);
        await db.SaveChangesAsync();

        var result = await new ProblemService(db, Current(users.AnswererId, UserRole.Answerer), Policy()).GetProblemAsync(problem.Id);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ManagementRoles_CanViewUnpublishedAndEmbargoedProblems()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var unpublished = AddProblem(db, users.SetterId, "unpublished", false);
        var embargoed = AddProblem(db, users.SetterId, "embargoed", true);
        Link(db, AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true), embargoed);
        await db.SaveChangesAsync();

        var setterItems = await new ProblemService(db, Current(users.OtherSetterId, UserRole.ProblemSetter), Policy()).GetProblemsAsync();
        var rootItems = await new ProblemService(db, Current(users.RootId, UserRole.Root), Policy()).GetProblemsAsync();

        Assert.Contains(setterItems.Value!, item => item.Id == unpublished.Id);
        Assert.Contains(setterItems.Value!, item => item.Id == embargoed.Id);
        Assert.Contains(rootItems.Value!, item => item.Id == unpublished.Id);
        Assert.Contains(rootItems.Value!, item => item.Id == embargoed.Id);
    }

    [Fact]
    public async Task PublicEndpoints_UseCurrentDatabaseRoleInsteadOfStaleJwtRole()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var problem = AddProblem(db, users.SetterId, "embargoed", true);
        var challenge = AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true);
        Link(db, challenge, problem);
        await db.SaveChangesAsync();
        var staleProblemSetterClaims = Current(users.AnswererId, UserRole.ProblemSetter);

        var problemResult = await new ProblemService(db, staleProblemSetterClaims, Policy()).GetProblemAsync(problem.Id);
        var challengeResult = await new ChallengeService(db, staleProblemSetterClaims, Policy()).GetChallengeAsync(challenge.Id);

        Assert.Equal("Problem not found.", problemResult.ErrorMessage);
        Assert.Equal("Challenge not found.", challengeResult.ErrorMessage);
    }

    [Fact]
    public async Task RemovingFutureTask_ImmediatelyRemovesProblemEmbargo()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var problem = AddProblem(db, users.SetterId, "unlink", true);
        var task = Link(db, AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true), problem);
        await db.SaveChangesAsync();
        var service = new ProblemService(db, Current(users.AnswererId, UserRole.Answerer), Policy());
        Assert.True((await service.GetProblemAsync(problem.Id)).IsFailure);

        db.ChallengeTasks.Remove(task);
        await db.SaveChangesAsync();

        Assert.True((await service.GetProblemAsync(problem.Id)).IsSuccess);
    }

    [Fact]
    public async Task DirectSubmissionWithoutTask_CannotBypassProblemVisibility()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var embargoed = AddProblem(db, users.SetterId, "embargoed", true);
        var unpublished = AddProblem(db, users.SetterId, "unpublished", false);
        Link(db, AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true), embargoed);
        await db.SaveChangesAsync();
        var service = new SubmissionService(db, new NoopJudgeQueue(), Current(users.AnswererId, UserRole.Answerer), Policy());

        var embargoedResult = await service.CreateSubmissionAsync(Request(embargoed.Id));
        var unpublishedResult = await service.CreateSubmissionAsync(Request(unpublished.Id));

        Assert.Equal("Problem not found.", embargoedResult.ErrorMessage);
        Assert.Equal("Problem not found.", unpublishedResult.ErrorMessage);
        Assert.Empty(db.Submissions);
    }

    [Fact]
    public async Task StartAtExact_AllowsOrdinarySubmissionWithoutChangingChallengeScoring()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var problem = AddProblem(db, users.SetterId, "exact", true);
        Link(db, AddChallenge(db, users.SetterId, "exact", Now, Now.AddHours(1), true), problem);
        await db.SaveChangesAsync();
        var service = new SubmissionService(db, new NoopJudgeQueue(), Current(users.AnswererId, UserRole.Answerer), Policy());

        var result = await service.CreateSubmissionAsync(Request(problem.Id));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ChallengeTaskId);
    }

    [Fact]
    public async Task ChallengeTaskSubmission_CannotBypassDraftOrFutureVisibility()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var futureProblem = AddProblem(db, users.SetterId, "future", true);
        var draftProblem = AddProblem(db, users.SetterId, "draft", true);
        var futureTask = Link(db, AddChallenge(db, users.SetterId, "future", Now.AddMinutes(1), Now.AddHours(1), true), futureProblem);
        var draftTask = Link(db, AddChallenge(db, users.SetterId, "draft", Now.AddHours(-1), Now.AddHours(1), false), draftProblem);
        await db.SaveChangesAsync();
        var service = new SubmissionService(db, new NoopJudgeQueue(), Current(users.AnswererId, UserRole.Answerer), Policy());

        var futureRequest = Request(futureProblem.Id);
        futureRequest.ChallengeTaskId = futureTask.Id;
        var draftRequest = Request(draftProblem.Id);
        draftRequest.ChallengeTaskId = draftTask.Id;

        Assert.Equal("Problem not found.", (await service.CreateSubmissionAsync(futureRequest)).ErrorMessage);
        Assert.Equal("Challenge is not open.", (await service.CreateSubmissionAsync(draftRequest)).ErrorMessage);
        Assert.Empty(db.Submissions);
    }

    [Fact]
    public void ChallengeOpenBoundary_IncludesEndAtAndExcludesAfterEnd()
    {
        var atEndPolicy = new ContentVisibilityPolicy(new FixedTimeProvider(Now));
        var afterEndPolicy = new ContentVisibilityPolicy(new FixedTimeProvider(Now.AddTicks(1)));
        var challenge = new Challenge { StartAt = Now.AddHours(-1), EndAt = Now };

        Assert.True(atEndPolicy.IsChallengeOpen(challenge));
        Assert.False(afterEndPolicy.IsChallengeOpen(challenge));
    }

    [Theory]
    [InlineData(UserRole.Root)]
    [InlineData(UserRole.ProblemSetter)]
    [InlineData(UserRole.Answerer)]
    public async Task AuthorizedRoles_CanPassFileDownloadPermission(UserRole role)
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var challenge = AddChallenge(db, users.SetterId, "file", Now.AddHours(-1), Now.AddHours(1), true);
        var ownerId = role == UserRole.Answerer ? users.AnswererId : users.OtherAnswererId;
        var submission = AddFileSubmission(db, challenge, ownerId);
        await db.SaveChangesAsync();
        var currentId = role switch
        {
            UserRole.Root => users.RootId,
            UserRole.ProblemSetter => users.OtherSetterId,
            _ => users.AnswererId
        };

        var result = await new ChallengeService(db, Current(currentId, role), Policy()).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);

        Assert.Equal("File not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task OtherAnswerer_CannotDownloadAnotherUsersFile()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var challenge = AddChallenge(db, users.SetterId, "file", Now.AddHours(-1), Now.AddHours(1), true);
        var submission = AddFileSubmission(db, challenge, users.AnswererId);
        await db.SaveChangesAsync();

        var result = await new ChallengeService(db, Current(users.OtherAnswererId, UserRole.Answerer), Policy()).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);

        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    [Fact]
    public async Task ChallengeCreator_CanPassFileDownloadPermission()
    {
        await using var db = CreateDb();
        var users = SeedUsers(db);
        var challenge = AddChallenge(db, users.SetterId, "file", Now.AddHours(-1), Now.AddHours(1), true);
        var submission = AddFileSubmission(db, challenge, users.AnswererId);
        await db.SaveChangesAsync();

        var result = await new ChallengeService(db, Current(users.SetterId, UserRole.ProblemSetter), Policy()).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);

        Assert.Equal("File not found.", result.ErrorMessage);
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static ContentVisibilityPolicy Policy() => new(new FixedTimeProvider(Now));

    private static ICurrentUser Current(Guid id, UserRole role) => new TestCurrentUser(id, role);

    private static TestUsers SeedUsers(OnlineJudgeDbContext db)
    {
        var users = new TestUsers();
        db.Users.AddRange(
            User(users.RootId, "root", UserRole.Root),
            User(users.SetterId, "setter", UserRole.ProblemSetter),
            User(users.OtherSetterId, "other-setter", UserRole.ProblemSetter),
            User(users.AnswererId, "answerer", UserRole.Answerer),
            User(users.OtherAnswererId, "other-answerer", UserRole.Answerer));
        db.SaveChanges();
        return users;
    }

    private static User User(Guid id, string name, UserRole role) => new()
    {
        Id = id,
        UserName = name,
        Email = $"{name}@example.test",
        PasswordHash = "hash",
        Role = role,
        CreatedAt = Now,
        UpdatedAt = Now
    };

    private static Problem AddProblem(OnlineJudgeDbContext db, Guid ownerId, string title, bool published)
    {
        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "description",
            InputDescription = "input",
            OutputDescription = "output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = published,
            JudgeMode = JudgeMode.StandardInputOutput,
            CreatedByUserId = ownerId,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        db.Problems.Add(problem);
        return problem;
    }

    private static Challenge AddChallenge(OnlineJudgeDbContext db, Guid ownerId, string title, DateTimeOffset start, DateTimeOffset end, bool published)
    {
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = "description",
            StartAt = start,
            EndAt = end,
            CreatedByUserId = ownerId,
            IsPublished = published,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        db.Challenges.Add(challenge);
        return challenge;
    }

    private static ChallengeTask Link(OnlineJudgeDbContext db, Challenge challenge, Problem problem)
    {
        var task = new ChallengeTask
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            AlgorithmProblemId = problem.Id,
            Title = "algorithm",
            Description = "description",
            TaskType = ChallengeTaskType.Algorithm,
            Difficulty = ChallengeTaskDifficulty.Pawn,
            Score = 100,
            IsPublished = true,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        db.ChallengeTasks.Add(task);
        return task;
    }

    private static ChallengeTaskFileSubmission AddFileSubmission(OnlineJudgeDbContext db, Challenge challenge, Guid ownerId)
    {
        var task = new ChallengeTask
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            Title = "file",
            Description = "description",
            TaskType = ChallengeTaskType.FileUpload,
            Difficulty = ChallengeTaskDifficulty.Pawn,
            Score = 100,
            IsPublished = true,
            CreatedAt = Now,
            UpdatedAt = Now
        };
        var submission = new ChallengeTaskFileSubmission
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            ChallengeTaskId = task.Id,
            UserId = ownerId,
            OriginalFileName = "submission.zip",
            StoredFileName = "missing.zip",
            FilePath = Path.Combine(ResolveApiContentRoot(), "App_Data", "challenge-file-submissions", "missing.zip"),
            FileSizeBytes = 1,
            ContentType = "application/zip",
            CreatedAt = Now,
            UpdatedAt = Now
        };
        db.AddRange(task, submission);
        return submission;
    }

    private static string ResolveApiContentRoot()
    {
        var method = typeof(ChallengeService).GetMethod("ResolveApiContentRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return Assert.IsType<string>(method!.Invoke(null, null));
    }

    private static CreateSubmissionRequest Request(Guid problemId) => new()
    {
        ProblemId = problemId,
        Language = JudgeLanguage.Cpp17,
        SourceCode = "int main() { return 0; }"
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestCurrentUser(Guid id, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? UserName => "test-user";
        public UserRole? Role => role;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestUsers
    {
        public Guid RootId { get; } = Guid.NewGuid();
        public Guid SetterId { get; } = Guid.NewGuid();
        public Guid OtherSetterId { get; } = Guid.NewGuid();
        public Guid AnswererId { get; } = Guid.NewGuid();
        public Guid OtherAnswererId { get; } = Guid.NewGuid();
    }
}
