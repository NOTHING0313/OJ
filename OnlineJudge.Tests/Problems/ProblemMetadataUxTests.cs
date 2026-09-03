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
