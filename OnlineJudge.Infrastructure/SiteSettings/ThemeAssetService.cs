using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Services;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Infrastructure.SiteSettings;

public sealed class ThemeAssetService(
    IRuntimeStoragePathProvider storagePaths,
    ISecureUploadValidator uploadValidator,
    SecureUploadOptions uploadOptions,
    ISiteSettingsService siteSettingsService) : IThemeAssetService
{
    private const string AssetUrlPrefix = "/theme-assets/";

    public async Task<Result<ThemeAssetDto>> UploadAsync(UserRole currentUserRole, string originalFileName, string declaredContentType, long declaredLength, Stream content, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Root)
        {
            return Result<ThemeAssetDto>.Failure("Forbidden.");
        }

        var validation = await uploadValidator.ValidateAsync(new SecureUploadRequest
        {
            Policy = UploadPolicy.ThemeImage,
            OriginalFileName = originalFileName,
            DeclaredContentType = declaredContentType,
            DeclaredLength = declaredLength,
            Content = content
        }, cancellationToken);
        if (!validation.IsValid || validation.CanonicalExtension is null)
        {
            return Result<ThemeAssetDto>.Failure(validation.ErrorMessage ?? "Theme image validation failed.");
        }

        var assetId = $"{Guid.NewGuid():N}{validation.CanonicalExtension}";
        var size = await storagePaths.WriteThemeAssetAsync(assetId, content, uploadOptions.ImageMaxBytes, cancellationToken);
        return Result<ThemeAssetDto>.Success(new ThemeAssetDto
        {
            AssetId = assetId,
            Url = AssetUrlPrefix + assetId,
            ContentType = GetContentType(validation.CanonicalExtension),
            Size = size
        });
    }

    public async Task<Result> DeleteAsync(UserRole currentUserRole, string assetId, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Root)
        {
            return Result.Failure("Forbidden.");
        }

        string path;
        try
        {
            path = storagePaths.ResolveThemeAssetPath(assetId);
        }
        catch (InvalidDataException)
        {
            return Result.Failure("Theme asset id is invalid.");
        }

        if (!IsManagedAssetId(assetId))
        {
            return Result.Failure("Theme asset id is invalid.");
        }

        var appearance = await siteSettingsService.GetAppearanceAsync(cancellationToken);
        if (appearance.IsFailure || appearance.Value is null)
        {
            return Result.Failure("Theme appearance could not be read.");
        }

        if (IsReferenced(appearance.Value, assetId))
        {
            return Result.Failure("Theme asset is currently referenced.");
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Result.Success();
    }

    private static bool IsReferenced(SiteAppearanceDto appearance, string assetId)
    {
        return string.Equals(appearance.Background.Asset?.AssetId, assetId, StringComparison.Ordinal)
            || string.Equals(appearance.PanelSkin.BackgroundTexture?.AssetId, assetId, StringComparison.Ordinal)
            || string.Equals(appearance.PanelSkin.HeaderTexture?.AssetId, assetId, StringComparison.Ordinal)
            || string.Equals(appearance.PanelSkin.BorderTexture?.AssetId, assetId, StringComparison.Ordinal);
    }

    private static bool IsManagedAssetId(string assetId)
    {
        var extension = Path.GetExtension(assetId);
        return assetId.Length is >= 36 and <= 37
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(assetId), "N", out _)
            && extension is ".png" or ".jpg" or ".webp";
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => throw new InvalidOperationException("Unsupported canonical theme image extension.")
    };
}
