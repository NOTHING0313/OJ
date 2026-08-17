using OnlineJudge.Application.Common;
using OnlineJudge.Application.Profile.Dtos;

namespace OnlineJudge.Application.Profile.Services;

public interface IProfileService
{
    Task<Result<ProfileSummaryDto>> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<Result<ProfileSummaryDto>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
