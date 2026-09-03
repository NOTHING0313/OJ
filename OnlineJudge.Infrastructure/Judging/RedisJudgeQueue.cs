using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Judging;

public class RedisJudgeQueue(
    IConnectionMultiplexer connectionMultiplexer,
    JudgeJobOptions options,
    ILogger<RedisJudgeQueue> logger) : IJudgeQueue
{
    private const string PendingSubmissionsKey = "judge:submissions:pending";

    public async Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await WithTimeoutAsync(
                connectionMultiplexer.GetDatabase().ListRightPushAsync(PendingSubmissionsKey, submissionId.ToString()),
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Judge wake-up signal could not be published. SubmissionId={SubmissionId}", submissionId);
            return false;
        }
    }

    public async Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var value = await WithTimeoutAsync(
                connectionMultiplexer.GetDatabase().ListLeftPopAsync(PendingSubmissionsKey),
                cancellationToken);
            if (!value.HasValue)
            {
                return JudgeQueueReadResult.Empty;
            }

            if (!Guid.TryParse(value.ToString(), out var submissionId))
            {
                logger.LogWarning("Discarding invalid judge wake-up signal. Value={Value}", value.ToString());
                return JudgeQueueReadResult.Empty;
            }

            return new JudgeQueueReadResult(true, submissionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Judge wake-up signal store is unavailable; database polling remains active.");
            return JudgeQueueReadResult.Unavailable;
        }
    }

    private async Task<T> WithTimeoutAsync<T>(Task<T> operation, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.RedisSignalTimeout);
        return await operation.WaitAsync(timeout.Token);
    }
}
