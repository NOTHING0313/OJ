using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Judging;

public sealed class JudgeJobStoreTests
{
    [Fact]
    public async Task ClaimAndRenew_UseFencedLeaseAndUpdateSubmissionState()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
        var time = new MutableTimeProvider(now);
        var submissionId = SeedPendingJob(db, now);
        await db.SaveChangesAsync();
        var store = new JudgeJobStore(db, new JudgeJobOptions(), time);

        var lease = await store.TryClaimAsync(submissionId, "worker-a");

        Assert.NotNull(lease);
        Assert.Equal(1, lease.AttemptNumber);
        Assert.Equal(JudgeJobStatus.Leased, (await db.JudgeJobs.SingleAsync()).Status);
        Assert.Equal(JudgeStatus.Judging, (await db.Submissions.SingleAsync()).Status);

        time.Now = now.AddSeconds(30);
        var renewal = await store.RenewLeaseAsync(lease);
        Assert.Equal(JudgeJobTransitionResult.Applied, renewal.Transition);
        Assert.Equal(time.Now.AddSeconds(120), renewal.LeaseExpiresAt);
    }

    [Fact]
    public async Task ExpiredLease_IsReclaimedAndRejectsStaleOwner()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
        var time = new MutableTimeProvider(now);
        var submissionId = SeedPendingJob(db, now);
        await db.SaveChangesAsync();
        var store = new JudgeJobStore(db, new JudgeJobOptions(), time);
        var first = (await store.TryClaimAsync(submissionId, "worker-a"))!;

        time.Now = first.LeaseExpiresAt.AddMilliseconds(1);
        var second = (await store.TryClaimAsync(submissionId, "worker-b"))!;

        Assert.NotEqual(first.LeaseToken, second.LeaseToken);
        Assert.Equal(2, second.AttemptNumber);
        Assert.Equal(
            JudgeJobTransitionResult.LeaseLost,
            await store.RequeueAsync(first, JudgeFailureKind.TransientInfrastructure, "stale", TimeSpan.Zero));
        Assert.Equal(JudgeJobStatus.Leased, (await db.JudgeJobs.SingleAsync()).Status);
        Assert.Equal(second.LeaseToken, (await db.JudgeJobs.SingleAsync()).LeaseToken);
    }

    [Fact]
    public async Task Requeue_ClearsLeaseAndSchedulesNextAttempt()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
        var time = new MutableTimeProvider(now);
        var submissionId = SeedPendingJob(db, now);
        await db.SaveChangesAsync();
        var store = new JudgeJobStore(db, new JudgeJobOptions(), time);
        var lease = (await store.TryClaimAsync(submissionId, "worker-a"))!;

        var result = await store.RequeueAsync(
            lease,
            JudgeFailureKind.TransientInfrastructure,
            "temporary failure",
            TimeSpan.FromSeconds(5));

        var job = await db.JudgeJobs.SingleAsync();
        Assert.Equal(JudgeJobTransitionResult.Applied, result);
        Assert.Equal(JudgeJobStatus.Pending, job.Status);
        Assert.Equal(now.AddSeconds(5), job.AvailableAt);
        Assert.Null(job.LeaseToken);
        Assert.Equal(JudgeStatus.Pending, (await db.Submissions.SingleAsync()).Status);
    }

    [Fact]
    public async Task ExpiredLastAttempt_IsDeadLettered()
    {
        await using var db = CreateDb();
        var now = new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
        var time = new MutableTimeProvider(now);
        var submissionId = SeedPendingJob(db, now);
        await db.SaveChangesAsync();
        var options = new JudgeJobOptions { LeaseDurationSeconds = 10, MaxAttempts = 1 };
        var store = new JudgeJobStore(db, options, time);
        var lease = (await store.TryClaimAsync(submissionId, "worker-a"))!;

        time.Now = lease.LeaseExpiresAt.AddMilliseconds(1);
        Assert.Null(await store.TryClaimAsync(submissionId, "worker-b"));

        var job = await db.JudgeJobs.SingleAsync();
        var submission = await db.Submissions.SingleAsync();
        Assert.Equal(JudgeJobStatus.DeadLettered, job.Status);
        Assert.Equal(JudgeStatus.SystemError, submission.Status);
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public void Options_RejectHeartbeatLongerThanOneThirdOfLease()
    {
        var values = new Dictionary<string, string?>
        {
            ["JudgeJobs:LeaseDurationSeconds"] = "60",
            ["JudgeJobs:HeartbeatIntervalSeconds"] = "21"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var exception = Assert.Throws<InvalidOperationException>(() => JudgeJobOptions.FromConfiguration(configuration));

        Assert.Contains("one third", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static Guid SeedPendingJob(OnlineJudgeDbContext db, DateTimeOffset now)
    {
        var submissionId = Guid.NewGuid();
        db.Submissions.Add(new Submission
        {
            Id = submissionId,
            ProblemId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}",
            Status = JudgeStatus.Pending,
            CreatedAt = now
        });
        db.JudgeJobs.Add(new JudgeJob
        {
            SubmissionId = submissionId,
            Status = JudgeJobStatus.Pending,
            AvailableAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });
        return submissionId;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
