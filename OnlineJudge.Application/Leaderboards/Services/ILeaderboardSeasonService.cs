using OnlineJudge.Application.Common;
using OnlineJudge.Application.Leaderboards.Dtos;
using OnlineJudge.Application.Leaderboards.Requests;

namespace OnlineJudge.Application.Leaderboards.Services;

public interface ILeaderboardSeasonService
{
    Task<Result<SeasonLeaderboardDto>> GetCurrentLeaderboardAsync(CancellationToken cancellationToken = default);

    Task<Result<SeasonLeaderboardDto>> GetCurrentAuditLeaderboardAsync(CancellationToken cancellationToken = default);

    Task<Result<SeasonProblemLeaderboardDto>> GetCurrentProblemLeaderboardAsync(Guid problemId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LeaderboardSeasonDto>>> GetSeasonsAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> CreateSeasonAsync(CreateLeaderboardSeasonRequest request, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> UpdateSeasonAsync(Guid seasonId, UpdateLeaderboardSeasonRequest request, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> AddProblemAsync(Guid seasonId, AddLeaderboardSeasonProblemRequest request, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> UpdateProblemBenchmarkAsync(
        Guid seasonId,
        Guid problemId,
        OnlineJudge.Domain.Enums.JudgeLanguage language,
        UpdateLeaderboardSeasonProblemBenchmarkRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> RemoveProblemAsync(Guid seasonId, Guid problemId, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> FreezeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonArchiveDto>> FinalizeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonDto>> ArchiveSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonArchiveDto>> GetArchiveAsync(Guid seasonId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LeaderboardSeasonHistorySummaryDto>>> GetHistoryAsync(CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonArchiveDto>> GetHistoryAsync(Guid seasonId, CancellationToken cancellationToken = default);

    Task<Result<LeaderboardSeasonPersonalDto>> GetCurrentPersonalAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<LeaderboardSeasonPersonalHistoryDto>>> GetPersonalHistoryAsync(CancellationToken cancellationToken = default);
}
