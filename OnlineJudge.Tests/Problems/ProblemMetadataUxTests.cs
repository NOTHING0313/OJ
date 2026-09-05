using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Problems;

public class ProblemMetadataUxTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProblemDetail_Anonymous_UsesAllTestCaseScoresWithoutExposingHiddenCases()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        dbContext.TestCases.AddRange(
            TestCase(ids.ProblemId, TestCaseVisibility.Sample, 1, "sample"),
            TestCase(ids.ProblemId, TestCaseVisibility.Hidden, 99, "hidden"));
        await dbContext.SaveChangesAsync();

        var service = new ProblemService(dbContext, new TestCurrentUser(null, null, false));
        var result = await service.GetProblemAsync(ids.ProblemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.TotalScore);
        Assert.Equal(0b010, result.Value.AllowedLanguagesMask);
        var visible = Assert.Single(result.Value.TestCases);
        Assert.Equal(TestCaseVisibility.Sample, visible.Visibility);
        Assert.Equal(1, visible.Score);
    }

    [Fact]
    public async Task ProblemList_UsesActiveTestCaseScoreSum()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b101);
        dbContext.TestCases.AddRange(
            TestCase(ids.ProblemId, TestCaseVisibility.Sample, 40, "sample"),
            TestCase(ids.ProblemId, TestCaseVisibility.Hidden, 60, "hidden"),
            new TestCase
            {
                Id = Guid.NewGuid(), ProblemId = ids.ProblemId, Input = "deleted", ExpectedOutput = "deleted",
                Visibility = TestCaseVisibility.Hidden, Score = 900, IsDeleted = true, DeletedAt = BaseTime,
                CreatedAt = BaseTime, UpdatedAt = BaseTime
            });
        await dbContext.SaveChangesAsync();

        var result = await new ProblemService(dbContext, new TestCurrentUser(null, null, false)).GetProblemsAsync();

        var problem = Assert.Single(result.Value!);
        Assert.Equal(100, problem.TotalScore);
        Assert.Equal(0b101, problem.AllowedLanguagesMask);
    }

    [Fact]
    public async Task Submission_RestrictedProblem_RejectsDisallowedLanguage()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Selected language is not allowed for this problem.", result.ErrorMessage);
        Assert.Empty(await dbContext.Submissions.ToListAsync());
    }

    [Fact]
    public async Task Submission_RestrictedProblem_AllowsSelectedLanguage()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.C11,
            SourceCode = "int main(void){return 0;}"
        });

        Assert.True(result.IsSuccess);
        var submission = Assert.Single(await dbContext.Submissions.ToListAsync());
        Assert.Equal(JudgeLanguage.C11, submission.Language);
        Assert.Equal((await dbContext.Problems.SingleAsync()).CurrentJudgeRevisionId, submission.ProblemJudgeRevisionId);
        var job = Assert.Single(await dbContext.JudgeJobs.ToListAsync());
        Assert.Equal(submission.Id, job.SubmissionId);
        Assert.Equal(JudgeJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
    }

    [Fact]
    public async Task Submission_RedisSignalFailure_DoesNotRollbackPersistedJob()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0);
        var service = new SubmissionService(
            dbContext,
            new UnavailableJudgeQueue(),
            new TestCurrentUser(ids.AnswererId, UserRole.Answerer));

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}"
        });

        Assert.True(result.IsSuccess);
        var submission = Assert.Single(await dbContext.Submissions.ToListAsync());
        var job = Assert.Single(await dbContext.JudgeJobs.ToListAsync());
        Assert.Equal(submission.Id, job.SubmissionId);
        Assert.Equal(JudgeJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task Submission_JudgeRevisionBinding_CannotBeChangedAfterCreation()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);
        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}"
        });
        Assert.True(result.IsSuccess);

        var submission = await dbContext.Submissions.SingleAsync();
        submission.ProblemJudgeRevisionId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Submission_LanguageValidation_UsesBoundRevisionInsteadOfMutableProblemFields()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        var problem = await dbContext.Problems.SingleAsync();
        problem.AllowedLanguagesMask = 0b001;
        await dbContext.SaveChangesAsync();

        var result = await CreateSubmissionService(dbContext, ids.AnswererId).CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.C11,
            SourceCode = "int main(void){return 0;}"
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull((await dbContext.Submissions.SingleAsync()).ProblemJudgeRevisionId);
    }

    [Fact]
    public async Task Submission_WithoutCurrentJudgeRevision_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0);
        var problem = await dbContext.Problems.SingleAsync();
        problem.CurrentJudgeRevisionId = null;
        problem.CurrentJudgeRevision = null;
        await dbContext.SaveChangesAsync();

        var result = await CreateSubmissionService(dbContext, ids.AnswererId).CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Problem judge definition is unavailable.", result.ErrorMessage);
        Assert.Empty(await dbContext.Submissions.ToListAsync());
    }

    [Fact]
    public async Task Submission_FunctionProblem_RejectsLanguageOutsideFunctionSpec()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(
            dbContext,
            JudgeMode.Function,
            allowedLanguagesMask: 0,
            functionSpecJson: """
                {
                  "functionName": "solve",
                  "returnType": "TreeNode<int>",
                  "parameters": [{ "name": "root", "type": "TreeNode<int>" }],
                  "supportedLanguages": ["cpp17", "csharp"]
                }
                """);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.C11,
            SourceCode = "int solve(){return 0;}"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Selected language is not supported by this function problem.", result.ErrorMessage);
        Assert.Empty(await dbContext.Submissions.ToListAsync());
    }

    [Fact]
    public async Task CreateProblem_FunctionRestrictionOutsideSupportedLanguages_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsers(dbContext);
        var service = new ProblemService(dbContext, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));

        var result = await service.CreateProblemAsync(new CreateProblemRequest
        {
            Title = "Function Language Restriction",
            Description = "Description",
            InputDescription = string.Empty,
            OutputDescription = string.Empty,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = JudgeMode.Function,
            AllowedLanguagesMask = 0b010,
            FunctionSpecJson = """
                {
                  "functionName": "solve",
                  "returnType": "TreeNode<int>",
                  "parameters": [{ "name": "root", "type": "TreeNode<int>" }],
                  "supportedLanguages": ["cpp17", "csharp"]
                }
                """,
            StarterCodeJson = """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}"""
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Allowed languages include a language not supported by the function spec.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateProblem_InvalidLanguageMask_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsers(dbContext);
        var service = new ProblemService(dbContext, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));

        var result = await service.CreateProblemAsync(new CreateProblemRequest
        {
            Title = "Invalid Mask",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = JudgeMode.StandardInputOutput,
            AllowedLanguagesMask = 0b1000
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Unsupported allowed languages mask.", result.ErrorMessage);
    }

    [Fact]
    public async Task PagedProblems_SearchesBeforePagingAndPreservesVisibility()
    {
        await using var db = CreateDbContext();
        var ids = SeedProblem(db, JudgeMode.StandardInputOutput, 0);
        db.Problems.Single().Title = "Visible Alpha";
        db.Problems.Add(new Problem { Id = Guid.NewGuid(), Title = "Hidden Alpha", IsPublished = false, CreatedByUserId = ids.OwnerId });
        db.Problems.Add(new Problem { Id = Guid.NewGuid(), Title = "Visible Beta", IsPublished = true, CreatedByUserId = ids.OwnerId });
        db.TestCases.Add(TestCase(ids.ProblemId, TestCaseVisibility.Hidden, 75, "case"));
        await db.SaveChangesAsync();
        var service = new ProblemService(db, new TestCurrentUser(ids.AnswererId, UserRole.Answerer));
        var result = (await service.QueryProblemsAsync(new ProblemQueryRequest { Keyword = "ALPHA", Page = int.MaxValue, PageSize = 1 })).Value!;
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(ids.ProblemId, Assert.Single(result.Items).Id);
        Assert.Equal(75, result.Items[0].TotalScore);
        var empty = (await service.QueryProblemsAsync(new ProblemQueryRequest { Keyword = "missing", Page = -1, PageSize = 500 })).Value!;
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.Page);
        Assert.Equal(100, empty.PageSize);
        var root = new ProblemService(db, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));
        var all = (await root.QueryProblemsAsync(new ProblemQueryRequest { Keyword = "alpha", PageSize = 1 })).Value!;
        Assert.Equal(2, all.TotalCount);
        var second = (await root.QueryProblemsAsync(new ProblemQueryRequest { Keyword = "alpha", PageSize = 1, Page = 2 })).Value!;
        Assert.NotEqual(Assert.Single(all.Items).Id, Assert.Single(second.Items).Id);
    }

    [Fact]
    public async Task SubmissionKindFilter_IsAppliedBeforePagingAndKeepsOwnerScope()
    {
        await using var db = CreateDbContext();
        var ids = SeedProblem(db, JudgeMode.StandardInputOutput, 0);
        db.Submissions.AddRange(
            new Submission { Id = Guid.NewGuid(), ProblemId = ids.ProblemId, UserId = ids.AnswererId, SubmissionKind = SubmissionKind.Code, Language = JudgeLanguage.Cpp17 },
            new Submission { Id = Guid.NewGuid(), ProblemId = ids.ProblemId, UserId = ids.AnswererId, SubmissionKind = SubmissionKind.Choice, ProblemJudgeRevisionId = db.ProblemJudgeRevisions.Single().Id, ChoiceQuestionResults = [new SubmissionChoiceQuestionResult { Id = Guid.NewGuid(), Score = 5 }] },
            new Submission { Id = Guid.NewGuid(), ProblemId = ids.ProblemId, UserId = ids.OwnerId, SubmissionKind = SubmissionKind.Choice });
        await db.SaveChangesAsync();
        var result = (await CreateSubmissionService(db, ids.AnswererId).QuerySubmissionsAsync(new SubmissionQueryRequest { SubmissionKind = SubmissionKind.Choice, PageSize = 1 })).Value!;
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(ids.AnswererId, Assert.Single(result.Items).UserId);
        Assert.Equal(5, result.Items[0].ChoiceScore);
    }

    [Fact]
    public async Task PagedProblems_LargeCatalogKeepsBoundedPagesAndFindsLateMatches()
    {
        await using var db = CreateDbContext();
        var users = SeedUsers(db);
        db.Problems.AddRange(Enumerable.Range(0, 5000).Select(index => new Problem
        {
            Id = Guid.NewGuid(), Title = $"Catalog {index:D5}", Description = "body",
            InputDescription = "", OutputDescription = "", IsPublished = true,
            CreatedByUserId = users.OwnerId, CreatedAt = BaseTime.AddSeconds(index)
        }));
        await db.SaveChangesAsync();
        var service = new ProblemService(db, new TestCurrentUser(users.AnswererId, UserRole.Answerer));
        var first = (await service.QueryProblemsAsync(new ProblemQueryRequest { PageSize = 20 })).Value!;
        var last = (await service.QueryProblemsAsync(new ProblemQueryRequest { PageSize = 20, Page = 250 })).Value!;
        Assert.Equal(5000, first.TotalCount);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(20, last.Items.Count);
        Assert.Equal("Catalog 04999", first.Items[0].Title);
        Assert.Equal("Catalog 00000", last.Items[^1].Title);
        var searched = (await service.QueryProblemsAsync(new ProblemQueryRequest { Keyword = "00001", PageSize = 20 })).Value!;
        Assert.Equal("Catalog 00001", Assert.Single(searched.Items).Title);
        Assert.Equal(1, searched.TotalCount);
    }

    [Theory]
    [InlineData(ProblemDifficulty.Unrated)]
    [InlineData(ProblemDifficulty.Easy)]
    [InlineData(ProblemDifficulty.Medium)]
    [InlineData(ProblemDifficulty.Hard)]
    public async Task Difficulty_CreateRoundTripsThroughDetailAndBothLists(ProblemDifficulty difficulty)
    {
        await using var db = CreateDbContext();
        var users = SeedUsers(db);
        var service = new ProblemService(db, new TestCurrentUser(users.OwnerId, UserRole.ProblemSetter));
        var result = await service.CreateProblemAsync(new CreateProblemRequest
        {
            Title = "Graded", Description = "body", InputDescription = "in", OutputDescription = "out",
            TimeLimitMs = 1000, MemoryLimitMb = 128, Difficulty = difficulty
        });
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(difficulty, result.Value!.Difficulty);
        Assert.Equal(difficulty, (await service.GetProblemAuthoringAsync(result.Value.Id)).Value!.Difficulty);
        Assert.Equal(difficulty, Assert.Single((await service.GetProblemsAsync()).Value!).Difficulty);
        Assert.Equal(difficulty, Assert.Single((await service.QueryProblemsAsync(new ProblemQueryRequest())).Value!.Items).Difficulty);
    }

    [Fact]
    public async Task Difficulty_UpdatePreservesJudgeRevisionAndOldClientOmission()
    {
        await using var db = CreateDbContext();
        var ids = SeedProblem(db, JudgeMode.StandardInputOutput, 0);
        db.TestCases.Add(TestCase(ids.ProblemId, TestCaseVisibility.Sample, 100, "1"));
        await db.SaveChangesAsync();
        var revision = db.Problems.Single().CurrentJudgeRevisionId;
        var service = new ProblemService(db, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));
        var request = DifficultyUpdate(ProblemDifficulty.Hard, 1);
        var updated = await service.UpdateProblemAsync(ids.ProblemId, request);
        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        Assert.Equal(ProblemDifficulty.Hard, updated.Value!.Difficulty);
        Assert.Equal(2, updated.Value.AuthoringVersion);
        Assert.Equal(revision, updated.Value.CurrentJudgeRevisionId);
        Assert.Single(db.ProblemJudgeRevisions);
        request.Difficulty = null; request.ExpectedAuthoringVersion = 2;
        var omitted = await service.UpdateProblemAsync(ids.ProblemId, request);
        Assert.Equal(ProblemDifficulty.Hard, omitted.Value!.Difficulty);
        Assert.Equal(2, omitted.Value.AuthoringVersion);
        request.Difficulty = ProblemDifficulty.Unrated;
        var cleared = await service.UpdateProblemAsync(ids.ProblemId, request);
        Assert.Equal(ProblemDifficulty.Unrated, cleared.Value!.Difficulty);
        Assert.Equal(3, cleared.Value.AuthoringVersion);
        Assert.Equal(revision, cleared.Value.CurrentJudgeRevisionId);
    }

    [Fact]
    public async Task Difficulty_RejectsInvalidValuesAndJudgeOnlyCollaborator()
    {
        await using var db = CreateDbContext();
        var ids = SeedProblem(db, JudgeMode.StandardInputOutput, 0);
        var service = new ProblemService(db, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));
        var invalid = await service.UpdateProblemAsync(ids.ProblemId, DifficultyUpdate((ProblemDifficulty)99, 1));
        Assert.True(invalid.IsFailure);
        Assert.Equal(ProblemDifficulty.Unrated, db.Problems.Single().Difficulty);
        var create = await service.CreateProblemAsync(new CreateProblemRequest { Difficulty = (ProblemDifficulty)(-1) });
        Assert.True(create.IsFailure);
        db.ProblemCollaborators.Add(new ProblemCollaborator { Id = Guid.NewGuid(), ProblemId = ids.ProblemId, UserId = ids.AnswererId, CanManageTestCases = true });
        await db.SaveChangesAsync();
        var judgeOnly = new ProblemService(db, new TestCurrentUser(ids.AnswererId, UserRole.Answerer));
        var forbidden = await judgeOnly.UpdateProblemAsync(ids.ProblemId, DifficultyUpdate(ProblemDifficulty.Easy, 1));
        Assert.True(forbidden.IsFailure);
        Assert.Equal("Forbidden.", forbidden.ErrorMessage);
        Assert.Equal(ProblemDifficulty.Unrated, db.Problems.Single().Difficulty);
    }

    private static UpdateProblemRequest DifficultyUpdate(ProblemDifficulty? difficulty, long version) => new()
    {
        Title = "Problem", Description = "Description", InputDescription = "Input", OutputDescription = "Output",
        TimeLimitMs = 1000, MemoryLimitMb = 128, IsPublished = true, Difficulty = difficulty, ExpectedAuthoringVersion = version
    };

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static (Guid OwnerId, Guid AnswererId) SeedUsers(OnlineJudgeDbContext dbContext)
    {
        var ownerId = Guid.NewGuid();
        var answererId = Guid.NewGuid();
        dbContext.Users.AddRange(
            User(ownerId, "owner", UserRole.ProblemSetter),
            User(answererId, "answerer", UserRole.Answerer));
        dbContext.SaveChanges();
        return (ownerId, answererId);
    }

    private static (Guid OwnerId, Guid AnswererId, Guid ProblemId) SeedProblem(
        OnlineJudgeDbContext dbContext,
        JudgeMode judgeMode,
        int allowedLanguagesMask,
        string? functionSpecJson = null)
    {
        var users = SeedUsers(dbContext);
        var problemId = Guid.NewGuid();
        var revision = new ProblemJudgeRevision
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            RevisionNumber = 1,
            JudgeMode = judgeMode,
            AllowedLanguagesMask = allowedLanguagesMask,
            FunctionSpecJson = judgeMode == JudgeMode.Function ? functionSpecJson : null,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            CreatedAt = BaseTime
        };
        dbContext.Problems.Add(new Problem
        {
            Id = problemId,
            Title = "Problem",
            Description = "Description",
            InputDescription = judgeMode == JudgeMode.StandardInputOutput ? "Input" : string.Empty,
            OutputDescription = judgeMode == JudgeMode.StandardInputOutput ? "Output" : string.Empty,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = judgeMode,
            AllowedLanguagesMask = allowedLanguagesMask,
            FunctionSpecJson = judgeMode == JudgeMode.Function ? functionSpecJson : null,
            StarterCodeJson = judgeMode == JudgeMode.Function
                ? """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}"""
                : null,
            CreatedByUserId = users.OwnerId,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        dbContext.ProblemJudgeRevisions.Add(revision);
        dbContext.Problems.Local.Single(problem => problem.Id == problemId).CurrentJudgeRevisionId = revision.Id;
        dbContext.SaveChanges();
        return (users.OwnerId, users.AnswererId, problemId);
    }

    private static TestCase TestCase(Guid problemId, TestCaseVisibility visibility, int score, string value)
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Input = value,
            ExpectedOutput = value,
            Visibility = visibility,
            Score = score,
            CreatedAt = BaseTime.AddTicks(score)
        };
    }

    private static SubmissionService CreateSubmissionService(OnlineJudgeDbContext dbContext, Guid answererId)
    {
        return new SubmissionService(dbContext, new NoopJudgeQueue(), new TestCurrentUser(answererId, UserRole.Answerer));
    }

    private static User User(Guid id, string userName, UserRole role)
    {
        return new User
        {
            Id = id,
            UserName = userName,
            Email = $"{userName}@example.test",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
    }

    private sealed class TestCurrentUser(Guid? userId, UserRole? role, bool isAuthenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => isAuthenticated;
        public Guid? UserId => userId;
        public string? UserName => "test-user";
        public UserRole? Role => role;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JudgeQueueReadResult.Empty);
    }

    private sealed class UnavailableJudgeQueue : IJudgeQueue
    {
        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JudgeQueueReadResult.Unavailable);
    }
}
