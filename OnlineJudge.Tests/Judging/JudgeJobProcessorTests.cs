using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.JudgeWorker;

namespace OnlineJudge.Tests.Judging;

public sealed class JudgeJobProcessorTests
{
    [Fact]
    public async Task AcceptedResult_CompletesJobAndPersistsSnapshots()
    {
        var result = new JudgeResult
        {
            Status = JudgeStatus.Accepted,
            TimeUsedMs = 12,
            MemoryUsedKb = 256,
            CaseResults =
            [
                new JudgeCaseResult
                {
                    TestCaseId = Guid.Empty,
                    Status = JudgeStatus.Accepted,
                    TimeUsedMs = 12,
                    MemoryUsedKb = 256,
                    ActualOutput = "42"
                }
            ]
        };
        await using var fixture = await Fixture.CreateAsync(result);
        result.CaseResults[0].TestCaseId = fixture.TestCaseId;

        await fixture.Processor.ProcessAsync(fixture.Lease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var job = await fixture.Db.JudgeJobs.SingleAsync();
        var submission = await fixture.Db.Submissions.Include(item => item.CaseResults).SingleAsync();
        Assert.Equal(JudgeJobStatus.Completed, job.Status);
        Assert.Null(job.LeaseToken);
        Assert.Equal(JudgeStatus.Accepted, submission.Status);
        var caseResult = Assert.Single(submission.CaseResults);
        Assert.Equal("42", caseResult.ExpectedOutputSnapshot);
        Assert.Equal(100, caseResult.ScoreSnapshot);
    }

    [Fact]
    public async Task TransientSystemError_RequeuesAndSignalsWithoutPersistingResult()
    {
        await using var fixture = await Fixture.CreateAsync(new JudgeResult
        {
            Status = JudgeStatus.SystemError,
            FailureKind = JudgeFailureKind.TransientInfrastructure,
            ErrorMessage = "temporary"
        });

        await fixture.Processor.ProcessAsync(fixture.Lease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var job = await fixture.Db.JudgeJobs.SingleAsync();
        var submission = await fixture.Db.Submissions.SingleAsync();
        Assert.Equal(JudgeJobStatus.Pending, job.Status);
        Assert.Equal(JudgeFailureKind.TransientInfrastructure, job.LastFailureKind);
        Assert.Equal(JudgeStatus.Pending, submission.Status);
        Assert.Equal(1, fixture.Queue.SignalCount);
    }

    [Fact]
    public async Task PermanentSystemError_DeadLettersImmediately()
    {
        await using var fixture = await Fixture.CreateAsync(new JudgeResult
        {
            Status = JudgeStatus.SystemError,
            FailureKind = JudgeFailureKind.PermanentConfiguration,
            ErrorMessage = "invalid function definition"
        });

        await fixture.Processor.ProcessAsync(fixture.Lease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(JudgeJobStatus.DeadLettered, (await fixture.Db.JudgeJobs.SingleAsync()).Status);
        var submission = await fixture.Db.Submissions.SingleAsync();
        Assert.Equal(JudgeStatus.SystemError, submission.Status);
        Assert.Equal("Judge configuration is invalid.", submission.ErrorMessage);
        Assert.Equal(0, fixture.Queue.SignalCount);
    }

    [Fact]
    public async Task StaleLease_CannotPersistJudgeResult()
    {
        await using var fixture = await Fixture.CreateAsync(new JudgeResult { Status = JudgeStatus.Accepted });
        var staleLease = fixture.Lease with { LeaseToken = Guid.NewGuid() };

        await fixture.Processor.ProcessAsync(staleLease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var job = await fixture.Db.JudgeJobs.SingleAsync();
        var submission = await fixture.Db.Submissions.SingleAsync();
        Assert.Equal(JudgeJobStatus.Leased, job.Status);
        Assert.Equal(fixture.Lease.LeaseToken, job.LeaseToken);
        Assert.Equal(JudgeStatus.Judging, submission.Status);
    }

    [Fact]
    public async Task LastTransientAttempt_DeadLettersInsteadOfRequeueing()
    {
        await using var fixture = await Fixture.CreateAsync(
            new JudgeResult
            {
                Status = JudgeStatus.SystemError,
                FailureKind = JudgeFailureKind.TransientInfrastructure
            },
            attemptNumber: 3);

        await fixture.Processor.ProcessAsync(fixture.Lease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        Assert.Equal(JudgeJobStatus.DeadLettered, (await fixture.Db.JudgeJobs.SingleAsync()).Status);
        Assert.Equal(JudgeStatus.SystemError, (await fixture.Db.Submissions.SingleAsync()).Status);
        Assert.Equal(0, fixture.Queue.SignalCount);
    }

    [Fact]
    public async Task CompletionFailure_DiscardsTrackedResultBeforeRequeueing()
    {
        var result = new JudgeResult
        {
            Status = JudgeStatus.Accepted,
            CaseResults =
            [
                new JudgeCaseResult
                {
                    TestCaseId = Guid.Empty,
                    Status = JudgeStatus.Accepted,
                    ActualOutput = "42"
                }
            ]
        };
        await using var fixture = await Fixture.CreateAsync(result, seasonScoreServiceFactory: _ => new ThrowingSeasonScoreService());
        result.CaseResults[0].TestCaseId = fixture.TestCaseId;

        await fixture.Processor.ProcessAsync(fixture.Lease, CancellationToken.None);

        fixture.Db.ChangeTracker.Clear();
        var job = await fixture.Db.JudgeJobs.SingleAsync();
        var submission = await fixture.Db.Submissions.Include(item => item.CaseResults).SingleAsync();
        Assert.Equal(JudgeJobStatus.Pending, job.Status);
        Assert.Equal(JudgeFailureKind.TransientInfrastructure, job.LastFailureKind);
        Assert.Equal(JudgeStatus.Pending, submission.Status);
        Assert.Null(submission.FinishedAt);
        Assert.Empty(submission.CaseResults);
        Assert.Equal(1, fixture.Queue.SignalCount);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            OnlineJudgeDbContext db,
            ServiceProvider provider,
            JudgeJobProcessor processor,
            RecordingQueue queue,
            JudgeJobLease lease,
            Guid testCaseId)
        {
            Db = db;
            Provider = provider;
            Processor = processor;
            Queue = queue;
            Lease = lease;
            TestCaseId = testCaseId;
        }

        public OnlineJudgeDbContext Db { get; }

        public ServiceProvider Provider { get; }

        public JudgeJobProcessor Processor { get; }

        public RecordingQueue Queue { get; }

        public JudgeJobLease Lease { get; }

        public Guid TestCaseId { get; }

        public static async Task<Fixture> CreateAsync(
            JudgeResult result,
            int attemptNumber = 1,
            Func<OnlineJudgeDbContext, ISeasonScoreService>? seasonScoreServiceFactory = null)
        {
            var now = new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
            var time = new FixedTimeProvider(now);
            var dbOptions = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var db = new OnlineJudgeDbContext(dbOptions);
            var provider = new ServiceCollection().BuildServiceProvider();
            var problemId = Guid.NewGuid();
            var revisionId = Guid.NewGuid();
            var testCaseId = Guid.NewGuid();
            var submissionId = Guid.NewGuid();
            var leaseToken = Guid.NewGuid();
            var userId = Guid.NewGuid();

            db.Users.Add(new User
            {
                Id = userId,
                UserName = "answerer",
                Email = "answerer@example.test",
                PasswordHash = "hash",
                Role = UserRole.Answerer,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.Problems.Add(new Problem
            {
                Id = problemId,
                Title = "Problem",
                Description = "Description",
                InputDescription = "Input",
                OutputDescription = "Output",
                TimeLimitMs = 1000,
                MemoryLimitMb = 128,
                IsPublished = true,
                CreatedByUserId = userId,
                CreatedAt = now,
                UpdatedAt = now,
                CurrentJudgeRevisionId = revisionId
            });
            db.TestCases.Add(new TestCase
            {
                Id = testCaseId,
                ProblemId = problemId,
                Input = "42",
                ExpectedOutput = "42",
                Visibility = TestCaseVisibility.Sample,
                Score = 100,
                CreatedAt = now
            });
            db.ProblemJudgeRevisions.Add(new ProblemJudgeRevision
            {
                Id = revisionId,
                ProblemId = problemId,
                RevisionNumber = 1,
                JudgeMode = JudgeMode.StandardInputOutput,
                TimeLimitMs = 1000,
                MemoryLimitMb = 128,
                CreatedAt = now,
                TestCases =
                [
                    new ProblemJudgeRevisionTestCase
                    {
                        Id = Guid.NewGuid(),
                        ProblemJudgeRevisionId = revisionId,
                        SourceTestCaseId = testCaseId,
                        Order = 0,
                        Input = "42",
                        ExpectedOutput = "42",
                        Visibility = TestCaseVisibility.Sample,
                        Score = 100
                    }
                ]
            });
            db.Submissions.Add(new Submission
            {
                Id = submissionId,
                ProblemId = problemId,
                ProblemJudgeRevisionId = revisionId,
                UserId = userId,
                Language = JudgeLanguage.Cpp17,
                SourceCode = "int main(){}",
                Status = JudgeStatus.Judging,
                CreatedAt = now
            });
            db.JudgeJobs.Add(new JudgeJob
            {
                SubmissionId = submissionId,
                Status = JudgeJobStatus.Leased,
                AttemptCount = attemptNumber,
                AvailableAt = now,
                LeaseToken = leaseToken,
                LeaseOwner = "worker-a",
                LeaseExpiresAt = now.AddMinutes(2),
                LastAttemptStartedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var jobOptions = new JudgeJobOptions();
            var store = new JudgeJobStore(db, jobOptions, time);
            var queue = new RecordingQueue();
            var processor = new JudgeJobProcessor(
                db,
                new FixedRunnerFactory(new FixedRunner(result)),
                new EmptyAssetLoader(),
                seasonScoreServiceFactory?.Invoke(db) ?? new SeasonScoreService(db, time, new LeaderboardScoringEngine()),
                new NoopSeasonLifecycleService(),
                new NoopSandboxMaintenance(),
                store,
                queue,
                jobOptions,
                time,
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<JudgeJobProcessor>.Instance);

            return new Fixture(
                db,
                provider,
                processor,
                queue,
                new JudgeJobLease(submissionId, leaseToken, attemptNumber, now.AddMinutes(2)),
                testCaseId);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Provider.DisposeAsync();
        }
    }

    private sealed class FixedRunnerFactory(IJudgeRunner runner) : IJudgeRunnerFactory
    {
        public IJudgeRunner GetRunner(JudgeLanguage language) => runner;
    }

    private sealed class FixedRunner(JudgeResult result) : IJudgeRunner
    {
        public bool Supports(JudgeLanguage language) => true;

        public Task<JudgeResult> RunAsync(JudgeRequest request, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }

    private sealed class EmptyAssetLoader : IJudgeCompileAssetLoader
    {
        public Task<IReadOnlyList<JudgeCompileAsset>> LoadAsync(Guid problemId, JudgeLanguage language, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JudgeCompileAsset>>([]);

        public Task<IReadOnlyList<JudgeCompileAsset>> LoadRevisionAsync(Guid problemJudgeRevisionId, JudgeLanguage language, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JudgeCompileAsset>>([]);
    }

    private sealed class RecordingQueue : IJudgeQueue
    {
        public int SignalCount { get; private set; }

        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            SignalCount++;
            return Task.FromResult(true);
        }

        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JudgeQueueReadResult.Empty);
    }

    private sealed class NoopSeasonLifecycleService : ILeaderboardSeasonLifecycleService
    {
        public Task ReconcileCurrentSeasonAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshPublicSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingSeasonScoreService : ISeasonScoreService
    {
        public Task<SeasonScoreApplyResult> ApplySubmissionResultAsync(
            SeasonSubmissionResult result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Injected completion failure.");
    }

    private sealed class NoopSandboxMaintenance : IJudgeSandboxMaintenance
    {
        public Task<int> ReconcileStaleContainersAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ReconcileSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
