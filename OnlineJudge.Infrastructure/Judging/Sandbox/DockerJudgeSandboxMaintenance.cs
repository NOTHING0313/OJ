using OnlineJudge.Application.Judging.Services;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

internal sealed class DockerJudgeSandboxMaintenance(IDockerCommandClient dockerCommandClient) : IJudgeSandboxMaintenance
{
    public Task<int> ReconcileStaleContainersAsync(CancellationToken cancellationToken = default) =>
        dockerCommandClient.RemoveManagedContainersAsync(cancellationToken);
}
