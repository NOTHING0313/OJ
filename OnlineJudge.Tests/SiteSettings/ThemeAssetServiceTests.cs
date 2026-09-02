using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.SiteSettings;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Tests.SiteSettings;

public sealed class ThemeAssetServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "onlinejudge-theme-asset-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task RootUpload_AcceptsValidatedImagesAndReturnsOnlyManagedSameOriginData(string fileName, string contentType, byte[] bytes, string extension)
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        await using var content = new MemoryStream(bytes);

        var result = await service.UploadAsync(UserRole.Root, fileName, contentType, bytes.LongLength, content);

        Assert.True(result.IsSuccess);
        Assert.EndsWith(extension, result.Value!.AssetId, StringComparison.Ordinal);
        Assert.Equal($"/theme-assets/{result.Value.AssetId}", result.Value.Url);
        Assert.Equal(Path.GetFileName(fileName), result.Value.DisplayName);
        Assert.DoesNotContain(paths.ThemeAssetsRoot, System.Text.Json.JsonSerializer.Serialize(result.Value), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(paths.ResolveThemeAssetPath(result.Value.AssetId)));
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    public async Task NonRootUpload_IsForbidden(UserRole role)
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        await using var content = new MemoryStream(Png());

        var result = await service.UploadAsync(role, "theme.png", "image/png", content.Length, content);

        Assert.Equal("Forbidden.", result.ErrorMessage);
        Assert.False(Directory.Exists(paths.ThemeAssetsRoot));
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    public async Task NonRootAssetLibrary_IsForbidden(UserRole role)
    {
        await using var db = CreateDb();
        var (service, _) = CreateService(db);

        var result = await service.ListAsync(role);

        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("theme.svg", "image/svg+xml")]
    [InlineData("theme.png", "image/png")]
    public async Task Upload_RejectsSvgAndForgedImages(string fileName, string contentType)
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        await using var content = new MemoryStream("<svg><script/></svg>"u8.ToArray());

        var result = await service.UploadAsync(UserRole.Root, fileName, contentType, content.Length, content);

        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(paths.ThemeAssetsRoot));
    }

    [Fact]
    public async Task Upload_EnforcesFiveMegabytePolicyBeforeStorage()
    {
        await using var db = CreateDb();
        var options = new SecureUploadOptions { ImageMaxBytes = 8 };
        var (service, paths) = CreateService(db, options);
        await using var content = new MemoryStream(Png());

        var result = await service.UploadAsync(UserRole.Root, "theme.png", "image/png", content.Length, content);

        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(paths.ThemeAssetsRoot));
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("C:\\escape.png")]
    [InlineData("not-managed.png")]
    public async Task Delete_RejectsPathsAndNonManagedIds(string assetId)
    {
        await using var db = CreateDb();
        var (service, _) = CreateService(db);

        var result = await service.DeleteAsync(UserRole.Root, assetId);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Delete_RefusesAnAssetReferencedByCurrentAppearance()
    {
        await using var db = CreateDb();
        var (service, paths, appearanceService) = CreateServiceWithAppearance(db);
        var assetId = $"{Guid.NewGuid():N}.png";
        Directory.CreateDirectory(paths.ThemeAssetsRoot);
        await File.WriteAllBytesAsync(paths.ResolveThemeAssetPath(assetId), Png());
        var request = new UpdateSiteAppearanceRequest
        {
            Background = new SiteThemeBackgroundDto
            {
                Enabled = true,
                Asset = new ThemeAssetReferenceDto { AssetId = assetId, Url = $"/theme-assets/{assetId}" }
            }
        };

        Assert.True((await appearanceService.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var result = await service.DeleteAsync(UserRole.Root, assetId);

        Assert.Equal("Theme asset is currently referenced.", result.ErrorMessage);
        Assert.True(File.Exists(paths.ResolveThemeAssetPath(assetId)));
    }

    [Fact]
    public async Task AssetLibrary_ReportsMultiSlotReuseAndDeleteRemainsProtected()
    {
        await using var db = CreateDb();
        var (service, paths, appearanceService) = CreateServiceWithAppearance(db);
        var assetId = $"{Guid.NewGuid():N}.webp";
        Directory.CreateDirectory(paths.ThemeAssetsRoot);
        await File.WriteAllBytesAsync(paths.ResolveThemeAssetPath(assetId), WebP());
        var asset = new ThemeAssetReferenceDto { AssetId = assetId, Url = $"/theme-assets/{assetId}" };
        var request = new UpdateSiteAppearanceRequest
        {
            Icons = new Dictionary<string, SiteThemeIconSlotDto?>
            {
                ["problem"] = new() { Enabled = true, Asset = asset },
                ["leaderboard"] = new() { Enabled = true, Asset = asset }
            },
            Decorations = new Dictionary<string, SiteThemeDecorationSlotDto?>
            {
                ["pageHeader"] = new() { Enabled = true, Asset = asset, Alignment = "end" }
            }
        };

        Assert.True((await appearanceService.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var library = await service.ListAsync(UserRole.Root);
        var item = Assert.Single(library.Value!);

        Assert.Equal(assetId, item.AssetId);
        Assert.Equal(["icon:problem", "icon:leaderboard", "decoration:pageHeader"], item.UsedBy);
        Assert.Equal("Theme asset is currently referenced.", (await service.DeleteAsync(UserRole.Root, assetId)).ErrorMessage);
    }

    [Fact]
    public async Task AssetLibrary_ExcludesNonManagedFilesAndReturnsNoDiskPath()
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        Directory.CreateDirectory(paths.ThemeAssetsRoot);
        var assetId = $"{Guid.NewGuid():N}.png";
        await File.WriteAllBytesAsync(paths.ResolveThemeAssetPath(assetId), Png());
        await File.WriteAllTextAsync(Path.Combine(paths.ThemeAssetsRoot, "notes.txt"), "not an asset");

        var result = await service.ListAsync(UserRole.Root);

        var asset = Assert.Single(result.Value!);
        Assert.Equal(assetId, asset.AssetId);
        Assert.Empty(asset.UsedBy);
        Assert.DoesNotContain(paths.ThemeAssetsRoot, System.Text.Json.JsonSerializer.Serialize(asset), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Upload_PersistsDisplayNameWithoutChangingGeneratedPhysicalName()
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        await using var content = new MemoryStream(Png());

        var uploaded = (await service.UploadAsync(Guid.NewGuid(), UserRole.Root, "problem-icon-blue.png", "image/png", content.Length, content)).Value!;

        Assert.Equal("problem-icon-blue.png", uploaded.DisplayName);
        Assert.Matches("^[0-9a-f]{32}\\.png$", uploaded.AssetId);
        Assert.True(File.Exists(paths.ResolveThemeAssetPath(uploaded.AssetId)));
        Assert.DoesNotContain("problem-icon-blue.png", paths.ResolveThemeAssetPath(uploaded.AssetId), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rename_ChangesOnlyDisplayMetadataAndDeleteRemovesIt()
    {
        await using var db = CreateDb();
        var (service, paths) = CreateService(db);
        await using var content = new MemoryStream(Png());
        var uploaded = (await service.UploadAsync(Guid.NewGuid(), UserRole.Root, "before.png", "image/png", content.Length, content)).Value!;
        var originalPath = paths.ResolveThemeAssetPath(uploaded.AssetId);

        var renamed = await service.RenameAsync(Guid.NewGuid(), UserRole.Root, uploaded.AssetId, "folder/after.png");

        Assert.Equal("after.png", renamed.Value!.DisplayName);
        Assert.Equal(uploaded.AssetId, renamed.Value.AssetId);
        Assert.True(File.Exists(originalPath));
        Assert.Equal("after.png", Assert.Single((await service.ListAsync(UserRole.Root)).Value!).DisplayName);
        Assert.True((await service.DeleteAsync(Guid.NewGuid(), UserRole.Root, uploaded.AssetId)).IsSuccess);
        Assert.False(File.Exists(originalPath));
        Assert.DoesNotContain(uploaded.AssetId, await db.SiteSettings.Select(item => item.Value).SingleAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalThemeRoot_PreservesAssetAcrossReleaseDirectories()
    {
        var persistentRoot = Path.Combine(root, "persistent-theme-assets");
        var releaseA = new RuntimeStoragePathProvider(Path.Combine(root, "release-a"), themeAssetsRoot: persistentRoot);
        var releaseB = new RuntimeStoragePathProvider(Path.Combine(root, "release-b"), themeAssetsRoot: persistentRoot);
        var assetId = $"{Guid.NewGuid():N}.png";
        await using var content = new MemoryStream(Png());

        await releaseA.WriteThemeAssetAsync(assetId, content, 1024);

        Assert.Equal(releaseA.ThemeAssetsRoot, releaseB.ThemeAssetsRoot);
        Assert.True(File.Exists(releaseB.ResolveThemeAssetPath(assetId)));
    }

    [Fact]
    public void ThemeAssetEndpoints_RequireRootAndRetainRiskRateLimits()
    {
        var authorize = Assert.Single(typeof(ThemeAssetsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var upload = typeof(ThemeAssetsController).GetMethod(nameof(ThemeAssetsController.Upload))!;
        var delete = typeof(ThemeAssetsController).GetMethod(nameof(ThemeAssetsController.Delete))!;

        Assert.Equal("RequireRoot", authorize.Policy);
        Assert.Contains(upload.GetCustomAttributes(typeof(RiskRateLimitAttribute), true).Cast<RiskRateLimitAttribute>(), item => item.PolicyName == RateLimitPolicies.Upload);
        Assert.Contains(delete.GetCustomAttributes(typeof(RiskRateLimitAttribute), true).Cast<RiskRateLimitAttribute>(), item => item.PolicyName == RateLimitPolicies.AdminMutation);
    }

    public static IEnumerable<object[]> ValidImages()
    {
        yield return ["theme.png", "image/png", Png(), ".png"];
        yield return ["theme.jpeg", "image/jpeg", Jpeg(), ".jpg"];
        yield return ["theme.webp", "image/webp", WebP(), ".webp"];
    }

    private (ThemeAssetService Service, RuntimeStoragePathProvider Paths) CreateService(OnlineJudgeDbContext db, SecureUploadOptions? options = null)
    {
        var (service, paths, _) = CreateServiceWithAppearance(db, options);
        return (service, paths);
    }

    private (ThemeAssetService Service, RuntimeStoragePathProvider Paths, SiteSettingsService AppearanceService) CreateServiceWithAppearance(OnlineJudgeDbContext db, SecureUploadOptions? options = null)
    {
        var paths = new RuntimeStoragePathProvider(Path.Combine(root, "api"), themeAssetsRoot: Path.Combine(root, "theme-assets"));
        var appearanceService = new SiteSettingsService(db, storagePaths: paths);
        var uploadOptions = options ?? new SecureUploadOptions();
        var validator = new SecureUploadValidator(uploadOptions);
        var libraryService = new ThemeLibraryService(db, appearanceService, paths, new SecureArchiveExtractor(uploadOptions), validator, new NullAuditWriter(), TimeProvider.System);
        return (new ThemeAssetService(paths, validator, uploadOptions, appearanceService, libraryService), paths, appearanceService);
    }

    private static OnlineJudgeDbContext CreateDb() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static byte[] Png() => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0];

    private static byte[] Jpeg() => [0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0xff, 0xd9];

    private static byte[] WebP() => [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray()];

    private sealed class NullAuditWriter : ISecurityAuditWriter
    {
        public void Stage(SecurityAuditRecord record) { }
        public Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
