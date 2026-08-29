namespace OnlineJudge.Application.Teams.Services;

public interface ITeamChatSystemEventReconciler
{
    Task ReconcileAsync(CancellationToken cancellationToken = default);
}
