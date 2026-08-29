using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Challenges;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Challenges;
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
            var compileAssetLoader = scope.ServiceProvider.GetRequiredService<IJudgeCompileAssetLoader>();
            var seasonScoreService = scope.ServiceProvider.GetRequiredService<ISeasonScoreService>();
            var seasonLifecycleService = scope.ServiceProvider.GetRequiredService<ILeaderboardSeasonLifecycleService>();

            logger.LogInformation("Processing submission. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "LoadSubmission");

            var submission = await dbContext.Submissions
                .Include(submission => submission.Problem)
                .ThenInclude(problem => problem!.TestCases.Where(testCase => !testCase.IsDeleted))
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

            IReadOnlyList<JudgeCompileAsset> compileAssets;
            try
            {
                compileAssets = await compileAssetLoader.LoadAsync(submission.ProblemId, submission.Language, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to load judge compile assets. SubmissionId={SubmissionId}, Stage={Stage}", submissionId, "LoadCompileAssets");
                submission.Status = JudgeStatus.SystemError;
                submission.ErrorMessage = "Judge support files could not be loaded.";
                submission.FinishedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var judgeRequest = ToJudgeRequest(submission, compileAssets);
            var judgedCases = judgeRequest.TestCases.ToDictionary(testCase => testCase.TestCaseId);
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
                    ErrorMessage = caseResult.ErrorMessage,
                    ExpectedOutputSnapshot = judgedCases[caseResult.TestCaseId].ExpectedOutput,
                    ExpectedJsonSnapshot = judgedCases[caseResult.TestCaseId].ExpectedJson,
                    VisibilitySnapshot = judgedCases[caseResult.TestCaseId].Visibility,
                    ScoreSnapshot = judgedCases[caseResult.TestCaseId].Score
                });

                dbContext.SubmissionCaseResults.AddRange(caseResults);
            }

            await using var scoringTransaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            if (submission.ChallengeTaskId.HasValue)
            {
                await UpsertChallengeTaskProgressAsync(dbContext, submission, judgeResult, judgedCases, cancellationToken);
            }

            var seasonScoreResult = await seasonScoreService.ApplySubmissionResultAsync(new SeasonSubmissionResult(
                submission.Id,
                submission.ProblemId,
                submission.UserId,
                submission.Language,
                judgeResult.Status,
                judgeResult.TimeUsedMs,
                judgeResult.MemoryUsedKb,
                submission.CreatedAt,
                submission.FinishedAt!.Value), cancellationToken);

            logger.LogInformation(
                "Updating submission with judge result. SubmissionId={SubmissionId}, Stage={Stage}, Status={Status}",
                submissionId,
                "ApplyJudgeResult",
                judgeResult.Status);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (scoringTransaction is not null) await scoringTransaction.CommitAsync(cancellationToken);
            if (seasonScoreResult.RequiresArchiveRefresh && seasonScoreResult.SeasonId is { } seasonId)
            {
                await seasonLifecycleService.RefreshPublicSeasonAsync(seasonId, cancellationToken);
            }
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

    private static async Task UpsertChallengeTaskProgressAsync(
        OnlineJudgeDbContext dbContext,
        Submission submission,
        JudgeResult judgeResult,
        IReadOnlyDictionary<Guid, JudgeCaseRequest> judgedCases,
        CancellationToken cancellationToken)
    {
        var task = submission.ChallengeTask ?? await dbContext.ChallengeTasks
            .FirstOrDefaultAsync(task => task.Id == submission.ChallengeTaskId, cancellationToken);

        if (task is null || task.TaskType != ChallengeTaskType.Algorithm || submission.Problem is null) return;

        var scoreByTestCaseId = judgedCases.ToDictionary(pair => pair.Key, pair => Math.Max(0, pair.Value.Score));
        var totalTestCaseScore = scoreByTestCaseId.Values.Sum();
        var passedTestCaseScore = judgeResult.CaseResults
            .Where(caseResult => caseResult.Status == JudgeStatus.Accepted)
            .Sum(caseResult => scoreByTestCaseId.GetValueOrDefault(caseResult.TestCaseId));
        var isCompleted = judgeResult.Status == JudgeStatus.Accepted;
        var earnedScore = isCompleted
            ? Math.Max(0, task.Score)
            : ChallengeScoreCalculator.CalculateEarnedScore(task.Score, passedTestCaseScore, totalTestCaseScore);
        var now = DateTimeOffset.UtcNow;

        if (submission.ChallengeTeamParticipantId is { } teamParticipantId)
        {
            await ChallengeBestScoreStore.UpsertTeamAsync(
                dbContext, task.ChallengeId, task.Id, teamParticipantId, earnedScore, isCompleted, task.Score,
                submission.Id, submission.UserId, now, cancellationToken);
            return;
        }

        await ChallengeBestScoreStore.UpsertAlgorithmIndividualAsync(
            dbContext, task.ChallengeId, task.Id, submission.UserId, earnedScore, isCompleted, task.Score,
            submission.Id, now, cancellationToken);
    }

    private static JudgeRequest ToJudgeRequest(Submission submission, IReadOnlyList<JudgeCompileAsset> compileAssets)
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
            CollectAllCaseResults = submission.ChallengeTaskId.HasValue,
            CompileAssets = compileAssets,
            TestCases = problem.TestCases
                .Where(testCase => !testCase.IsDeleted)
                .OrderBy(testCase => testCase.CreatedAt)
                .Select(testCase => new JudgeCaseRequest
                {
                    TestCaseId = testCase.Id,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    ArgumentsJson = testCase.ArgumentsJson,
                    ExpectedJson = testCase.ExpectedJson,
                    Visibility = testCase.Visibility,
                    Score = testCase.Score
                })
                .ToList()
        };
    }
}
