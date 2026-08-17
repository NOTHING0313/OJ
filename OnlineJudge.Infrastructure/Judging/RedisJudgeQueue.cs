using OnlineJudge.Application.Judging.Services;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Judging;

public class RedisJudgeQueue(IConnectionMultiplexer connectionMultiplexer) : IJudgeQueue
{
    private const string PendingSubmissionsKey = "judge:submissions:pending";

    public async Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var database = connectionMultiplexer.GetDatabase();
        await database.ListRightPushAsync(PendingSubmissionsKey, submissionId.ToString());
    }
}
