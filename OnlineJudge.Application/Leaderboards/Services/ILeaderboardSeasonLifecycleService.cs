namespace OnlineJudge.Application.Leaderboards.Services;

public interface ILeaderboardSeasonLifecycleService
{
    Task ReconcileCurrentSeasonAsync(CancellationToken cancellationToken = default);

    Task RefreshPublicSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default);
}
