using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Common;

namespace OnlineJudge.Application.Challenges.Services;

public interface IChallengePeerReviewService
{
    Task EnsureAssignmentsAsync(CancellationToken cancellationToken = default);
    Task<Result<ChallengePeerReviewWorkspaceDto>> GetMyWorkspaceAsync(Guid challengeId, CancellationToken cancellationToken = default);
    Task<Result<ChallengePeerReviewWorkspaceDto>> SaveDraftAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default);
    Task<Result<ChallengePeerReviewWorkspaceDto>> SubmitAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default);
    Task<Result<ChallengePeerReviewAdminSummaryDto>> GetAdminAuditAsync(Guid challengeId, CancellationToken cancellationToken = default);
}
