using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Application.Leaderboards.Services;

public interface ISeasonScoreService
{
    Task ApplySubmissionResultAsync(SeasonSubmissionResult submission, CancellationToken cancellationToken = default);
}
