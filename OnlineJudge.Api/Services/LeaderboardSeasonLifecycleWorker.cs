using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Infrastructure.Leaderboards;

namespace OnlineJudge.Api.Services;

public sealed class LeaderboardSeasonLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    LeaderboardSeasonLifecycleOptions options,
    ILogger<LeaderboardSeasonLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Leaderboard season lifecycle worker is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.PollIntervalSeconds));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ILeaderboardSeasonLifecycleService>();
                await service.ReconcileCurrentSeasonAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Leaderboard season lifecycle reconciliation failed; the next poll will retry.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
