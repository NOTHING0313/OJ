using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Problems;

public class ChoiceProblemTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 9, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompleteChoiceProblem_CanBeCreatedPublishedWithOneRevision()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var request = CreateChoiceRequest();
        request.IsPublished = true;
        var created = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).CreateProblemAsync(request));
        Assert.True(created.IsPublished);
        Assert.NotNull(created.CurrentJudgeRevisionId);
        Assert.Single(await db.ProblemJudgeRevisions.ToListAsync());
        var detail = AssertSuccess(await ProblemService(db, answerer.Id, UserRole.Answerer).GetProblemAsync(created.Id));
        Assert.All(detail.ChoiceQuestions, question => Assert.Null(question.CorrectOptionIds));
    }

    [Fact]
    public async Task IncompleteChoicePublication_DoesNotPersistProblem()
    {
        await using var db = CreateDbContext();
        var (owner, _) = SeedUsers(db);
        var request = CreateChoiceRequest();
        request.IsPublished = true;
        request.ChoiceQuestions[0].StemMarkdown = "";
        var result = await ProblemService(db, owner.Id, UserRole.ProblemSetter).CreateProblemAsync(request);
        Assert.True(result.IsFailure);
        Assert.Empty(await db.Problems.ToListAsync());
        Assert.Empty(await db.ProblemJudgeRevisions.ToListAsync());
    }

    [Fact]
    public async Task PublishedChoiceProblem_UsesImmutableRevisionAndHidesAnswersFromPublicDetail()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var ownerService = ProblemService(db, owner.Id, UserRole.ProblemSetter);
        var draft = AssertSuccess(await ownerService.CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await ownerService.UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));

        Assert.Equal(2, published.AuthoringVersion);
        var revision = await db.ProblemJudgeRevisions.AsNoTracking()
            .Include(item => item.ChoiceQuestions).ThenInclude(question => question.Options)
            .SingleAsync();
        Assert.Equal(ProblemKind.ChoiceSet, revision.ProblemKind);
        Assert.Equal(2, revision.ChoiceQuestions.Count);
        Assert.Empty(await db.JudgeJobs.ToListAsync());

        db.ChangeTracker.Clear();
        var publicDetail = AssertSuccess(await ProblemService(db, answerer.Id, UserRole.Answerer).GetProblemAsync(draft.Id));
        Assert.All(publicDetail.ChoiceQuestions, question =>
        {
            Assert.Null(question.CorrectOptionIds);
            Assert.Null(question.ExplanationMarkdown);
        });
        var publicJson = JsonSerializer.Serialize(publicDetail);
        Assert.DoesNotContain("CorrectOptionIds", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ExplanationMarkdown", publicJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(revision.ChoiceQuestions.OrderBy(question => question.Order).Select(question => question.Id), publicDetail.ChoiceQuestions.Select(question => question.Id));
    }

    [Fact]
    public async Task EditingPublishedChoiceContent_CreatesOneNewRevisionAndPreservesOldRevision()
    {
        await using var db = CreateDbContext();
        var (owner, _) = SeedUsers(db);
        var service = ProblemService(db, owner.Id, UserRole.ProblemSetter);
        var draft = AssertSuccess(await service.CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await service.UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        var authoring = AssertSuccess(await service.GetProblemAuthoringAsync(published.Id));
        var update = UpdateChoiceRequest(authoring, true);
        update.ChoiceQuestions[0].StemMarkdown = "Changed stem";

        var changed = AssertSuccess(await service.UpdateProblemAsync(published.Id, update));

        Assert.Equal(3, changed.AuthoringVersion);
        var revisions = await db.ProblemJudgeRevisions.AsNoTracking().Include(item => item.ChoiceQuestions)
            .OrderBy(item => item.RevisionNumber).ToListAsync();
        Assert.Equal(2, revisions.Count);
        Assert.Equal("Single", revisions[0].ChoiceQuestions.OrderBy(question => question.Order).First().StemMarkdown);
        Assert.Equal("Changed stem", revisions[1].ChoiceQuestions.OrderBy(question => question.Order).First().StemMarkdown);
    }

    [Fact]
    public async Task ChoiceSubmission_UsesExactSetScoringAndDoesNotCreateJudgeJob()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var draft = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        db.ChangeTracker.Clear();
        var publishedRevisionView = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).GetProblemAsync(published.Id));
        var first = publishedRevisionView.ChoiceQuestions[0];
        var second = publishedRevisionView.ChoiceQuestions[1];
        var result = AssertSuccess(await SubmissionService(db, answerer.Id, TimeProvider.System).CreateChoiceSubmissionAsync(new CreateChoiceSubmissionRequest
        {
            ProblemId = publishedRevisionView.Id,
            ProblemJudgeRevisionId = publishedRevisionView.CurrentJudgeRevisionId!.Value,
            Answers =
            [
                new() { QuestionId = first.Id, OptionIds = first.CorrectOptionIds! },
                new() { QuestionId = second.Id, OptionIds = [second.CorrectOptionIds![0]] }
            ]
        }));

        Assert.Equal(SubmissionKind.Choice, result.SubmissionKind);
        Assert.Equal(JudgeStatus.WrongAnswer, result.Status);
        Assert.Equal(first.Score, result.ChoiceScore);
        Assert.Equal(first.Score + second.Score, result.ChoiceTotalScore);
        Assert.True(result.AnswersRevealed);
        Assert.All(result.ChoiceQuestionResults, item => Assert.NotNull(item.CorrectOptionIds));
        Assert.Empty(await db.JudgeJobs.ToListAsync());
        Assert.Null((await db.Submissions.AsNoTracking().SingleAsync()).Language);
    }

    [Fact]
    public async Task ScheduledReveal_HidesThenRevealsWithoutBackgroundMutation()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var clock = new TestTimeProvider(BaseTime);
        var request = CreateChoiceRequest();
        request.ChoiceAnswerRevealPolicy = ChoiceAnswerRevealPolicy.AtScheduledTime;
        request.ChoiceAnswerRevealAt = BaseTime.AddHours(1);
        var draft = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).CreateProblemAsync(request));
        var update = UpdateChoiceRequest(draft, true);
        update.ChoiceAnswerRevealPolicy = request.ChoiceAnswerRevealPolicy;
        update.ChoiceAnswerRevealAt = request.ChoiceAnswerRevealAt;
        var published = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(draft.Id, update));
        db.ChangeTracker.Clear();
        var revisionView = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).GetProblemAsync(published.Id));
        var result = AssertSuccess(await SubmissionService(db, answerer.Id, clock).CreateChoiceSubmissionAsync(CorrectSubmission(revisionView)));
        Assert.False(result.AnswersRevealed);
        Assert.All(result.ChoiceQuestionResults, item => Assert.Null(item.CorrectOptionIds));

        clock.UtcNow = BaseTime.AddHours(1);
        db.ChangeTracker.Clear();
        var revealed = AssertSuccess(await SubmissionService(db, answerer.Id, clock).GetSubmissionAsync(result.Id));
        Assert.True(revealed.AnswersRevealed);
        Assert.All(revealed.ChoiceQuestionResults, item => Assert.NotNull(item.CorrectOptionIds));
    }

    [Fact]
    public async Task AuthoringVersionAndRevisionConflicts_AreRejected()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var draft = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).CreateProblemAsync(CreateChoiceRequest()));
        var stale = UpdateChoiceRequest(draft, false);
        stale.ExpectedAuthoringVersion = 0;
        var staleResult = await ProblemService(db, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(draft.Id, stale);
        Assert.Equal("authoring_version_conflict:1", staleResult.ErrorMessage);

        var published = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        db.ChangeTracker.Clear();
        var revisionView = AssertSuccess(await ProblemService(db, owner.Id, UserRole.ProblemSetter).GetProblemAsync(published.Id));
        var submission = CorrectSubmission(revisionView);
        submission.ProblemJudgeRevisionId = Guid.NewGuid();
        var conflict = await SubmissionService(db, answerer.Id, TimeProvider.System).CreateChoiceSubmissionAsync(submission);
        Assert.Equal("problem_revision_conflict", conflict.ErrorMessage);
    }

    [Fact]
    public async Task RevealPolicy_CannotHideAnswersAfterAfterSubmissionDisclosure()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var ownerService = ProblemService(db, owner.Id, UserRole.ProblemSetter);
        var draft = AssertSuccess(await ownerService.CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await ownerService.UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        db.ChangeTracker.Clear();
        var revisionView = AssertSuccess(await ownerService.GetProblemAsync(published.Id));
        AssertSuccess(await SubmissionService(db, answerer.Id, TimeProvider.System).CreateChoiceSubmissionAsync(CorrectSubmission(revisionView)));
        db.ChangeTracker.Clear();

        var authoring = AssertSuccess(await ownerService.GetProblemAuthoringAsync(published.Id));
        var update = UpdateChoiceRequest(authoring, true);
        update.ChoiceAnswerRevealPolicy = ChoiceAnswerRevealPolicy.AtScheduledTime;
        update.ChoiceAnswerRevealAt = DateTimeOffset.UtcNow.AddDays(1);
        var result = await ownerService.UpdateProblemAsync(published.Id, update);

        Assert.Equal("answers_already_revealed", result.ErrorMessage);
    }

    [Fact]
    public async Task FullScoreChoiceSubmission_AppliesSeasonBaseScoreWithoutPerformanceCandidate()
    {
        await using var db = CreateDbContext();
        var (owner, answerer) = SeedUsers(db);
        var clock = new TestTimeProvider(BaseTime);
        var ownerService = ProblemService(db, owner.Id, UserRole.ProblemSetter);
        var draft = AssertSuccess(await ownerService.CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await ownerService.UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        var season = new LeaderboardSeason
        {
            Id = Guid.NewGuid(), Name = "Choice season", StartAt = BaseTime.AddHours(-1), FreezeAt = BaseTime.AddHours(1),
            PublicUntil = BaseTime.AddHours(2), Status = LeaderboardSeasonStatus.Active, IsCurrent = true,
            ScoringRulesJson = "{}", CreatedByUserId = owner.Id, CreatedAt = BaseTime, UpdatedAt = BaseTime
        };
        db.LeaderboardSeasons.Add(season);
        db.LeaderboardSeasonProblems.Add(new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(), SeasonId = season.Id, ProblemId = published.Id, BaseScore = 100,
            Season = season, CreatedAt = BaseTime
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var revisionView = AssertSuccess(await ownerService.GetProblemAsync(published.Id));
        var submissionService = new SubmissionService(
            db,
            new NoopJudgeQueue(),
            new TestCurrentUser(answerer.Id, UserRole.Answerer),
            new ContentVisibilityPolicy(clock),
            clock,
            NullLogger<SubmissionService>.Instance,
            seasonScoreService: new SeasonScoreService(db, clock));
        var submission = AssertSuccess(await submissionService.CreateChoiceSubmissionAsync(CorrectSubmission(revisionView)));

        Assert.Equal(JudgeStatus.Accepted, submission.Status);
        var score = await db.LeaderboardUserProblemScores.SingleAsync();
        Assert.Equal(100, score.BestBaseScore);
        Assert.True(score.IsFullScore);
        Assert.Equal(submission.Id, score.FirstFullSubmissionId);
        Assert.Null(score.BestPerformanceSubmissionId);
        Assert.Null(score.BestPerformanceLanguage);
        Assert.Null(score.BestRuntimeMs);
        Assert.Null(score.BestMemoryKb);
    }

    [PostgresIntegrationFact]
    public async Task ConcurrentAuthoringAndReorder_UsesDatabaseLockVersionAndUniqueOrders()
    {
        var connectionString = Environment.GetEnvironmentVariable("ONLINEJUDGE_POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException("ONLINEJUDGE_POSTGRES_CONNECTION is required.");
        await using var setupDb = CreatePostgresDbContext(connectionString);
        var (owner, _) = SeedUsers(setupDb, $"-{Guid.NewGuid():N}"[..9]);
        var setupService = ProblemService(setupDb, owner.Id, UserRole.ProblemSetter);
        var draft = AssertSuccess(await setupService.CreateProblemAsync(CreateChoiceRequest()));
        var published = AssertSuccess(await setupService.UpdateProblemAsync(draft.Id, UpdateChoiceRequest(draft, true)));
        var baseline = AssertSuccess(await setupService.GetProblemAuthoringAsync(published.Id));
        var firstRequest = UpdateChoiceRequest(baseline, true);
        var secondRequest = UpdateChoiceRequest(baseline, true);
        firstRequest.Title = "Concurrent winner A";
        secondRequest.Title = "Concurrent winner B";

        await using var firstDb = CreatePostgresDbContext(connectionString);
        await using var secondDb = CreatePostgresDbContext(connectionString);
        var firstTask = ProblemService(firstDb, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(published.Id, firstRequest);
        var secondTask = ProblemService(secondDb, owner.Id, UserRole.ProblemSetter).UpdateProblemAsync(published.Id, secondRequest);
        var results = await Task.WhenAll(firstTask, secondTask);

        var success = Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => result.IsFailure);
        Assert.StartsWith("authoring_version_conflict:", conflict.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(baseline.AuthoringVersion + 1, success.Value!.AuthoringVersion);

        await using var reorderDb = CreatePostgresDbContext(connectionString);
        var reorderService = ProblemService(reorderDb, owner.Id, UserRole.ProblemSetter);
        var latest = AssertSuccess(await reorderService.GetProblemAuthoringAsync(published.Id));
        var reorderRequest = UpdateChoiceRequest(latest, true);
        reorderRequest.ChoiceQuestions = reorderRequest.ChoiceQuestions.Reverse().ToList();
        foreach (var question in reorderRequest.ChoiceQuestions)
        {
            question.Options = question.Options.Reverse().ToList();
        }

        var reordered = AssertSuccess(await reorderService.UpdateProblemAsync(published.Id, reorderRequest));
        Assert.Equal(latest.AuthoringVersion + 1, reordered.AuthoringVersion);
        var persistedQuestions = await reorderDb.ProblemChoiceQuestions.AsNoTracking()
            .Where(question => question.ProblemId == published.Id && !question.IsDeleted)
            .OrderBy(question => question.Order)
            .Include(question => question.Options.Where(option => !option.IsDeleted))
            .ToListAsync();
        Assert.Equal(Enumerable.Range(0, persistedQuestions.Count), persistedQuestions.Select(question => question.Order));
        Assert.All(persistedQuestions, question =>
            Assert.Equal(Enumerable.Range(0, question.Options.Count), question.Options.OrderBy(option => option.Order).Select(option => option.Order)));
    }

    private static CreateChoiceSubmissionRequest CorrectSubmission(ProblemDetailDto problem) => new()
    {
        ProblemId = problem.Id,
        ProblemJudgeRevisionId = problem.CurrentJudgeRevisionId!.Value,
        Answers = problem.ChoiceQuestions.Select(question => new ChoiceQuestionAnswerRequest
        {
            QuestionId = question.Id,
            OptionIds = question.CorrectOptionIds!
        }).ToList()
    };

    private static CreateProblemRequest CreateChoiceRequest() => new()
    {
        ProblemKind = ProblemKind.ChoiceSet,
        Title = "Choice set",
        Description = "Choose carefully.\n\n```cpp\nint x = 1;\n```",
        JudgeMode = null,
        TimeLimitMs = null,
        MemoryLimitMb = null,
        ChoiceAnswerRevealPolicy = ChoiceAnswerRevealPolicy.AfterSubmission,
        ChoiceQuestions =
        [
            new()
            {
                StemMarkdown = "Single",
                SelectionMode = ChoiceSelectionMode.Single,
                Score = 3,
                ExplanationMarkdown = "Because A.",
                Options = [new() { ContentMarkdown = "A", IsCorrect = true }, new() { ContentMarkdown = "B" }]
            },
            new()
            {
                StemMarkdown = "Multiple",
                SelectionMode = ChoiceSelectionMode.Multiple,
                Score = 7,
                ExplanationMarkdown = "A and B.",
                Options = [new() { ContentMarkdown = "A", IsCorrect = true }, new() { ContentMarkdown = "B", IsCorrect = true }, new() { ContentMarkdown = "C" }]
            }
        ]
    };

    private static UpdateProblemRequest UpdateChoiceRequest(ProblemDetailDto detail, bool published) => new()
    {
        ExpectedAuthoringVersion = detail.AuthoringVersion,
        ProblemKind = ProblemKind.ChoiceSet,
        Title = detail.Title,
        Description = detail.Description,
        IsPublished = published,
        JudgeMode = null,
        TimeLimitMs = null,
        MemoryLimitMb = null,
        ChoiceAnswerRevealPolicy = detail.ChoiceAnswerRevealPolicy,
        ChoiceAnswerRevealAt = detail.ChoiceAnswerRevealAt,
        ChoiceQuestions = detail.ChoiceQuestions.Select(question => new ChoiceQuestionWriteRequest
        {
            Id = question.Id,
            StemMarkdown = question.StemMarkdown,
            SelectionMode = question.SelectionMode,
            Score = question.Score,
            ExplanationMarkdown = question.ExplanationMarkdown ?? string.Empty,
            Options = question.Options.Select(option => new ChoiceOptionWriteRequest
            {
                Id = option.Id,
                ContentMarkdown = option.ContentMarkdown,
                IsCorrect = question.CorrectOptionIds?.Contains(option.Id) == true
            }).ToList()
        }).ToList()
    };

    private static ProblemService ProblemService(OnlineJudgeDbContext db, Guid userId, UserRole role) =>
        new(db, new TestCurrentUser(userId, role), new ContentVisibilityPolicy(TimeProvider.System));

    private static SubmissionService SubmissionService(OnlineJudgeDbContext db, Guid userId, TimeProvider clock) =>
        new(db, new NoopJudgeQueue(), new TestCurrentUser(userId, UserRole.Answerer), new ContentVisibilityPolicy(clock), clock, NullLogger<SubmissionService>.Instance);

    private static (User Owner, User Answerer) SeedUsers(OnlineJudgeDbContext db, string suffix = "")
    {
        var owner = User($"owner{suffix}", UserRole.ProblemSetter);
        var answerer = User($"answerer{suffix}", UserRole.Answerer);
        db.Users.AddRange(owner, answerer);
        db.SaveChanges();
        return (owner, answerer);
    }

    private static User User(string name, UserRole role) => new()
    {
        Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.test", PasswordHash = "hash",
        Role = role, CreatedAt = BaseTime, UpdatedAt = BaseTime
    };

    private static OnlineJudgeDbContext CreateDbContext() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static OnlineJudgeDbContext CreatePostgresDbContext(string connectionString) => new(
        new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseNpgsql(connectionString).Options);

    private static T AssertSuccess<T>(Application.Common.Result<T> result)
    {
        Assert.True(result.IsSuccess, result.ErrorMessage);
        return Assert.IsType<T>(result.Value);
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "test";
        public UserRole? Role => role;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) => Task.FromResult(JudgeQueueReadResult.Empty);
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}

public sealed class PostgresIntegrationFactAttribute : FactAttribute
{
    public PostgresIntegrationFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ONLINEJUDGE_POSTGRES_INTEGRATION"), "1", StringComparison.Ordinal))
        {
            Skip = "Real PostgreSQL integration is disabled. Set ONLINEJUDGE_POSTGRES_INTEGRATION=1 to run this gate.";
        }
    }
}
