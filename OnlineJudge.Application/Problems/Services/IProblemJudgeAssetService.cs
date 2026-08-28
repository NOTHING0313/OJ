using OnlineJudge.Application.Common;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;

namespace OnlineJudge.Application.Problems.Services;

public interface IProblemJudgeAssetService
{
    Task<Result<IReadOnlyList<ProblemJudgeAssetDto>>> GetAssetsAsync(Guid problemId, CancellationToken cancellationToken = default);

    Task<Result<ProblemJudgeAssetDto>> CreateAssetAsync(Guid problemId, CreateProblemJudgeAssetRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAssetAsync(Guid problemId, Guid assetId, CancellationToken cancellationToken = default);
}
