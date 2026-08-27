using OnlineJudge.Application.Common;
using OnlineJudge.Application.Leaderboards.Dtos;

namespace OnlineJudge.Application.Leaderboards.Services;

public interface ILeaderboardService
{
    Task<Result<GlobalUserLeaderboardDto>> GetGlobalUserLeaderboardAsync(CancellationToken cancellationToken = default);

    Task<Result<RankHistoryDto>> GetGlobalUserRankHistoryAsync(int days = 10, CancellationToken cancellationToken = default);

    Task<Result<ChallengeLeaderboardIndexDto>> GetChallengeLeaderboardIndexAsync(CancellationToken cancellationToken = default);
}
