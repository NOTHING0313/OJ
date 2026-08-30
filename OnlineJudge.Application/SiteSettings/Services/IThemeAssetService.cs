using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.SiteSettings.Services;

public interface IThemeAssetService
{
    Task<Result<ThemeAssetDto>> UploadAsync(UserRole currentUserRole, string originalFileName, string declaredContentType, long declaredLength, Stream content, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(UserRole currentUserRole, string assetId, CancellationToken cancellationToken = default);
}
