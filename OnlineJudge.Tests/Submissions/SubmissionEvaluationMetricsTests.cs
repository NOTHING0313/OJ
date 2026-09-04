using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Submissions;

public class SubmissionEvaluationMetricsTests
{
    [Fact]
    public void FromCaseResults_ComputesFourMetricsAndRoundsAverages()
    {
        var result = SubmissionEvaluationMetrics.FromCaseResults(
        [
            Case(timeMs: 10, memoryKb: 100),
            Case(timeMs: 11, memoryKb: null),
            Case(timeMs: 13, memoryKb: 201)
        ]);

        Assert.Equal(13, result.MaxTimeUsedMs);
        Assert.Equal(11.33m, result.AverageCaseTimeUsedMs);
        Assert.Equal(201, result.MaxMemoryUsedKb);
        Assert.Equal(150.50m, result.AverageCaseMemoryUsedKb);
    }

    [Fact]
    public void FromCaseResults_LeavesMetricsNullWhenNoMeasurementsExist()
    {
        var result = SubmissionEvaluationMetrics.FromCaseResults([Case(null, null)]);

        Assert.Null(result.MaxTimeUsedMs);
        Assert.Null(result.AverageCaseTimeUsedMs);
        Assert.Null(result.MaxMemoryUsedKb);
        Assert.Null(result.AverageCaseMemoryUsedKb);
    }

    [Fact]
    public async Task SubmissionService_ReturnsSameEvaluationForDetailAndList()
    {
        await using var db = new OnlineJudgeDbContext(new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        var userId = Guid.NewGuid();
        var problemId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();
        var firstTestCaseId = Guid.NewGuid();
        var secondTestCaseId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            UserName = "answerer",
            Email = "answerer@example.test",
            PasswordHash = "hash",
            Role = UserRole.Answerer
        });
        db.Problems.Add(new Problem
        {
            Id = problemId,
            Title = "Metrics",
            Description = string.Empty,
            InputDescription = string.Empty,
            OutputDescription = string.Empty,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            CreatedByUserId = userId
        });
        db.TestCases.AddRange(
            new TestCase { Id = firstTestCaseId, ProblemId = problemId, Input = "1", ExpectedOutput = "1", Visibility = TestCaseVisibility.Sample },
            new TestCase { Id = secondTestCaseId, ProblemId = problemId, Input = "2", ExpectedOutput = "2", Visibility = TestCaseVisibility.Hidden });
        db.Submissions.Add(new Submission
        {
            Id = submissionId,
            ProblemId = problemId,
            UserId = userId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}",
            Status = JudgeStatus.Accepted,
            CaseResults =
            [
                new SubmissionCaseResult { Id = Guid.NewGuid(), SubmissionId = submissionId, TestCaseId = firstTestCaseId, TimeUsedMs = 10, MemoryUsedKb = 100 },
                new SubmissionCaseResult { Id = Guid.NewGuid(), SubmissionId = submissionId, TestCaseId = secondTestCaseId, TimeUsedMs = 13, MemoryUsedKb = 201 }
            ]
        });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.SubmissionCaseResults.CountAsync());
        db.ChangeTracker.Clear();
        var service = new SubmissionService(db, new NoopJudgeQueue(), new TestCurrentUser(userId));

        var detail = await service.GetSubmissionAsync(submissionId);
        var list = await service.QuerySubmissionsAsync(new SubmissionQueryRequest());

        Assert.Equal(13, detail.Value!.Evaluation.MaxTimeUsedMs);
        Assert.Equal(11.50m, detail.Value.Evaluation.AverageCaseTimeUsedMs);
        var listEvaluation = Assert.Single(list.Value!.Items).Evaluation;
        Assert.Equal(detail.Value.Evaluation.MaxTimeUsedMs, listEvaluation.MaxTimeUsedMs);
        Assert.Equal(detail.Value.Evaluation.AverageCaseTimeUsedMs, listEvaluation.AverageCaseTimeUsedMs);
        Assert.Equal(detail.Value.Evaluation.MaxMemoryUsedKb, listEvaluation.MaxMemoryUsedKb);
        Assert.Equal(detail.Value.Evaluation.AverageCaseMemoryUsedKb, listEvaluation.AverageCaseMemoryUsedKb);
    }

    private static SubmissionCaseResult Case(int? timeMs, int? memoryKb) => new()
    {
        TimeUsedMs = timeMs,
        MemoryUsedKb = memoryKb
    };

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "answerer";
        public UserRole? Role => UserRole.Answerer;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) => Task.FromResult(JudgeQueueReadResult.Empty);
    }
}
