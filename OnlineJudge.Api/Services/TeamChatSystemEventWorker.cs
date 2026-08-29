using OnlineJudge.Application.Teams.Services;

namespace OnlineJudge.Api.Services;

public sealed class TeamChatSystemEventWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<TeamChatSystemEventWorker> logger;

    public TeamChatSystemEventWorker(IServiceScopeFactory scopeFactory, ILogger<TeamChatSystemEventWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<ITeamChatSystemEventReconciler>();
                await reconciler.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Team chat system event reconciliation failed; the next poll will retry.");
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
