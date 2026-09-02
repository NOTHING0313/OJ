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
        var displayNames = themeLibraryService is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : (await themeLibraryService.GetAssetDisplayNamesAsync(cancellationToken)).Value
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

        var assets = Directory.EnumerateFiles(storagePaths.ThemeAssetsRoot, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .Where(file => IsManagedAssetId(file.Name))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new ThemeAssetLibraryItemDto
            {
                AssetId = file.Name,
                Url = AssetUrlPrefix + file.Name,
                DisplayName = displayNames.GetValueOrDefault(file.Name) ?? GetFallbackDisplayName(file.Name),
                ContentType = GetContentType(file.Extension.ToLowerInvariant()),
                Size = file.Length,
                UsedBy = GetReferences(appearance.Value, file.Name)
                    .Concat(presetReferences.GetValueOrDefault(file.Name) ?? [])
                    .ToArray()
            })
            .ToArray();

        return Result<IReadOnlyList<ThemeAssetLibraryItemDto>>.Success(assets);
    }

    public async Task<Result<ThemeAssetDto>> UploadAsync(Guid userId, UserRole currentUserRole, string originalFileName, string declaredContentType, long declaredLength, Stream content, CancellationToken cancellationToken = default)
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
        var displayName = GetFallbackDisplayName(assetId);
        if (themeLibraryService is not null)
        {
            var metadata = await themeLibraryService.RegisterAssetDisplayNameAsync(assetId, originalFileName, userId, currentUserRole, cancellationToken);
            if (metadata.IsFailure || metadata.Value is null)
            {
                TryDelete(storagePaths.ResolveThemeAssetPath(assetId));
                return Result<ThemeAssetDto>.Failure(metadata.ErrorMessage ?? "Theme asset metadata could not be saved.");
            }
            displayName = metadata.Value;
        }
        return Result<ThemeAssetDto>.Success(new ThemeAssetDto
        {
            AssetId = assetId,
            Url = AssetUrlPrefix + assetId,
            DisplayName = displayName,
            ContentType = GetContentType(validation.CanonicalExtension),
            Size = size
        });
    }

    public Task<Result<ThemeAssetDto>> UploadAsync(UserRole currentUserRole, string originalFileName, string declaredContentType, long declaredLength, Stream content, CancellationToken cancellationToken = default) =>
        UploadAsync(Guid.Empty, currentUserRole, originalFileName, declaredContentType, declaredLength, content, cancellationToken);

    public async Task<Result<ThemeAssetDto>> RenameAsync(Guid userId, UserRole currentUserRole, string assetId, string displayName, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Root)
        {
            return Result<ThemeAssetDto>.Failure("Forbidden.");
        }

        string path;
        try
        {
            path = storagePaths.ResolveThemeAssetPath(assetId);
        }
        catch (InvalidDataException)
        {
            return Result<ThemeAssetDto>.Failure("Theme asset id is invalid.");
        }

        if (!IsManagedAssetId(assetId) || !File.Exists(path))
        {
            return Result<ThemeAssetDto>.Failure("Theme asset was not found.");
        }

        if (themeLibraryService is null)
        {
            return Result<ThemeAssetDto>.Failure("Theme asset metadata is unavailable.");
        }

        var renamed = await themeLibraryService.RenameAssetDisplayNameAsync(assetId, displayName, userId, currentUserRole, cancellationToken);
        if (renamed.IsFailure || renamed.Value is null)
        {
            return Result<ThemeAssetDto>.Failure(renamed.ErrorMessage ?? "Theme asset could not be renamed.");
        }

        var file = new FileInfo(path);
        return Result<ThemeAssetDto>.Success(new ThemeAssetDto
        {
            AssetId = assetId,
            Url = AssetUrlPrefix + assetId,
            DisplayName = renamed.Value,
            ContentType = GetContentType(file.Extension.ToLowerInvariant()),
            Size = file.Length
        });
    }

    public async Task<Result> DeleteAsync(Guid userId, UserRole currentUserRole, string assetId, CancellationToken cancellationToken = default)
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

        if (themeLibraryService is not null)
        {
            var metadata = await themeLibraryService.RemoveAssetDisplayNameAsync(assetId, userId, currentUserRole, cancellationToken);
            if (metadata.IsFailure) return metadata;
        }

        return Result.Success();
    }

    public Task<Result> DeleteAsync(UserRole currentUserRole, string assetId, CancellationToken cancellationToken = default) =>
        DeleteAsync(Guid.Empty, currentUserRole, assetId, cancellationToken);

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

    private static string GetFallbackDisplayName(string assetId) => $"Asset {Path.GetFileNameWithoutExtension(assetId)[..8].ToUpperInvariant()}";

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
