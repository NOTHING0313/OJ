using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.SiteSettings.Services;

public interface ISiteSettingsService
{
    Task<Result<SiteAppearanceDto>> GetAppearanceAsync(CancellationToken cancellationToken = default);

    SiteAppearanceDto GetDefaultAppearance();

    Task<Result<SiteAppearanceDto>> ValidateAppearanceAsync(UpdateSiteAppearanceRequest request, string? requestHost = null, CancellationToken cancellationToken = default);

    Task<Result<SiteAppearanceDto>> UpdateAppearanceAsync(UpdateSiteAppearanceRequest request, Guid currentUserId, UserRole currentUserRole, string? requestHost = null, CancellationToken cancellationToken = default);
}
