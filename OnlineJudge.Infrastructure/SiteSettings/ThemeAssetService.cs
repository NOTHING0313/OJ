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
    ISiteSettingsService siteSettingsService,
    IThemeLibraryService? themeLibraryService = null) : IThemeAssetService
{
    private const string AssetUrlPrefix = "/theme-assets/";

    public async Task<Result<IReadOnlyList<ThemeAssetLibraryItemDto>>> ListAsync(UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Root)
        {
            return Result<IReadOnlyList<ThemeAssetLibraryItemDto>>.Failure("Forbidden.");
        }

        var appearance = await siteSettingsService.GetAppearanceAsync(cancellationToken);
        if (appearance.IsFailure || appearance.Value is null)
        {
            return Result<IReadOnlyList<ThemeAssetLibraryItemDto>>.Failure("Theme appearance could not be read.");
        }

        if (!Directory.Exists(storagePaths.ThemeAssetsRoot))
        {
            return Result<IReadOnlyList<ThemeAssetLibraryItemDto>>.Success([]);
        }

        var presetReferences = themeLibraryService is null
            ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            : (await themeLibraryService.GetAssetReferencesAsync(cancellationToken)).Value
                ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var assets = Directory.EnumerateFiles(storagePaths.ThemeAssetsRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => IsManagedAssetId(file.Name))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new ThemeAssetLibraryItemDto
            {
                AssetId = file.Name,
                Url = AssetUrlPrefix + file.Name,
                ContentType = GetContentType(file.Extension.ToLowerInvariant()),
                Size = file.Length,
                UsedBy = GetReferences(appearance.Value, file.Name)
                    .Concat(presetReferences.GetValueOrDefault(file.Name) ?? [])
                    .ToArray()
            })
            .ToArray();

        return Result<IReadOnlyList<ThemeAssetLibraryItemDto>>.Success(assets);
    }

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

        var presetReferences = themeLibraryService is null
            ? null
            : (await themeLibraryService.GetAssetReferencesAsync(cancellationToken)).Value;
        if (GetReferences(appearance.Value, assetId).Count > 0
            || presetReferences?.ContainsKey(assetId) == true)
        {
            return Result.Failure("Theme asset is currently referenced.");
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Result.Success();
    }

    private static IReadOnlyList<string> GetReferences(SiteAppearanceDto appearance, string assetId)
    {
        var references = new List<string>();
        AddReference(references, "background", appearance.Background.Asset, assetId);
        AddReference(references, "panelBackground", appearance.PanelSkin.BackgroundTexture, assetId);
        AddReference(references, "panelHeader", appearance.PanelSkin.HeaderTexture, assetId);
        AddReference(references, "panelBorder", appearance.PanelSkin.BorderTexture, assetId);

        foreach (var (slot, assignment) in appearance.Icons)
        {
            AddReference(references, $"icon:{slot}", assignment?.Asset, assetId);
        }

        foreach (var (slot, assignment) in appearance.Decorations)
        {
            AddReference(references, $"decoration:{slot}", assignment?.Asset, assetId);
        }

        foreach (var (page, background) in appearance.Pages)
        {
            if (string.Equals(background.ImageUrl, AssetUrlPrefix + assetId, StringComparison.Ordinal))
            {
                references.Add($"page:{page}");
            }
        }

        return references;
    }

    private static void AddReference(ICollection<string> references, string slot, ThemeAssetReferenceDto? asset, string assetId)
    {
        if (string.Equals(asset?.AssetId, assetId, StringComparison.Ordinal))
        {
            references.Add(slot);
        }
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
