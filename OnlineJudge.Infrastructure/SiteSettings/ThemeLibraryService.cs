using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Application.SiteSettings;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Infrastructure.SiteSettings;

public sealed class ThemeLibraryService(
    OnlineJudgeDbContext dbContext,
    ISiteSettingsService siteSettingsService,
    IRuntimeStoragePathProvider storagePaths,
    ISecureArchiveExtractor archiveExtractor,
    ISecureUploadValidator uploadValidator,
    ISecurityAuditWriter auditWriter,
    TimeProvider timeProvider) : IThemeLibraryService
{
    private const string LibraryKey = "theme-library";
    private const string AssetUrlPrefix = "/theme-assets/";
    private const int MaxAssetDisplayNameLength = 128;
    private static readonly SemaphoreSlim MutationLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions ImportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly HashSet<string> ArchiveContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/zip", "application/x-zip-compressed", "application/octet-stream"
    };

    public async Task<Result<ThemePresetListDto>> ListAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Root) return Result<ThemePresetListDto>.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        var items = new List<ThemePresetDto> { CreateDefaultPreset() };
        items.AddRange(library.Presets.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Select(ToDto));
        return Result<ThemePresetListDto>.Success(new ThemePresetListDto { Items = items, LastAppliedPresetId = library.LastAppliedPresetId });
    }

    public Task<Result<ThemePresetDto>> CreateAsync(CreateThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => SaveNewAsync(request.Name, request.Description, request.Appearance, userId, role, SecurityAuditActions.ThemePresetCreated, null, cancellationToken), cancellationToken);

    public Task<Result<ThemePresetDto>> UpdateAsync(Guid presetId, UpdateThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => UpdateCoreAsync(presetId, request, userId, role, cancellationToken), cancellationToken);

    private async Task<Result<ThemePresetDto>> UpdateCoreAsync(Guid presetId, UpdateThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<ThemePresetDto>.Failure("Forbidden.");
        var nameError = ValidateMetadata(request.Name, request.Description, out var name, out var description);
        if (nameError is not null) return Result<ThemePresetDto>.Failure(nameError);
        var validated = await siteSettingsService.ValidateAppearanceAsync(ToRequest(request.Appearance), cancellationToken: cancellationToken);
        if (validated.IsFailure || validated.Value is null) return Result<ThemePresetDto>.Failure(validated.ErrorMessage ?? "Theme appearance is invalid.");

        var library = await ReadLibraryAsync(cancellationToken);
        var preset = library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null) return Result<ThemePresetDto>.Failure("Theme preset was not found.");
        if (HasNameCollision(library, name, presetId)) return Result<ThemePresetDto>.Failure("Theme preset name already exists.");

        preset.Name = name;
        preset.Description = description;
        preset.Appearance = validated.Value;
        preset.SchemaVersion = ThemePackContract.PresetSchemaVersion;
        preset.UpdatedAt = timeProvider.GetUtcNow();
        await PersistAsync(library, userId, SecurityAuditActions.ThemePresetUpdated, preset, cancellationToken);
        return Result<ThemePresetDto>.Success(ToDto(preset));
    }

    public Task<Result<ThemePresetDto>> DuplicateAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => DuplicateCoreAsync(presetId, userId, role, cancellationToken), cancellationToken);

    private async Task<Result<ThemePresetDto>> DuplicateCoreAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<ThemePresetDto>.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        var source = library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (source is null) return Result<ThemePresetDto>.Failure("Theme preset was not found.");
        if (library.Presets.Count >= ThemePackContract.MaxPresets) return Result<ThemePresetDto>.Failure("Theme preset limit of 30 has been reached.");

        var now = timeProvider.GetUtcNow();
        var duplicate = new StoredThemePreset
        {
            Id = Guid.NewGuid(),
            Name = SuggestName(library, source.Name),
            Description = source.Description,
            SchemaVersion = ThemePackContract.PresetSchemaVersion,
            Appearance = Clone(source.Appearance),
            CreatedAt = now,
            UpdatedAt = now
        };
        library.Presets.Add(duplicate);
        await PersistAsync(library, userId, SecurityAuditActions.ThemePresetDuplicated, duplicate, cancellationToken, source.Id);
        return Result<ThemePresetDto>.Success(ToDto(duplicate));
    }

    public Task<Result<ThemePresetDto>> RenameAsync(Guid presetId, RenameThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => RenameCoreAsync(presetId, request, userId, role, cancellationToken), cancellationToken);

    private async Task<Result<ThemePresetDto>> RenameCoreAsync(Guid presetId, RenameThemePresetRequest request, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<ThemePresetDto>.Failure("Forbidden.");
        var nameError = ValidateMetadata(request.Name, null, out var name, out _);
        if (nameError is not null) return Result<ThemePresetDto>.Failure(nameError);
        var library = await ReadLibraryAsync(cancellationToken);
        var preset = library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null) return Result<ThemePresetDto>.Failure("Theme preset was not found.");
        if (HasNameCollision(library, name, presetId)) return Result<ThemePresetDto>.Failure("Theme preset name already exists.");

        preset.Name = name;
        preset.UpdatedAt = timeProvider.GetUtcNow();
        await PersistAsync(library, userId, SecurityAuditActions.ThemePresetRenamed, preset, cancellationToken);
        return Result<ThemePresetDto>.Success(ToDto(preset));
    }

    public Task<Result> DeleteAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => DeleteCoreAsync(presetId, userId, role, cancellationToken), cancellationToken);

    private async Task<Result> DeleteCoreAsync(Guid presetId, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        var preset = library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null) return Result.Failure("Theme preset was not found.");
        library.Presets.Remove(preset);
        if (library.LastAppliedPresetId == presetId) library.LastAppliedPresetId = null;
        await PersistAsync(library, userId, SecurityAuditActions.ThemePresetDeleted, preset, cancellationToken);
        return Result.Success();
    }

    public Task<Result<SiteAppearanceDto>> ApplyAsync(Guid? presetId, Guid userId, UserRole role, string? requestHost, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => ApplyCoreAsync(presetId, userId, role, requestHost, cancellationToken), cancellationToken);

    private async Task<Result<SiteAppearanceDto>> ApplyCoreAsync(Guid? presetId, Guid userId, UserRole role, string? requestHost, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<SiteAppearanceDto>.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        var preset = presetId is null ? null : library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (presetId is not null && preset is null) return Result<SiteAppearanceDto>.Failure("Theme preset was not found.");

        var appearance = preset is null ? siteSettingsService.GetDefaultAppearance() : Clone(preset.Appearance);
        RemoveMissingAssets(appearance);
        var applied = await siteSettingsService.UpdateAppearanceAsync(ToRequest(appearance), userId, role, requestHost, cancellationToken);
        if (applied.IsFailure || applied.Value is null) return Result<SiteAppearanceDto>.Failure(applied.ErrorMessage ?? "Theme could not be applied.");

        library.LastAppliedPresetId = presetId;
        var auditPreset = preset ?? new StoredThemePreset { Id = Guid.Empty, Name = "Default Theme", SchemaVersion = 1, Appearance = appearance };
        await PersistAsync(library, userId, SecurityAuditActions.ThemeApplied, auditPreset, cancellationToken);
        return applied;
    }

    public async Task<Result<ThemePackExportDto>> ExportAsync(Guid presetId, UserRole role, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Root) return Result<ThemePackExportDto>.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        var preset = library.Presets.SingleOrDefault(item => item.Id == presetId);
        if (preset is null) return Result<ThemePackExportDto>.Failure("Theme preset was not found.");

        var appearance = Clone(preset.Appearance);
        var sources = RewriteForExport(appearance);
        if (sources.Count > ThemePackContract.MaxAssets) return Result<ThemePackExportDto>.Failure("Theme preset references more than 50 assets.");
        if (sources.Any(item => !File.Exists(item.SourcePath))) return Result<ThemePackExportDto>.Failure("Theme preset references a missing asset.");

        var manifest = new ThemePackManifest
        {
            Format = ThemePackContract.Format,
            Version = ThemePackContract.Version,
            Name = preset.Name,
            Description = preset.Description,
            SchemaVersion = preset.SchemaVersion,
            Appearance = appearance,
            Assets = sources
                .Where(source => source.AssetId is not null && library.AssetDisplayNames.TryGetValue(source.AssetId, out _))
                .Select(source => new ThemePackAssetMetadata
                {
                    Path = source.PackPath,
                    DisplayName = NormalizeAssetDisplayName(library.AssetDisplayNames[source.AssetId!], Path.GetFileName(source.PackPath))
                })
                .ToList()
        };
        var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);
            }

            foreach (var source in sources)
            {
                var entry = archive.CreateEntry(source.PackPath, CompressionLevel.Optimal);
                await using var destination = entry.Open();
                await using var input = new FileStream(source.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await input.CopyToAsync(destination, cancellationToken);
            }
        }

        if (output.Length > ThemePackContract.MaxPackBytes)
        {
            output.Dispose();
            return Result<ThemePackExportDto>.Failure("Theme pack exceeds the 50 MiB limit.");
        }

        output.Position = 0;
        return Result<ThemePackExportDto>.Success(new ThemePackExportDto { Content = output, FileName = $"{SafeFileName(preset.Name)}.oj-theme.zip" });
    }

    public async Task<Result<ThemePackPreflightDto>> PreflightImportAsync(string fileName, string contentType, long length, Stream content, UserRole role, CancellationToken cancellationToken = default)
    {
        if (role != UserRole.Root) return Result<ThemePackPreflightDto>.Failure("Forbidden.");
        var validated = await ValidateImportAsync(fileName, contentType, length, content, cancellationToken);
        if (validated.IsFailure || validated.Value is null) return Result<ThemePackPreflightDto>.Failure(validated.ErrorMessage ?? "Theme pack is invalid.");
        var library = await ReadLibraryAsync(cancellationToken);
        if (library.Presets.Count >= ThemePackContract.MaxPresets) return Result<ThemePackPreflightDto>.Failure("Theme preset limit of 30 has been reached.");
        var collision = HasNameCollision(library, validated.Value.RequestedName);
        var resolvedName = collision ? SuggestName(library, validated.Value.RequestedName) : validated.Value.RequestedName;
        var appearance = validated.Value.Appearance;
        return Result<ThemePackPreflightDto>.Success(new ThemePackPreflightDto
        {
            Name = validated.Value.RequestedName,
            Description = validated.Value.Description,
            Format = ThemePackContract.Format,
            Version = ThemePackContract.Version,
            SchemaVersion = ThemePackContract.PresetSchemaVersion,
            AssetCount = validated.Value.References.Count,
            TotalAssetBytes = validated.Value.References.Sum(path => (long)validated.Value.Entries[path].Length),
            HasBackground = appearance.Background.Asset is not null || appearance.Pages.Values.Any(page => !string.IsNullOrWhiteSpace(page.ImageUrl)),
            PanelAssetCount = new[] { appearance.PanelSkin.BackgroundTexture, appearance.PanelSkin.HeaderTexture, appearance.PanelSkin.BorderTexture }.Count(asset => asset is not null),
            IconOverrideCount = appearance.Icons.Values.Count(slot => slot?.Asset is not null),
            DecorationCount = appearance.Decorations.Values.Count(slot => slot?.Asset is not null),
            HasNameCollision = collision,
            ResolvedName = resolvedName,
            Warnings = collision ? [$"A theme named '{validated.Value.RequestedName}' already exists; it will be imported as '{resolvedName}'."] : []
        });
    }

    public Task<Result<ThemePresetDto>> ImportAsync(string fileName, string contentType, long length, Stream content, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => ImportCoreAsync(fileName, contentType, length, content, userId, role, cancellationToken), cancellationToken);

    private async Task<Result<ThemePresetDto>> ImportCoreAsync(string fileName, string contentType, long length, Stream content, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<ThemePresetDto>.Failure("Forbidden.");
        var validatedPack = await ValidateImportAsync(fileName, contentType, length, content, cancellationToken);
        if (validatedPack.IsFailure || validatedPack.Value is null) return Result<ThemePresetDto>.Failure(validatedPack.ErrorMessage ?? "Theme pack is invalid.");
        var pack = validatedPack.Value;
        var library = await ReadLibraryAsync(cancellationToken);
        if (library.Presets.Count >= ThemePackContract.MaxPresets) return Result<ThemePresetDto>.Failure("Theme preset limit of 30 has been reached.");
        var name = HasNameCollision(library, pack.RequestedName) ? SuggestName(library, pack.RequestedName) : pack.RequestedName;
        var importedAssets = pack.References.ToDictionary(path => path, path => $"{Guid.NewGuid():N}{CanonicalExtension(Path.GetExtension(path))}", StringComparer.Ordinal);
        var appearance = pack.Appearance;
        RewriteImportedReferences(appearance, importedAssets);
        var stagingRoot = Path.Combine(Path.GetTempPath(), "onlinejudge-theme-import", Guid.NewGuid().ToString("N"));
        var stagingPaths = new RuntimeStoragePathProvider(Path.Combine(stagingRoot, "api"), themeAssetsRoot: Path.Combine(stagingRoot, "assets"));
        var written = new List<string>();
        try
        {
            foreach (var (packPath, assetId) in importedAssets)
            {
                await using var image = new MemoryStream(pack.Entries[packPath], writable: false);
                await stagingPaths.WriteThemeAssetAsync(assetId, image, 5L * 1024 * 1024, cancellationToken);
            }

            var stagingAppearanceService = new SiteSettingsService(dbContext, storagePaths: stagingPaths);
            var validated = await stagingAppearanceService.ValidateAppearanceAsync(ToRequest(appearance), cancellationToken: cancellationToken);
            if (validated.IsFailure || validated.Value is null) throw new InvalidDataException(validated.ErrorMessage ?? "Imported theme appearance is invalid.");

            foreach (var assetId in importedAssets.Values)
            {
                await using var image = new FileStream(stagingPaths.ResolveThemeAssetPath(assetId), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                await storagePaths.WriteThemeAssetAsync(assetId, image, 5L * 1024 * 1024, cancellationToken);
                written.Add(assetId);
            }

            var now = timeProvider.GetUtcNow();
            var preset = new StoredThemePreset { Id = Guid.NewGuid(), Name = name, Description = pack.Description, SchemaVersion = 1, Appearance = validated.Value, CreatedAt = now, UpdatedAt = now };
            library.Presets.Add(preset);
            foreach (var (packPath, assetId) in importedAssets)
            {
                library.AssetDisplayNames[assetId] = pack.DisplayNames.GetValueOrDefault(packPath)
                    ?? NormalizeAssetDisplayName(Path.GetFileName(packPath), GetFallbackAssetDisplayName(assetId));
            }
            await PersistAsync(library, userId, SecurityAuditActions.ThemePresetImported, preset, cancellationToken);
            return Result<ThemePresetDto>.Success(ToDto(preset));
        }
        catch (Exception exception) when (exception is InvalidDataException or DbUpdateException or IOException)
        {
            foreach (var assetId in written) TryDelete(storagePaths.ResolveThemeAssetPath(assetId));
            return Result<ThemePresetDto>.Failure(exception.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<Result<ValidatedThemePack>> ValidateImportAsync(string fileName, string contentType, long length, Stream content, CancellationToken cancellationToken)
    {
        if (length is <= 0 or > ThemePackContract.MaxPackBytes
            || !string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase)
            || !ArchiveContentTypes.Contains(contentType))
        {
            return Result<ValidatedThemePack>.Failure("Only OnlineJudge theme ZIP packs up to 50 MiB are allowed.");
        }

        var extraction = await archiveExtractor.ExtractThemePackAsync(content, cancellationToken);
        if (extraction.IsFailure || extraction.Value is null) return Result<ValidatedThemePack>.Failure(extraction.ErrorMessage ?? "Theme pack is invalid.");
        var entries = extraction.Value;
        ThemePackManifest? manifest;
        try
        {
            using var document = JsonDocument.Parse(entries["manifest.json"]);
            var allowed = new HashSet<string>(["format", "version", "name", "description", "schemaVersion", "appearance", "assets"], StringComparer.Ordinal);
            if (document.RootElement.ValueKind != JsonValueKind.Object || document.RootElement.EnumerateObject().Any(property => !allowed.Contains(property.Name)))
                return Result<ValidatedThemePack>.Failure("Theme pack manifest contains unknown fields.");
            manifest = document.RootElement.Deserialize<ThemePackManifest>(ImportJsonOptions);
        }
        catch (JsonException)
        {
            return Result<ValidatedThemePack>.Failure("Theme pack manifest is invalid JSON.");
        }

        if (manifest is null || manifest.Format != ThemePackContract.Format || manifest.Version != ThemePackContract.Version || manifest.SchemaVersion != ThemePackContract.PresetSchemaVersion)
            return Result<ValidatedThemePack>.Failure("Theme pack format or version is not supported.");
        var metadataError = ValidateMetadata(manifest.Name, manifest.Description, out var requestedName, out var description);
        if (metadataError is not null) return Result<ValidatedThemePack>.Failure(metadataError);
        var appearance = manifest.Appearance ?? new SiteAppearanceDto();
        if (appearance.Theme is null || appearance.Pages is null || appearance.Background is null || appearance.PanelSkin is null || appearance.Icons is null || appearance.Decorations is null)
            return Result<ValidatedThemePack>.Failure("Theme pack appearance is incomplete.");
        var references = CollectPackReferences(appearance);
        if (references.IsFailure || references.Value is null) return Result<ValidatedThemePack>.Failure(references.ErrorMessage ?? "Theme pack asset references are invalid.");
        var packedAssets = entries.Keys.Where(key => key != "manifest.json").ToHashSet(StringComparer.Ordinal);
        if (!packedAssets.SetEquals(references.Value)) return Result<ValidatedThemePack>.Failure("Theme pack must contain exactly the assets referenced by its manifest.");

        foreach (var packPath in references.Value)
        {
            var bytes = entries[packPath];
            var extension = Path.GetExtension(packPath).ToLowerInvariant();
            await using var image = new MemoryStream(bytes, writable: false);
            var validation = await uploadValidator.ValidateAsync(new SecureUploadRequest
            {
                Policy = UploadPolicy.ThemeImage,
                OriginalFileName = Path.GetFileName(packPath),
                DeclaredContentType = ContentType(extension),
                DeclaredLength = bytes.LongLength,
                Content = image
            }, cancellationToken);
            if (!validation.IsValid || validation.CanonicalExtension is null)
                return Result<ValidatedThemePack>.Failure(validation.ErrorMessage ?? "Theme pack contains an invalid image.");
        }

        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in manifest.Assets ?? [])
        {
            if (string.IsNullOrWhiteSpace(asset.Path) || !references.Value.Contains(asset.Path) || displayNames.ContainsKey(asset.Path))
                return Result<ValidatedThemePack>.Failure("Theme pack asset metadata is invalid.");
            displayNames[asset.Path] = NormalizeAssetDisplayName(asset.DisplayName, Path.GetFileName(asset.Path));
        }

        var appearanceValidation = await ValidateImportedAppearanceAsync(appearance, entries, references.Value, cancellationToken);
        if (appearanceValidation.IsFailure) return Result<ValidatedThemePack>.Failure(appearanceValidation.ErrorMessage ?? "Imported theme appearance is invalid.");

        return Result<ValidatedThemePack>.Success(new ValidatedThemePack(entries, references.Value, appearance, requestedName, description, displayNames));
    }

    private async Task<Result> ValidateImportedAppearanceAsync(SiteAppearanceDto appearance, IReadOnlyDictionary<string, byte[]> entries, IEnumerable<string> references, CancellationToken cancellationToken)
    {
        var validationRoot = Path.Combine(Path.GetTempPath(), "onlinejudge-theme-preflight", Guid.NewGuid().ToString("N"));
        var validationPaths = new RuntimeStoragePathProvider(Path.Combine(validationRoot, "api"), themeAssetsRoot: Path.Combine(validationRoot, "assets"));
        var assetIds = references.ToDictionary(path => path, path => $"{Guid.NewGuid():N}{CanonicalExtension(Path.GetExtension(path))}", StringComparer.Ordinal);
        var validationAppearance = Clone(appearance);
        RewriteImportedReferences(validationAppearance, assetIds);
        try
        {
            foreach (var (packPath, assetId) in assetIds)
            {
                await using var image = new MemoryStream(entries[packPath], writable: false);
                await validationPaths.WriteThemeAssetAsync(assetId, image, 5L * 1024 * 1024, cancellationToken);
            }
            var validator = new SiteSettingsService(dbContext, storagePaths: validationPaths);
            var validated = await validator.ValidateAppearanceAsync(ToRequest(validationAppearance), cancellationToken: cancellationToken);
            return validated.IsSuccess ? Result.Success() : Result.Failure(validated.ErrorMessage ?? "Imported theme appearance is invalid.");
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return Result.Failure(exception.Message);
        }
        finally
        {
            TryDeleteDirectory(validationRoot);
        }
    }

    public async Task<Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>> GetAssetReferencesAsync(CancellationToken cancellationToken = default)
    {
        var library = await ReadLibraryAsync(cancellationToken);
        var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var preset in library.Presets)
        {
            foreach (var (assetId, surface) in EnumerateThemeReferences(preset.Appearance))
            {
                if (!references.TryGetValue(assetId, out var values)) references[assetId] = values = [];
                values.Add($"preset:{preset.Name}:{surface}");
            }
        }
        return Result<IReadOnlyDictionary<string, IReadOnlyList<string>>>.Success(references.ToDictionary(item => item.Key, item => (IReadOnlyList<string>)item.Value, StringComparer.Ordinal));
    }

    public async Task<Result<IReadOnlyDictionary<string, string>>> GetAssetDisplayNamesAsync(CancellationToken cancellationToken = default)
    {
        var library = await ReadLibraryAsync(cancellationToken);
        return Result<IReadOnlyDictionary<string, string>>.Success(new Dictionary<string, string>(library.AssetDisplayNames, StringComparer.Ordinal));
    }

    public Task<Result<string>> RegisterAssetDisplayNameAsync(string assetId, string originalFileName, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => SaveAssetDisplayNameAsync(assetId, NormalizeAssetDisplayName(originalFileName, GetFallbackAssetDisplayName(assetId)), userId, role, cancellationToken), cancellationToken);

    public Task<Result<string>> RenameAssetDisplayNameAsync(string assetId, string displayName, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(() => SaveAssetDisplayNameAsync(assetId, NormalizeAssetDisplayName(displayName, GetFallbackAssetDisplayName(assetId)), userId, role, cancellationToken), cancellationToken);

    public Task<Result> RemoveAssetDisplayNameAsync(string assetId, Guid userId, UserRole role, CancellationToken cancellationToken = default) =>
        WithMutationLockAsync(async () =>
        {
            if (role != UserRole.Root) return Result.Failure("Forbidden.");
            var library = await ReadLibraryAsync(cancellationToken);
            if (!library.AssetDisplayNames.Remove(assetId)) return Result.Success();
            await PersistMetadataAsync(library, userId, cancellationToken);
            return Result.Success();
        }, cancellationToken);

    private async Task<Result<string>> SaveAssetDisplayNameAsync(string assetId, string displayName, Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<string>.Failure("Forbidden.");
        var library = await ReadLibraryAsync(cancellationToken);
        library.AssetDisplayNames[assetId] = displayName;
        await PersistMetadataAsync(library, userId, cancellationToken);
        return Result<string>.Success(displayName);
    }

    private async Task<Result<ThemePresetDto>> SaveNewAsync(string requestedName, string? requestedDescription, SiteAppearanceDto appearance, Guid userId, UserRole role, string action, Guid? sourcePresetId, CancellationToken cancellationToken)
    {
        if (role != UserRole.Root) return Result<ThemePresetDto>.Failure("Forbidden.");
        var metadataError = ValidateMetadata(requestedName, requestedDescription, out var name, out var description);
        if (metadataError is not null) return Result<ThemePresetDto>.Failure(metadataError);
        var validated = await siteSettingsService.ValidateAppearanceAsync(ToRequest(appearance), cancellationToken: cancellationToken);
        if (validated.IsFailure || validated.Value is null) return Result<ThemePresetDto>.Failure(validated.ErrorMessage ?? "Theme appearance is invalid.");
        var library = await ReadLibraryAsync(cancellationToken);
        if (library.Presets.Count >= ThemePackContract.MaxPresets) return Result<ThemePresetDto>.Failure("Theme preset limit of 30 has been reached.");
        if (HasNameCollision(library, name)) return Result<ThemePresetDto>.Failure("Theme preset name already exists.");
        var now = timeProvider.GetUtcNow();
        var preset = new StoredThemePreset { Id = Guid.NewGuid(), Name = name, Description = description, SchemaVersion = 1, Appearance = validated.Value, CreatedAt = now, UpdatedAt = now };
        library.Presets.Add(preset);
        await PersistAsync(library, userId, action, preset, cancellationToken, sourcePresetId);
        return Result<ThemePresetDto>.Success(ToDto(preset));
    }

    private async Task<StoredThemeLibrary> ReadLibraryAsync(CancellationToken cancellationToken)
    {
        var value = await dbContext.SiteSettings.AsNoTracking().Where(item => item.Key == LibraryKey).Select(item => item.Value).SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(value)) return new StoredThemeLibrary();
        try
        {
            var library = JsonSerializer.Deserialize<StoredThemeLibrary>(value, JsonOptions) ?? new StoredThemeLibrary();
            library.Presets = library.Presets.Where(item => item.Id != Guid.Empty && item.SchemaVersion == 1).Take(ThemePackContract.MaxPresets).ToList();
            library.AssetDisplayNames = (library.AssetDisplayNames ?? new Dictionary<string, string>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Key))
                .ToDictionary(item => item.Key, item => NormalizeAssetDisplayName(item.Value, GetFallbackAssetDisplayName(item.Key)), StringComparer.Ordinal);
            return library;
        }
        catch (JsonException)
        {
            return new StoredThemeLibrary();
        }
    }

    private async Task PersistAsync(StoredThemeLibrary library, Guid userId, string action, StoredThemePreset preset, CancellationToken cancellationToken, Guid? sourcePresetId = null)
    {
        var setting = await dbContext.SiteSettings.SingleOrDefaultAsync(item => item.Key == LibraryKey, cancellationToken);
        if (setting is null)
        {
            setting = new SiteSetting { Id = Guid.NewGuid(), Key = LibraryKey };
            dbContext.SiteSettings.Add(setting);
        }
        setting.Value = JsonSerializer.Serialize(library, JsonOptions);
        setting.UpdatedAt = timeProvider.GetUtcNow();
        setting.UpdatedByUserId = userId;
        auditWriter.Stage(new SecurityAuditRecord(action, "ThemePreset", preset.Id == Guid.Empty ? "default" : preset.Id.ToString(), Metadata: AuditMetadata(preset, sourcePresetId)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PersistMetadataAsync(StoredThemeLibrary library, Guid userId, CancellationToken cancellationToken)
    {
        var setting = await dbContext.SiteSettings.SingleOrDefaultAsync(item => item.Key == LibraryKey, cancellationToken);
        if (setting is null)
        {
            setting = new SiteSetting { Id = Guid.NewGuid(), Key = LibraryKey };
            dbContext.SiteSettings.Add(setting);
        }
        setting.Value = JsonSerializer.Serialize(library, JsonOptions);
        setting.UpdatedAt = timeProvider.GetUtcNow();
        setting.UpdatedByUserId = userId;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, string?> AuditMetadata(StoredThemePreset preset, Guid? sourcePresetId) => new Dictionary<string, string?>
    {
        ["presetId"] = preset.Id == Guid.Empty ? "default" : preset.Id.ToString(),
        ["presetName"] = preset.Name,
        ["schemaVersion"] = preset.SchemaVersion.ToString(),
        ["assetCount"] = CountAssets(preset.Appearance).ToString(),
        ["sourcePresetId"] = sourcePresetId?.ToString()
    };

    private static string? ValidateMetadata(string? requestedName, string? requestedDescription, out string name, out string? description)
    {
        name = requestedName?.Trim() ?? string.Empty;
        description = string.IsNullOrWhiteSpace(requestedDescription) ? null : requestedDescription.Trim();
        if (name.Length is < 1 or > 64) return "Theme preset name must contain 1 to 64 characters.";
        if (name.Equals("Default Theme", StringComparison.OrdinalIgnoreCase)) return "Default Theme is reserved.";
        if (description?.Length > 256) return "Theme preset description cannot exceed 256 characters.";
        return null;
    }

    private static bool HasNameCollision(StoredThemeLibrary library, string name, Guid? exceptId = null) =>
        library.Presets.Any(item => item.Id != exceptId && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string SuggestName(StoredThemeLibrary library, string source)
    {
        for (var index = 2; index <= 999; index++)
        {
            var suffix = $" ({index})";
            var baseName = source[..Math.Min(source.Length, 64 - suffix.Length)].TrimEnd();
            var candidate = baseName + suffix;
            if (!HasNameCollision(library, candidate)) return candidate;
        }
        return Guid.NewGuid().ToString("N")[..16];
    }

    private List<PackAssetSource> RewriteForExport(SiteAppearanceDto appearance)
    {
        var bySource = new Dictionary<string, PackAssetSource>(StringComparer.OrdinalIgnoreCase);
        string RewriteTheme(ThemeAssetReferenceDto asset)
        {
            var source = storagePaths.ResolveThemeAssetPath(asset.AssetId);
            return Add(source, Path.GetExtension(asset.AssetId), asset.AssetId);
        }
        string RewritePage(string url)
        {
            if (url.StartsWith(AssetUrlPrefix, StringComparison.Ordinal))
            {
                var assetId = url[AssetUrlPrefix.Length..];
                return Add(storagePaths.ResolveThemeAssetPath(assetId), Path.GetExtension(url), assetId);
            }
            const string uploadPrefix = "/uploads/images/";
            return Add(storagePaths.ResolveUploadImagePath(url[uploadPrefix.Length..]), Path.GetExtension(url), null);
        }
        string Add(string source, string extension, string? assetId)
        {
            if (bySource.TryGetValue(source, out var existing)) return existing.PackPath;
            var packPath = $"assets/{bySource.Count + 1:D3}{CanonicalExtension(extension)}";
            bySource[source] = new PackAssetSource(source, packPath, assetId);
            return packPath;
        }
        RewriteReferences(appearance, RewriteTheme, RewritePage, exporting: true);
        return bySource.Values.ToList();
    }

    private static Result<HashSet<string>> CollectPackReferences(SiteAppearanceDto appearance)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            RewriteReferences(appearance, asset =>
            {
                if (!string.Equals(asset.AssetId, asset.Url, StringComparison.Ordinal)) throw new InvalidDataException("Theme pack asset references must use logical pack keys only.");
                return ValidatePackPath(asset.AssetId, references);
            }, path => ValidatePackPath(path, references), exporting: false);
            return Result<HashSet<string>>.Success(references);
        }
        catch (InvalidDataException exception)
        {
            return Result<HashSet<string>>.Failure(exception.Message);
        }
    }

    private static string ValidatePackPath(string path, ISet<string> references)
    {
        var segments = path.Split('/');
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (segments.Length != 2 || segments[0] != "assets" || Path.GetFileName(segments[1]) != segments[1]
            || extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidDataException("Theme pack contains an unsafe asset reference.");
        references.Add(path);
        return path;
    }

    private static void RewriteImportedReferences(SiteAppearanceDto appearance, IReadOnlyDictionary<string, string> importedAssets)
    {
        RewriteReferences(appearance,
            asset => importedAssets[asset.AssetId],
            path => importedAssets[path],
            exporting: false,
            imported: true);
    }

    private static void RewriteReferences(SiteAppearanceDto appearance, Func<ThemeAssetReferenceDto, string> themeRewrite, Func<string, string> pageRewrite, bool exporting, bool imported = false)
    {
        void Theme(ThemeAssetReferenceDto? asset)
        {
            if (asset is null) return;
            var rewritten = themeRewrite(asset);
            asset.AssetId = rewritten;
            asset.Url = imported ? AssetUrlPrefix + rewritten : rewritten;
        }
        Theme(appearance.Background?.Asset);
        Theme(appearance.PanelSkin?.BackgroundTexture);
        Theme(appearance.PanelSkin?.HeaderTexture);
        Theme(appearance.PanelSkin?.BorderTexture);
        foreach (var slot in appearance.Icons?.Values ?? Enumerable.Empty<SiteThemeIconSlotDto?>()) Theme(slot?.Asset);
        foreach (var slot in appearance.Decorations?.Values ?? Enumerable.Empty<SiteThemeDecorationSlotDto?>()) Theme(slot?.Asset);
        foreach (var page in appearance.Pages?.Values ?? Enumerable.Empty<SitePageBackgroundDto>())
        {
            if (string.IsNullOrWhiteSpace(page.ImageUrl)) continue;
            var rewritten = pageRewrite(page.ImageUrl);
            page.ImageUrl = imported ? AssetUrlPrefix + rewritten : rewritten;
        }
    }

    private void RemoveMissingAssets(SiteAppearanceDto appearance)
    {
        bool Missing(ThemeAssetReferenceDto? asset) => asset is not null && !File.Exists(storagePaths.ResolveThemeAssetPath(asset.AssetId));
        if (Missing(appearance.Background.Asset)) { appearance.Background.Asset = null; appearance.Background.Enabled = false; }
        if (Missing(appearance.PanelSkin.BackgroundTexture)) appearance.PanelSkin.BackgroundTexture = null;
        if (Missing(appearance.PanelSkin.HeaderTexture)) appearance.PanelSkin.HeaderTexture = null;
        if (Missing(appearance.PanelSkin.BorderTexture)) appearance.PanelSkin.BorderTexture = null;
        foreach (var slot in appearance.Icons.Values.Where(item => item is not null && Missing(item.Asset))) { slot!.Asset = null; slot.Enabled = false; }
        foreach (var slot in appearance.Decorations.Values.Where(item => item is not null && Missing(item.Asset))) { slot!.Asset = null; slot.Enabled = false; }
        foreach (var page in appearance.Pages.Values)
        {
            if (page.ImageUrl?.StartsWith(AssetUrlPrefix, StringComparison.Ordinal) == true && !File.Exists(storagePaths.ResolveThemeAssetPath(page.ImageUrl[AssetUrlPrefix.Length..])))
            { page.ImageUrl = null; page.Enabled = false; }
        }
    }

    private static IEnumerable<(string AssetId, string Surface)> EnumerateThemeReferences(SiteAppearanceDto appearance)
    {
        if (appearance.Background.Asset is { } background) yield return (background.AssetId, "background");
        if (appearance.PanelSkin.BackgroundTexture is { } panel) yield return (panel.AssetId, "panelBackground");
        if (appearance.PanelSkin.HeaderTexture is { } header) yield return (header.AssetId, "panelHeader");
        if (appearance.PanelSkin.BorderTexture is { } border) yield return (border.AssetId, "panelBorder");
        foreach (var (key, slot) in appearance.Icons) if (slot?.Asset is { } asset) yield return (asset.AssetId, $"icon:{key}");
        foreach (var (key, slot) in appearance.Decorations) if (slot?.Asset is { } asset) yield return (asset.AssetId, $"decoration:{key}");
        foreach (var (key, page) in appearance.Pages)
            if (page.ImageUrl?.StartsWith(AssetUrlPrefix, StringComparison.Ordinal) == true) yield return (page.ImageUrl[AssetUrlPrefix.Length..], $"page:{key}");
    }

    private static int CountAssets(SiteAppearanceDto appearance) => EnumerateThemeReferences(appearance).Select(item => item.AssetId)
        .Concat(appearance.Pages.Values.Where(page => !string.IsNullOrWhiteSpace(page.ImageUrl)).Select(page => page.ImageUrl!))
        .Distinct(StringComparer.Ordinal).Count();
    private static ThemePresetDto ToDto(StoredThemePreset preset) => new() { Id = preset.Id, Name = preset.Name, Description = preset.Description, SchemaVersion = preset.SchemaVersion, Appearance = Clone(preset.Appearance), CreatedAt = preset.CreatedAt, UpdatedAt = preset.UpdatedAt, AssetCount = CountAssets(preset.Appearance) };
    private ThemePresetDto CreateDefaultPreset() => new() { Id = null, Name = "Default Theme", SchemaVersion = 1, Appearance = siteSettingsService.GetDefaultAppearance(), IsBuiltIn = true, AssetCount = 0 };
    private static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;
    private static UpdateSiteAppearanceRequest ToRequest(SiteAppearanceDto value) => new() { Theme = value.Theme, Pages = value.Pages, Background = value.Background, PanelSkin = value.PanelSkin, Icons = value.Icons, Decorations = value.Decorations };
    private static string SafeFileName(string name) => string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
    private static string CanonicalExtension(string extension) => extension.ToLowerInvariant() switch { ".png" => ".png", ".jpg" or ".jpeg" => ".jpg", ".webp" => ".webp", _ => throw new InvalidDataException("Theme pack contains an unsupported asset type.") };
    private static string ContentType(string extension) => extension switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => "application/octet-stream" };
    private static string NormalizeAssetDisplayName(string? value, string fallback)
    {
        var normalizedPath = (value ?? string.Empty).Replace('\\', '/');
        var name = normalizedPath[(normalizedPath.LastIndexOf('/') + 1)..]
            .Normalize(NormalizationForm.FormC)
            .Trim();
        name = new string(name.Where(character => !char.IsControl(character)).ToArray()).Trim();
        if (name is "" or "." or "..") name = fallback;
        if (name.Length <= MaxAssetDisplayNameLength) return name;
        var end = char.IsHighSurrogate(name[MaxAssetDisplayNameLength - 1]) ? MaxAssetDisplayNameLength - 1 : MaxAssetDisplayNameLength;
        return name[..end].TrimEnd();
    }
    private static string GetFallbackAssetDisplayName(string assetId)
    {
        var stem = Path.GetFileNameWithoutExtension(assetId);
        return $"Asset {stem[..Math.Min(8, stem.Length)].ToUpperInvariant()}";
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void TryDeleteDirectory(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { } }

    private static async Task<T> WithMutationLockAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        await MutationLock.WaitAsync(cancellationToken);
        try { return await action(); }
        finally { MutationLock.Release(); }
    }

    private sealed class StoredThemeLibrary
    {
        public int SchemaVersion { get; set; } = 1;
        public Guid? LastAppliedPresetId { get; set; }
        public List<StoredThemePreset> Presets { get; set; } = [];
        public Dictionary<string, string> AssetDisplayNames { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class StoredThemePreset
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SchemaVersion { get; set; } = 1;
        public SiteAppearanceDto Appearance { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    private sealed class ThemePackManifest
    {
        public string Format { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SchemaVersion { get; set; }
        public SiteAppearanceDto? Appearance { get; set; }
        public List<ThemePackAssetMetadata>? Assets { get; set; }
    }

    private sealed class ThemePackAssetMetadata
    {
        public string Path { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    private sealed record ValidatedThemePack(
        IReadOnlyDictionary<string, byte[]> Entries,
        HashSet<string> References,
        SiteAppearanceDto Appearance,
        string RequestedName,
        string? Description,
        IReadOnlyDictionary<string, string> DisplayNames);

    private sealed record PackAssetSource(string SourcePath, string PackPath, string? AssetId);
}
