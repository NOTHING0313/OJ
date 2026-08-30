namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeSandboxMaintenance
{
    Task<int> ReconcileStaleContainersAsync(CancellationToken cancellationToken = default);
}
