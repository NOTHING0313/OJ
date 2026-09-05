using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Challenges;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.Judging;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.JudgeWorker;

public sealed class JudgeJobProcessor(
    OnlineJudgeDbContext dbContext,
    IJudgeRunnerFactory runnerFactory,
    IJudgeCompileAssetLoader compileAssetLoader,
    ISeasonScoreService seasonScoreService,
    ILeaderboardSeasonLifecycleService seasonLifecycleService,
    IJudgeSandboxMaintenance sandboxMaintenance,
    IJudgeJobStore jobStore,
    IJudgeQueue judgeQueue,
    JudgeJobOptions options,
    TimeProvider timeProvider,
    IServiceScopeFactory scopeFactory,
    ILogger<JudgeJobProcessor> logger,
    JudgeResourcePolicy? resourcePolicy = null)
{
    private JudgeResourcePolicy ResourcePolicy { get; } = resourcePolicy ?? JudgeResourcePolicy.Default;

    public async Task ProcessAsync(JudgeJobLease lease, CancellationToken stoppingToken)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var heartbeatState = new LeaseHeartbeatState();
        var heartbeatTask = MaintainLeaseAsync(
            lease,
            heartbeatState,
            executionCancellation,
            heartbeatCancellation.Token);

        try
        {
            logger.LogInformation(
                "Processing leased judge job. SubmissionId={SubmissionId}, Attempt={Attempt}",
                lease.SubmissionId,
                lease.AttemptNumber);
            var removedContainers = await sandboxMaintenance.ReconcileSubmissionContainersAsync(
                lease.SubmissionId,
                executionCancellation.Token);
            if (removedContainers > 0)
            {
                logger.LogWarning(
                    "Removed containers left by a previous lease. SubmissionId={SubmissionId}, Count={Count}",
                    lease.SubmissionId,
                    removedContainers);
            }
            var outcome = await ExecuteJudgeAsync(lease.SubmissionId, executionCancellation.Token);
            if (heartbeatState.IsLeaseLost)
            {
                logger.LogWarning("Judge execution stopped after lease loss. SubmissionId={SubmissionId}", lease.SubmissionId);
                return;
            }

            if (outcome.Failure is { } failure)
            {
                await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
                if (!heartbeatState.IsLeaseLost)
                {
                    await ApplyFailureAsync(lease, failure, stoppingToken);
                }
                return;
            }

            if (outcome.Result is null || outcome.Submission is null || outcome.JudgedCases is null)
            {
                throw new InvalidOperationException("Judge execution produced no result or failure.");
            }

            var completion = await CompleteAsync(
                lease,
                outcome.Submission,
                outcome.Result,
                outcome.JudgedCases,
                executionCancellation.Token);
            await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
            if (!completion.Applied)
            {
                heartbeatState.MarkLeaseLost();
                logger.LogWarning("Judge result was discarded because the lease is no longer valid. SubmissionId={SubmissionId}", lease.SubmissionId);
                return;
            }

            await RefreshPublicSeasonAsync(completion, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (heartbeatState.IsLeaseLost || executionCancellation.IsCancellationRequested)
        {
            logger.LogWarning("Judge execution was cancelled after its lease could no longer be confirmed. SubmissionId={SubmissionId}", lease.SubmissionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Judge job processing failed. SubmissionId={SubmissionId}, Attempt={Attempt}", lease.SubmissionId, lease.AttemptNumber);
            await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
            if (!heartbeatState.IsLeaseLost)
            {
                // A failed completion can leave result and scoring mutations tracked after its
                // transaction rolls back. Discard them before the lease transition is persisted.
                dbContext.ChangeTracker.Clear();
                await ApplyFailureAsync(
                    lease,
                    new ExecutionFailure(
                        JudgeFailureKind.TransientInfrastructure,
                        $"Unhandled judge processor failure: {ex.GetType().Name}.",
                        "Judge execution failed after retries."),
                    stoppingToken);
            }
        }
        finally
        {
            await StopHeartbeatAsync(heartbeatCancellation, heartbeatTask);
        }
    }

    private async Task<ExecutionOutcome> ExecuteJudgeAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await dbContext.Submissions
            .Include(item => item.Problem)
            .Include(item => item.ProblemJudgeRevision)
                .ThenInclude(revision => revision!.TestCases)
            .Include(item => item.ChallengeTask)
            .Include(item => item.CaseResults)
            .FirstOrDefaultAsync(item => item.Id == submissionId, cancellationToken);

        if (submission is null)
        {
            return ExecutionOutcome.Failed(Permanent("Submission is missing for its judge job."));
        }

        if (submission.Problem is null)
        {
            return ExecutionOutcome.Failed(Permanent("Submission problem is missing."));
        }

        if (submission.ProblemJudgeRevision is null
            || submission.ProblemJudgeRevisionId is null
            || submission.ProblemJudgeRevision.Id != submission.ProblemJudgeRevisionId.Value
            || submission.ProblemJudgeRevision.ProblemId != submission.ProblemId)
        {
            return ExecutionOutcome.Failed(Permanent("Submission judge revision is missing or mismatched."));
        }

        if (submission.SubmissionKind != SubmissionKind.Code || submission.Language is null)
        {
            return ExecutionOutcome.Failed(Permanent("Judge jobs may only reference code submissions."));
        }

        if (submission.ProblemJudgeRevision.TestCases.Count == 0)
        {
            return ExecutionOutcome.Failed(Permanent("Submission judge revision contains no test cases."));
        }

        IReadOnlyList<JudgeCompileAsset> compileAssets;
        try
        {
            compileAssets = await compileAssetLoader.LoadRevisionAsync(
                submission.ProblemJudgeRevisionId.Value,
                submission.Language.Value,
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Judge support files are invalid. SubmissionId={SubmissionId}", submissionId);
            return ExecutionOutcome.Failed(Permanent($"Judge support files are invalid: {ex.GetType().Name}."));
        }

        var requestResult = SubmissionJudgeRequestFactory.Create(submission, compileAssets, ResourcePolicy);
        if (requestResult.IsFailure || requestResult.Value is null)
        {
            return ExecutionOutcome.Failed(Permanent(requestResult.ErrorMessage ?? "Judge request could not be built."));
        }

        IJudgeRunner runner;
        try
        {
            runner = runnerFactory.GetRunner(submission.Language.Value);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            logger.LogError(ex, "No valid judge runner is configured. SubmissionId={SubmissionId}", submissionId);
            return ExecutionOutcome.Failed(Permanent($"Judge runner configuration is invalid: {ex.GetType().Name}."));
        }

        using var budgetCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budgetCancellation.CancelAfter(TimeSpan.FromSeconds(ResourcePolicy.SubmissionJudgeWallTimeSeconds));
        JudgeResult result;
        try
        {
            result = await runner.RunAsync(requestResult.Value, budgetCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budgetCancellation.IsCancellationRequested)
        {
            return ExecutionOutcome.Failed(Permanent("Submission judge wall-time budget exceeded."));
        }
        if (result.Status == JudgeStatus.SystemError)
        {
            var kind = result.FailureKind ?? JudgeFailureKind.TransientInfrastructure;
            if (result.FailureKind is null)
            {
                logger.LogWarning("Judge runner returned an unclassified SystemError. SubmissionId={SubmissionId}", submissionId);
            }

            return ExecutionOutcome.Failed(new ExecutionFailure(
                kind,
                result.ErrorMessage ?? "Judge runner returned SystemError.",
                kind == JudgeFailureKind.PermanentConfiguration
                    ? "Judge configuration is invalid."
                    : "Judge execution failed after retries."));
        }

        if (result.FailureKind is not null || result.Status is JudgeStatus.Pending or JudgeStatus.Judging)
        {
            return ExecutionOutcome.Failed(new ExecutionFailure(
                JudgeFailureKind.TransientInfrastructure,
                "Judge runner returned an invalid result contract.",
                "Judge execution failed after retries."));
        }

        var judgedCases = requestResult.Value.TestCases.ToDictionary(testCase => testCase.TestCaseId);
        if (result.CaseResults.Any(caseResult => !judgedCases.ContainsKey(caseResult.TestCaseId))
            || result.CaseResults.Select(caseResult => caseResult.TestCaseId).Distinct().Count() != result.CaseResults.Count)
        {
            return ExecutionOutcome.Failed(new ExecutionFailure(
                JudgeFailureKind.TransientInfrastructure,
                "Judge runner returned unknown or duplicate test case results.",
                "Judge execution failed after retries."));
        }

        return ExecutionOutcome.Succeeded(submission, result, judgedCases);
    }

    private async Task<CompletionOutcome> CompleteAsync(
        JudgeJobLease lease,
        Submission submission,
        JudgeResult result,
        IReadOnlyDictionary<Guid, JudgeCaseRequest> judgedCases,
        CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var job = await LockJobAsync(lease.SubmissionId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (job is null
            || job.Status != JudgeJobStatus.Leased
            || job.LeaseToken != lease.LeaseToken
            || job.LeaseExpiresAt <= now)
        {
            return CompletionOutcome.NotApplied;
        }

        submission.Status = result.Status;
        submission.TimeUsedMs = result.TimeUsedMs;
        submission.MemoryUsedKb = result.MemoryUsedKb;
        submission.ErrorMessage = result.ErrorMessage;
        submission.FinishedAt = now;

        if (submission.CaseResults.Count > 0)
        {
            dbContext.SubmissionCaseResults.RemoveRange(submission.CaseResults);
        }

        dbContext.SubmissionCaseResults.AddRange(result.CaseResults.Select(caseResult => new SubmissionCaseResult
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
        }));

        if (submission.ChallengeTaskId.HasValue)
        {
            await UpsertChallengeTaskProgressAsync(dbContext, submission, result, judgedCases, now, cancellationToken);
        }

        var seasonScoreResult = await seasonScoreService.ApplySubmissionResultAsync(new SeasonSubmissionResult(
            submission.Id,
            submission.ProblemId,
            submission.UserId,
            submission.Language!.Value,
            result.Status,
            result.TimeUsedMs,
            result.MemoryUsedKb,
            submission.CreatedAt,
            now), cancellationToken);

        job.Status = JudgeJobStatus.Completed;
        job.LeaseToken = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.LastFailureKind = null;
        job.LastError = null;
        job.UpdatedAt = now;
        job.FinishedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Judge job completed. SubmissionId={SubmissionId}, Attempt={Attempt}, Status={Status}",
            lease.SubmissionId,
            lease.AttemptNumber,
            result.Status);
        return new CompletionOutcome(true, seasonScoreResult.RequiresArchiveRefresh, seasonScoreResult.SeasonId);
    }

    private async Task RefreshPublicSeasonAsync(CompletionOutcome completion, CancellationToken cancellationToken)
    {
        if (!completion.RequiresArchiveRefresh || completion.SeasonId is not { } seasonId)
        {
            return;
        }

        try
        {
            await seasonLifecycleService.RefreshPublicSeasonAsync(seasonId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Public season refresh was deferred during worker shutdown. SeasonId={SeasonId}", seasonId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Public season refresh failed after judge result commit. SeasonId={SeasonId}", seasonId);
        }
    }

    private async Task ApplyFailureAsync(
        JudgeJobLease lease,
        ExecutionFailure failure,
        CancellationToken cancellationToken)
    {
        if (failure.Kind == JudgeFailureKind.PermanentConfiguration || lease.AttemptNumber >= options.MaxAttempts)
        {
            var transition = await jobStore.DeadLetterAsync(
                lease,
                failure.Kind,
                failure.InternalError,
                failure.UserError,
                cancellationToken);
            logger.LogWarning(
                "Judge job reached a terminal failure. SubmissionId={SubmissionId}, Attempt={Attempt}, Kind={Kind}, Transition={Transition}",
                lease.SubmissionId,
                lease.AttemptNumber,
                failure.Kind,
                transition);
            return;
        }

        var delay = options.GetRetryDelay(lease.AttemptNumber);
        var result = await jobStore.RequeueAsync(
            lease,
            failure.Kind,
            failure.InternalError,
            delay,
            cancellationToken);
        logger.LogWarning(
            "Judge job scheduled for retry. SubmissionId={SubmissionId}, Attempt={Attempt}, Delay={Delay}, Transition={Transition}",
            lease.SubmissionId,
            lease.AttemptNumber,
            delay,
            result);
        if (result == JudgeJobTransitionResult.Applied)
        {
            try
            {
                await judgeQueue.TryEnqueueSubmissionAsync(lease.SubmissionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Retry wake-up signal failed; database polling will recover the job. SubmissionId={SubmissionId}", lease.SubmissionId);
            }
        }
    }

    private async Task MaintainLeaseAsync(
        JudgeJobLease lease,
        LeaseHeartbeatState state,
        CancellationTokenSource executionCancellation,
        CancellationToken cancellationToken)
    {
        var confirmedExpiry = lease.LeaseExpiresAt;
        var nextDelay = options.HeartbeatInterval;
        var safetyMargin = TimeSpan.FromSeconds(Math.Min(10, Math.Max(1, options.LeaseDurationSeconds / 4)));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(nextDelay, cancellationToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                var heartbeatStore = scope.ServiceProvider.GetRequiredService<IJudgeJobStore>();
                var renewal = await heartbeatStore.RenewLeaseAsync(lease, cancellationToken);
                if (renewal.Transition == JudgeJobTransitionResult.LeaseLost || !renewal.LeaseExpiresAt.HasValue)
                {
                    state.MarkLeaseLost();
                    await executionCancellation.CancelAsync();
                    return;
                }

                confirmedExpiry = renewal.LeaseExpiresAt.Value;
                nextDelay = options.HeartbeatInterval;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Judge lease heartbeat failed. SubmissionId={SubmissionId}", lease.SubmissionId);
                if (timeProvider.GetUtcNow() >= confirmedExpiry.Subtract(safetyMargin))
                {
                    state.MarkLeaseLost();
                    await executionCancellation.CancelAsync();
                    return;
                }

                nextDelay = TimeSpan.FromSeconds(1);
            }
        }
    }

    private async Task<JudgeJob?> LockJobAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.JudgeJobs.SingleOrDefaultAsync(job => job.SubmissionId == submissionId, cancellationToken);
        }

        var jobs = await dbContext.JudgeJobs.FromSqlInterpolated($$"""
            SELECT * FROM "JudgeJobs"
            WHERE "SubmissionId" = {{submissionId}}
            FOR UPDATE
            """).AsTracking().ToListAsync(cancellationToken);
        return jobs.SingleOrDefault();
    }

    private static async Task StopHeartbeatAsync(CancellationTokenSource cancellation, Task heartbeatTask)
    {
        if (!cancellation.IsCancellationRequested)
        {
            await cancellation.CancelAsync();
        }

        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private static ExecutionFailure Permanent(string internalError) => new(
        JudgeFailureKind.PermanentConfiguration,
        internalError,
        "Judge configuration is invalid.");

    private static async Task UpsertChallengeTaskProgressAsync(
        OnlineJudgeDbContext dbContext,
        Submission submission,
        JudgeResult judgeResult,
        IReadOnlyDictionary<Guid, JudgeCaseRequest> judgedCases,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var task = submission.ChallengeTask ?? await dbContext.ChallengeTasks
            .FirstOrDefaultAsync(item => item.Id == submission.ChallengeTaskId, cancellationToken);

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

    private sealed record ExecutionFailure(JudgeFailureKind Kind, string InternalError, string UserError);

    private sealed record CompletionOutcome(bool Applied, bool RequiresArchiveRefresh, Guid? SeasonId)
    {
        public static CompletionOutcome NotApplied { get; } = new(false, false, null);
    }

    private sealed record ExecutionOutcome(
        Submission? Submission,
        JudgeResult? Result,
        IReadOnlyDictionary<Guid, JudgeCaseRequest>? JudgedCases,
        ExecutionFailure? Failure)
    {
        public static ExecutionOutcome Failed(ExecutionFailure failure) => new(null, null, null, failure);

        public static ExecutionOutcome Succeeded(
            Submission submission,
            JudgeResult result,
            IReadOnlyDictionary<Guid, JudgeCaseRequest> judgedCases) => new(submission, result, judgedCases, null);
    }

    private sealed class LeaseHeartbeatState
    {
        private int leaseLost;

        public bool IsLeaseLost => Volatile.Read(ref leaseLost) != 0;

        public void MarkLeaseLost() => Interlocked.Exchange(ref leaseLost, 1);
    }
}
