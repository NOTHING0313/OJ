namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeQueue
{
    Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);
}
