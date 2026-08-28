using System.Collections.Concurrent;

namespace OnlineJudge.Infrastructure.Teams;

public sealed class TeamGitSyncLockProvider
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> locks = new();

    public SemaphoreSlim Get(Guid projectId) => locks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
}
