using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Judging;

public sealed class JudgeJobStore(
    OnlineJudgeDbContext dbContext,
    JudgeJobOptions options,
    TimeProvider timeProvider) : IJudgeJobStore
{
    private const string RetryExhaustedMessage = "Judge execution failed after the retry limit was reached.";

    public async Task<JudgeJobLease?> TryClaimAsync(
        Guid? preferredSubmissionId,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        if (workerId.Length > 200) throw new ArgumentOutOfRangeException(nameof(workerId));

        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var job = await LockNextClaimableAsync(preferredSubmissionId, now, cancellationToken);
        if (job is null)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var submission = await dbContext.Submissions
            .SingleAsync(item => item.Id == job.SubmissionId, cancellationToken);

        if (IsTerminal(submission.Status))
        {
            MarkCompleted(job, submission.FinishedAt ?? now, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (job.AttemptCount >= options.MaxAttempts)
        {
            MarkDeadLettered(
                job,
                submission,
                JudgeFailureKind.TransientInfrastructure,
                "The previous judge lease expired after the retry limit was reached.",
                RetryExhaustedMessage,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var leaseToken = Guid.NewGuid();
        var leaseExpiresAt = now.Add(options.LeaseDuration);
        job.Status = JudgeJobStatus.Leased;
        job.AttemptCount++;
        job.LeaseToken = leaseToken;
        job.LeaseOwner = workerId;
        job.LeaseExpiresAt = leaseExpiresAt;
        job.LastAttemptStartedAt = now;
        job.UpdatedAt = now;
        job.FinishedAt = null;
        submission.Status = JudgeStatus.Judging;
        submission.TimeUsedMs = null;
        submission.MemoryUsedKb = null;
        submission.ErrorMessage = null;
        submission.FinishedAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new JudgeJobLease(job.SubmissionId, leaseToken, job.AttemptCount, leaseExpiresAt);
    }

    public async Task<JudgeLeaseRenewalResult> RenewLeaseAsync(
        JudgeJobLease lease,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var job = await LockJobAsync(lease.SubmissionId, cancellationToken);
        if (!OwnsUnexpiredLease(job, lease, now))
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new JudgeLeaseRenewalResult(JudgeJobTransitionResult.LeaseLost, null);
        }

        var leaseExpiresAt = now.Add(options.LeaseDuration);
        job!.LeaseExpiresAt = leaseExpiresAt;
        job.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new JudgeLeaseRenewalResult(JudgeJobTransitionResult.Applied, leaseExpiresAt);
    }

    public async Task<JudgeJobTransitionResult> RequeueAsync(
        JudgeJobLease lease,
        JudgeFailureKind failureKind,
        string error,
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));

        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var job = await LockJobAsync(lease.SubmissionId, cancellationToken);
        if (!OwnsUnexpiredLease(job, lease, now))
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return JudgeJobTransitionResult.LeaseLost;
        }

        var submission = await dbContext.Submissions
            .Include(item => item.CaseResults)
            .SingleAsync(item => item.Id == lease.SubmissionId, cancellationToken);
        job!.Status = JudgeJobStatus.Pending;
        job.AvailableAt = now.Add(delay);
        ClearLease(job);
        job.LastFailureKind = failureKind;
        job.LastError = NormalizeError(error);
        job.UpdatedAt = now;
        job.FinishedAt = null;
        submission.Status = JudgeStatus.Pending;
        submission.TimeUsedMs = null;
        submission.MemoryUsedKb = null;
        submission.ErrorMessage = null;
        submission.FinishedAt = null;
        if (submission.CaseResults.Count > 0)
        {
            dbContext.SubmissionCaseResults.RemoveRange(submission.CaseResults);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return JudgeJobTransitionResult.Applied;
    }

    public async Task<JudgeJobTransitionResult> DeadLetterAsync(
        JudgeJobLease lease,
        JudgeFailureKind failureKind,
        string internalError,
        string userError,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);
        var job = await LockJobAsync(lease.SubmissionId, cancellationToken);
        if (!OwnsUnexpiredLease(job, lease, now))
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return JudgeJobTransitionResult.LeaseLost;
        }

        var submission = await dbContext.Submissions
            .SingleAsync(item => item.Id == lease.SubmissionId, cancellationToken);
        MarkDeadLettered(job!, submission, failureKind, internalError, userError, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return JudgeJobTransitionResult.Applied;
    }

    private async Task<JudgeJob?> LockNextClaimableAsync(
        Guid? preferredSubmissionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var query = dbContext.JudgeJobs.Where(job =>
                (job.Status == JudgeJobStatus.Pending && job.AvailableAt <= now)
                || (job.Status == JudgeJobStatus.Leased && job.LeaseExpiresAt <= now));
            if (preferredSubmissionId.HasValue)
            {
                query = query.Where(job => job.SubmissionId == preferredSubmissionId.Value);
            }

            return await query
                .OrderBy(job => job.Status == JudgeJobStatus.Leased ? job.LeaseExpiresAt : job.AvailableAt)
                .ThenBy(job => job.CreatedAt)
                .ThenBy(job => job.SubmissionId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        List<JudgeJob> jobs;
        if (preferredSubmissionId.HasValue)
        {
            jobs = await dbContext.JudgeJobs.FromSqlInterpolated($$"""
                SELECT * FROM "JudgeJobs"
                WHERE "SubmissionId" = {{preferredSubmissionId.Value}}
                  AND (("Status" = {{(int)JudgeJobStatus.Pending}} AND "AvailableAt" <= {{now}})
                    OR ("Status" = {{(int)JudgeJobStatus.Leased}} AND "LeaseExpiresAt" <= {{now}}))
                FOR UPDATE SKIP LOCKED
                """).AsTracking().ToListAsync(cancellationToken);
        }
        else
        {
            jobs = await dbContext.JudgeJobs.FromSqlInterpolated($$"""
                SELECT * FROM "JudgeJobs"
                WHERE ("Status" = {{(int)JudgeJobStatus.Pending}} AND "AvailableAt" <= {{now}})
                   OR ("Status" = {{(int)JudgeJobStatus.Leased}} AND "LeaseExpiresAt" <= {{now}})
                ORDER BY CASE WHEN "Status" = {{(int)JudgeJobStatus.Leased}} THEN "LeaseExpiresAt" ELSE "AvailableAt" END,
                         "CreatedAt",
                         "SubmissionId"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """).AsTracking().ToListAsync(cancellationToken);
        }

        return jobs.SingleOrDefault();
    }

    private async Task<JudgeJob?> LockJobAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.JudgeJobs.SingleOrDefaultAsync(
                job => job.SubmissionId == submissionId,
                cancellationToken);
        }

        var jobs = await dbContext.JudgeJobs.FromSqlInterpolated($$"""
            SELECT * FROM "JudgeJobs"
            WHERE "SubmissionId" = {{submissionId}}
            FOR UPDATE
            """).AsTracking().ToListAsync(cancellationToken);
        return jobs.SingleOrDefault();
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private static bool OwnsUnexpiredLease(JudgeJob? job, JudgeJobLease lease, DateTimeOffset now)
    {
        return job is not null
            && job.Status == JudgeJobStatus.Leased
            && job.LeaseToken == lease.LeaseToken
            && job.LeaseExpiresAt > now;
    }

    private static void MarkCompleted(JudgeJob job, DateTimeOffset finishedAt, DateTimeOffset now)
    {
        job.Status = JudgeJobStatus.Completed;
        ClearLease(job);
        job.LastFailureKind = null;
        job.LastError = null;
        job.UpdatedAt = now;
        job.FinishedAt = finishedAt;
    }

    private static void MarkDeadLettered(
        JudgeJob job,
        Submission submission,
        JudgeFailureKind failureKind,
        string internalError,
        string userError,
        DateTimeOffset now)
    {
        job.Status = JudgeJobStatus.DeadLettered;
        ClearLease(job);
        job.LastFailureKind = failureKind;
        job.LastError = NormalizeError(internalError);
        job.UpdatedAt = now;
        job.FinishedAt = now;
        submission.Status = JudgeStatus.SystemError;
        submission.TimeUsedMs = null;
        submission.MemoryUsedKb = null;
        submission.ErrorMessage = userError;
        submission.FinishedAt = now;
    }

    private static void ClearLease(JudgeJob job)
    {
        job.LeaseToken = null;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
    }

    private static string NormalizeError(string error)
    {
        var normalized = string.IsNullOrWhiteSpace(error) ? "Judge job failed." : error.Trim();
        return normalized.Length <= 2048 ? normalized : normalized[..2048];
    }

    private static bool IsTerminal(JudgeStatus status) => status is
        JudgeStatus.Accepted or
        JudgeStatus.WrongAnswer or
        JudgeStatus.TimeLimitExceeded or
        JudgeStatus.MemoryLimitExceeded or
        JudgeStatus.RuntimeError or
        JudgeStatus.CompileError or
        JudgeStatus.SystemError;
}
