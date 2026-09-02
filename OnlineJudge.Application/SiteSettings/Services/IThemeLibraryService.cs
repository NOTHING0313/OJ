using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.SiteSettings.Services;

public interface IThemeLibraryService
{
    Task<Result<ThemePresetListDto>> ListAsync(UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePresetDto>> CreateAsync(CreateThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePresetDto>> UpdateAsync(Guid presetId, UpdateThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePresetDto>> DuplicateAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePresetDto>> RenameAsync(Guid presetId, RenameThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<SiteAppearanceDto>> ApplyAsync(Guid? presetId, Guid userId, UserRole role, string? requestHost, CancellationToken cancellationToken = default);

    Task<Result<ThemePackExportDto>> ExportAsync(Guid presetId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePackPreflightDto>> PreflightImportAsync(string fileName, string contentType, long length, Stream content, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<ThemePresetDto>> ImportAsync(string fileName, string contentType, long length, Stream content, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>> GetAssetReferencesAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyDictionary<string, string>>> GetAssetDisplayNamesAsync(CancellationToken cancellationToken = default);

    Task<Result<string>> RegisterAssetDisplayNameAsync(string assetId, string originalFileName, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result<string>> RenameAssetDisplayNameAsync(string assetId, string displayName, Guid userId, UserRole role, CancellationToken cancellationToken = default);

    Task<Result> RemoveAssetDisplayNameAsync(string assetId, Guid userId, UserRole role, CancellationToken cancellationToken = default);
}
