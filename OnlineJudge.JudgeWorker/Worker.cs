using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Infrastructure.Judging;

namespace OnlineJudge.JudgeWorker;

internal sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IJudgeQueue judgeQueue,
    JudgeJobOptions jobOptions,
    JudgeWorkerOptions workerOptions,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileJudgeContainersAsync(stoppingToken);
        logger.LogInformation("Judge worker started. Concurrency={Concurrency}", workerOptions.Concurrency);

        var consumers = Enumerable.Range(1, workerOptions.Concurrency)
            .Select(slot => RunConsumerAsync(CreateWorkerId(slot), stoppingToken))
            .ToArray();
        await Task.WhenAll(consumers);
    }

    private async Task RunConsumerAsync(string workerId, CancellationToken stoppingToken)
    {
        logger.LogInformation("Judge worker consumer started. WorkerId={WorkerId}", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var signal = await judgeQueue.TryDequeueSubmissionAsync(stoppingToken);
                var lease = signal.SubmissionId.HasValue
                    ? await TryClaimAsync(signal.SubmissionId.Value, workerId, stoppingToken)
                    : null;
                lease ??= await TryClaimAsync(null, workerId, stoppingToken);

                if (lease is null)
                {
                    await Task.Delay(jobOptions.PollInterval, stoppingToken);
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<JudgeJobProcessor>();
                await processor.ProcessAsync(lease, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Judge worker loop failed. WorkerId={WorkerId}", workerId);
                await DelayAfterFailureAsync(stoppingToken);
            }
        }

        logger.LogInformation("Judge worker consumer stopped. WorkerId={WorkerId}", workerId);
    }

    private async Task<JudgeJobLease?> TryClaimAsync(Guid? preferredSubmissionId, string workerId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IJudgeJobStore>();
        return await store.TryClaimAsync(preferredSubmissionId, workerId, cancellationToken);
    }

    private async Task ReconcileJudgeContainersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var maintenance = scope.ServiceProvider.GetRequiredService<IJudgeSandboxMaintenance>();
            var removed = await maintenance.ReconcileStaleContainersAsync(cancellationToken);
            logger.LogInformation("Judge sandbox startup reconciliation completed. RemovedContainers={RemovedContainers}", removed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Judge sandbox startup reconciliation failed.");
        }
    }

    private async Task DelayAfterFailureAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(jobOptions.PollInterval, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string CreateWorkerId(int slot)
    {
        var value = $"{Environment.MachineName}:{Environment.ProcessId}:{slot}:{Guid.NewGuid():N}";
        return value.Length <= 200 ? value : value[..200];
    }
}
