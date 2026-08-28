using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Application.Leaderboards.Services;

public interface ISeasonScoreService
{
    Task<SeasonScoreApplyResult> ApplySubmissionResultAsync(SeasonSubmissionResult submission, CancellationToken cancellationToken = default);
}

public sealed record SeasonScoreApplyResult(bool Applied, Guid? SeasonId, bool RequiresArchiveRefresh)
{
    public static SeasonScoreApplyResult Ignored { get; } = new(false, null, false);
}
