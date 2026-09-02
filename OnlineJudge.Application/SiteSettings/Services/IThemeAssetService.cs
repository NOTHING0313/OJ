using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.SiteSettings.Services;

public interface IThemeAssetService
{
    Task<Result<IReadOnlyList<ThemeAssetLibraryItemDto>>> ListAsync(UserRole currentUserRole, CancellationToken cancellationToken = default);

    Task<Result<ThemeAssetDto>> UploadAsync(Guid userId, UserRole currentUserRole, string originalFileName, string declaredContentType, long declaredLength, Stream content, CancellationToken cancellationToken = default);

    Task<Result<ThemeAssetDto>> RenameAsync(Guid userId, UserRole currentUserRole, string assetId, string displayName, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid userId, UserRole currentUserRole, string assetId, CancellationToken cancellationToken = default);
}
