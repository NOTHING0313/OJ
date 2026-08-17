using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using StackExchange.Redis;

namespace OnlineJudge.JudgeWorker;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<Worker> logger) : BackgroundService
{
    private const string PendingSubmissionsKey = "judge:submissions:pending";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var database = connectionMultiplexer.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var submissionIdValue = await database.ListLeftPopAsync(PendingSubmissionsKey);

                if (!submissionIdValue.HasValue)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                if (!Guid.TryParse(submissionIdValue.ToString(), out var submissionId))
                {
                    logger.LogWarning("Skipping invalid submission id from queue. SubmissionId={SubmissionId}, Stage={Stage}", submissionIdValue.ToString(), "ParseSubmissionId");
                    continue;
                }

                await ProcessSubmissionAsync(submissionId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Judge worker loop failed. SubmissionId={SubmissionId}, Stage={Stage}", null, "ConsumeQueue");
            }
        }
    }

    private async Task ProcessSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OnlineJudgeDbContext>();
            var runnerFactory = scope.ServiceProvider.GetRequiredService<IJudgeRunnerFactory>();

            logger.LogInformation("Processing submission. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "LoadSubmission");

            var submission = await dbContext.Submissions
                .Include(submission => submission.Problem)
                .ThenInclude(problem => problem!.TestCases)
                .Include(submission => submission.ChallengeTask)
                .Include(submission => submission.CaseResults)
                .FirstOrDefaultAsync(submission => submission.Id == submissionId, cancellationToken);

            if (submission is null)
            {
                logger.LogWarning("Submission not found. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "LoadSubmission");
                return;
            }

            if (submission.Status != JudgeStatus.Pending)
            {
                logger.LogInformation(
                    "Skipping submission because status is not Pending. SubmissionId={SubmissionId}, Stage={Stage}, Status={Status}",
                    submissionId,
                    "ValidateStatus",
                    submission.Status);
                return;
            }

            logger.LogInformation("Marking submission as Judging. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "MarkJudging");
            submission.Status = JudgeStatus.Judging;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (submission.Problem is null)
            {
                logger.LogWarning("Submission problem not found. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "BuildJudgeRequest");
                submission.Status = JudgeStatus.SystemError;
                submission.ErrorMessage = "Problem not found for submission.";
                submission.FinishedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var judgeRequest = ToJudgeRequest(submission);
            var runner = runnerFactory.GetRunner(submission.Language);

            logger.LogInformation("Running judge runner. SubmissionId={SubmissionId}, Stage={Stage}, Language={Language}", submissionId, "RunJudge", submission.Language);
            var judgeResult = await runner.RunAsync(judgeRequest, cancellationToken);

            submission.Status = judgeResult.Status;
            submission.TimeUsedMs = judgeResult.TimeUsedMs;
            submission.MemoryUsedKb = judgeResult.MemoryUsedKb;
            submission.ErrorMessage = judgeResult.ErrorMessage;
            submission.FinishedAt = DateTimeOffset.UtcNow;

            if (judgeResult.CaseResults.Count > 0)
            {
                dbContext.SubmissionCaseResults.RemoveRange(submission.CaseResults);

                var caseResults = judgeResult.CaseResults.Select(caseResult => new SubmissionCaseResult
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    TestCaseId = caseResult.TestCaseId,
                    Status = caseResult.Status,
                    TimeUsedMs = caseResult.TimeUsedMs,
                    MemoryUsedKb = caseResult.MemoryUsedKb,
                    ActualOutput = caseResult.ActualOutput,
                    ErrorMessage = caseResult.ErrorMessage
                });

                dbContext.SubmissionCaseResults.AddRange(caseResults);
            }

            if (submission.Status == JudgeStatus.Accepted && submission.ChallengeTaskId.HasValue)
            {
                await UpsertChallengeTaskCompletionAsync(dbContext, submission, cancellationToken);
            }

            logger.LogInformation(
                "Updating submission with judge result. SubmissionId={SubmissionId}, Stage={Stage}, Status={Status}",
                submissionId,
                "ApplyJudgeResult",
                judgeResult.Status);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process submission. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "ProcessSubmission");
        }
    }

    private static async Task UpsertChallengeTaskCompletionAsync(
        OnlineJudgeDbContext dbContext,
        Submission submission,
        CancellationToken cancellationToken)
    {
        var task = submission.ChallengeTask ?? await dbContext.ChallengeTasks
            .FirstOrDefaultAsync(task => task.Id == submission.ChallengeTaskId, cancellationToken);

        if (task is null || task.TaskType != ChallengeTaskType.Algorithm)
        {
            return;
        }

        var completion = await dbContext.ChallengeTaskCompletions
            .FirstOrDefaultAsync(
                completion => completion.UserId == submission.UserId && completion.ChallengeTaskId == task.Id,
                cancellationToken);

        if (completion is null)
        {
            completion = new ChallengeTaskCompletion
            {
                Id = Guid.NewGuid(),
                ChallengeId = task.ChallengeId,
                ChallengeTaskId = task.Id,
                UserId = submission.UserId,
                SubmissionId = submission.Id,
                CompletedAt = DateTimeOffset.UtcNow,
                Score = task.Score
            };

            dbContext.ChallengeTaskCompletions.Add(completion);
            return;
        }

        completion.SubmissionId = submission.Id;
        completion.Score = task.Score;
    }

    private static JudgeRequest ToJudgeRequest(Submission submission)
    {
        var problem = submission.Problem!;

        return new JudgeRequest
        {
            SubmissionId = submission.Id,
            ProblemId = submission.ProblemId,
            Language = submission.Language,
            JudgeMode = problem.JudgeMode,
            SourceCode = submission.SourceCode,
            FunctionSpecJson = problem.FunctionSpecJson,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitMb = problem.MemoryLimitMb,
            TestCases = problem.TestCases
                .OrderBy(testCase => testCase.CreatedAt)
                .Select(testCase => new JudgeCaseRequest
                {
                    TestCaseId = testCase.Id,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    ArgumentsJson = testCase.ArgumentsJson,
                    ExpectedJson = testCase.ExpectedJson
                })
                .ToList()
        };
    }
}
