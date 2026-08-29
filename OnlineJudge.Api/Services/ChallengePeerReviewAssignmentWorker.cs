using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Application.Challenges.Services;

namespace OnlineJudge.Api.Services;

public sealed class ChallengePeerReviewAssignmentWorker : BackgroundService
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(60);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ChallengePeerReviewAssignmentWorker> logger;
    private readonly TimeSpan pollInterval;

    [ActivatorUtilitiesConstructor]
    public ChallengePeerReviewAssignmentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ChallengePeerReviewAssignmentWorker> logger)
        : this(scopeFactory, logger, DefaultPollInterval)
    {
    }

    public ChallengePeerReviewAssignmentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ChallengePeerReviewAssignmentWorker> logger,
        TimeSpan pollInterval)
    {
        if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        this.scopeFactory = scopeFactory;
        this.logger = logger;
        this.pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(pollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IChallengePeerReviewService>();
                await service.EnsureAssignmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Challenge peer review assignment reconciliation failed; the next poll will retry.");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
