using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeQueue
{
    /// <summary>
    /// Attempts to publish a non-authoritative wake-up hint for a persisted judge job.
    /// </summary>
    Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to read one wake-up hint. Database job claiming remains authoritative.
    /// </summary>
    Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default);
}
