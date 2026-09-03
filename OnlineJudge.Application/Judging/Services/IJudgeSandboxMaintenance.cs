namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeSandboxMaintenance
{
    Task<int> ReconcileStaleContainersAsync(CancellationToken cancellationToken = default);

    Task<int> ReconcileSubmissionContainersAsync(Guid submissionId, CancellationToken cancellationToken = default);
}
