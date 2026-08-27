using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Leaderboards.Dtos;

namespace OnlineJudge.Application.Challenges.Services;

public interface IChallengeService
{
    Task<Result<IReadOnlyList<ChallengeListItemDto>>> GetChallengesAsync(CancellationToken cancellationToken = default);

    Task<Result<ChallengeDetailDto>> GetChallengeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ChallengeLeaderboardDto>> GetLeaderboardAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeLeaderboardProgressDto>> GetLeaderboardProgressAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<RankHistoryDto>> GetLeaderboardHistoryAsync(Guid challengeId, int days = 10, CancellationToken cancellationToken = default);

    Task<Result<ChallengeAdminSummaryDto>> GetAdminSummaryAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeCsvExportResult>> ExportAdminUsersCsvAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeCsvExportResult>> ExportAdminTasksCsvAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeFileDownloadDto>> GetFileSubmissionDownloadAsync(Guid challengeId, Guid fileSubmissionId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeTaskFileSubmissionDto?>> GetMyFileSubmissionAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default);

    Task<Result> JoinChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeDetailDto>> CreateChallengeAsync(CreateChallengeRequest request, CancellationToken cancellationToken = default);

    Task<Result<ChallengeDetailDto>> UpdateChallengeAsync(Guid id, UpdateChallengeRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteChallengeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ChallengeTaskDto>> AddTaskAsync(Guid challengeId, CreateChallengeTaskRequest request, CancellationToken cancellationToken = default);

    Task<Result<ChallengeTaskDto>> UpdateTaskAsync(Guid challengeId, Guid taskId, UpdateChallengeTaskRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteTaskAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default);

    Task<Result<ChallengeTaskFileSubmissionDto>> SubmitFileAnswerAsync(Guid challengeId, Guid taskId, SubmitChallengeTaskFileRequest request, CancellationToken cancellationToken = default);

    Task<Result> WithdrawMyFileSubmissionAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default);

    Task<Result> ReviewFileSubmissionAsync(Guid challengeId, Guid fileSubmissionId, ReviewChallengeFileSubmissionRequest request, CancellationToken cancellationToken = default);
}
