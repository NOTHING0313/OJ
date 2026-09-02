using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Application.SiteSettings;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.SiteSettings;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Tests.SiteSettings;

public sealed class ThemeLibraryServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "onlinejudge-theme-library-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EmptyLibrary_ContainsVirtualDefaultWithoutPersistingIt()
    {
        await using var db = CreateDb();
        var context = CreateService(db);

        var result = await context.Service.ListAsync(UserRole.Root);

        var preset = Assert.Single(result.Value!.Items);
        Assert.True(preset.IsBuiltIn);
        Assert.Null(preset.Id);
        Assert.Equal("Default Theme", preset.Name);
        Assert.Empty(db.SiteSettings);
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    public async Task NonRoot_CannotAccessThemeLibrary(UserRole role)
    {
        await using var db = CreateDb();
        var context = CreateService(db);

        Assert.Equal("Forbidden.", (await context.Service.ListAsync(role)).ErrorMessage);
        Assert.Equal("Forbidden.", (await context.Service.CreateAsync(Request("Denied"), Guid.NewGuid(), role)).ErrorMessage);
        Assert.Empty(db.SiteSettings);
    }

    [Fact]
    public async Task SaveAsPreset_TrimsMetadataAndDoesNotChangeActiveAppearance()
    {
        await using var db = CreateDb();
        var context = CreateService(db);

        var result = await context.Service.CreateAsync(Request("  Midnight  ", "  compact  "), Guid.NewGuid(), UserRole.Root);

        Assert.Equal("Midnight", result.Value!.Name);
        Assert.Equal("compact", result.Value.Description);
        Assert.Equal("theme-library", Assert.Single(db.SiteSettings).Key);
        Assert.DoesNotContain(db.SiteSettings, item => item.Key == "appearance");
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemePresetCreated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Default Theme")]
    public async Task Create_RejectsInvalidOrReservedName(string name)
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        Assert.True((await context.Service.CreateAsync(Request(name), Guid.NewGuid(), UserRole.Root)).IsFailure);
    }

    [Fact]
    public async Task DuplicateRenameUpdateDelete_KeepAssetsAndPreserveLifecycle()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var created = (await context.Service.CreateAsync(Request("Alpha"), Guid.NewGuid(), UserRole.Root)).Value!;
        var duplicate = (await context.Service.DuplicateAsync(created.Id!.Value, Guid.NewGuid(), UserRole.Root)).Value!;
        var renamed = (await context.Service.RenameAsync(duplicate.Id!.Value, new RenameThemePresetRequest { Name = "Beta" }, Guid.NewGuid(), UserRole.Root)).Value!;
        var update = new UpdateThemePresetRequest { Name = renamed.Name, Description = "updated", Appearance = renamed.Appearance };
        var updated = (await context.Service.UpdateAsync(renamed.Id!.Value, update, Guid.NewGuid(), UserRole.Root)).Value!;

        Assert.Equal("Beta", updated.Name);
        Assert.Equal("updated", updated.Description);
        Assert.True((await context.Service.DeleteAsync(updated.Id!.Value, Guid.NewGuid(), UserRole.Root)).IsSuccess);
        Assert.Equal(2, (await context.Service.ListAsync(UserRole.Root)).Value!.Items.Count);
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemePresetDuplicated);
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemePresetRenamed);
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemePresetUpdated);
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemePresetDeleted);
    }

    [Fact]
    public async Task NameCollision_IsCaseInsensitive()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        Assert.True((await context.Service.CreateAsync(Request("Alpha"), Guid.NewGuid(), UserRole.Root)).IsSuccess);
        Assert.Equal("Theme preset name already exists.", (await context.Service.CreateAsync(Request("alpha"), Guid.NewGuid(), UserRole.Root)).ErrorMessage);
    }

    [Fact]
    public async Task ApplyPreset_UsesAppearanceAuthorityAndRecordsLastAppliedMetadata()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var request = Request("Violet");
        request.Appearance.Theme.AccentColor = "#123456";
        var preset = (await context.Service.CreateAsync(request, Guid.NewGuid(), UserRole.Root)).Value!;

        var result = await context.Service.ApplyAsync(preset.Id, Guid.NewGuid(), UserRole.Root, "localhost");

        Assert.Equal("#123456", result.Value!.Theme.AccentColor);
        Assert.Equal(2, db.SiteSettings.Count());
        Assert.Contains(db.SiteSettings, item => item.Key == "appearance");
        Assert.Contains(context.Audits, item => item.Action == SecurityAuditActions.ThemeApplied);
    }

    [Fact]
    public async Task ApplyDefault_IsExactDefaultAndDefaultCannotBeMutatedAsStoredPreset()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var result = await context.Service.ApplyAsync(null, Guid.NewGuid(), UserRole.Root, "localhost");

        Assert.Equal("#6E7BFF", result.Value!.Theme.AccentColor);
        Assert.Equal("Theme preset was not found.", (await context.Service.DeleteAsync(Guid.Empty, Guid.NewGuid(), UserRole.Root)).ErrorMessage);
    }

    [Fact]
    public async Task ReferencedAsset_IsReportedByPresetAndCannotBeDeleted()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var assetId = await AddAssetAsync(context.Paths, ".png", Png());
        var request = Request("With Asset");
        request.Appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = Ref(assetId) };
        Assert.True((await context.Service.CreateAsync(request, Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var assetService = new ThemeAssetService(context.Paths, context.Validator, new SecureUploadOptions(), context.Appearance, context.Service);

        var item = Assert.Single((await assetService.ListAsync(UserRole.Root)).Value!);
        Assert.Contains(item.UsedBy, value => value.StartsWith("preset:With Asset:background", StringComparison.Ordinal));
        Assert.Equal("Theme asset is currently referenced.", (await assetService.DeleteAsync(UserRole.Root, assetId)).ErrorMessage);
    }

    [Fact]
    public async Task Export_ContainsOnlyManifestAndReferencedDeduplicatedAssets()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var assetId = await AddAssetAsync(context.Paths, ".png", Png());
        var request = Request("Portable");
        request.Appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = Ref(assetId) };
        request.Appearance.Icons["problem"] = new SiteThemeIconSlotDto { Enabled = true, Asset = Ref(assetId) };
        var preset = (await context.Service.CreateAsync(request, Guid.NewGuid(), UserRole.Root)).Value!;

        using var result = (await context.Service.ExportAsync(preset.Id!.Value, UserRole.Root)).Value!;
        using var archive = new ZipArchive(result.Content, ZipArchiveMode.Read, leaveOpen: true);

        Assert.Equal(["assets/001.png", "manifest.json"], archive.Entries.Select(item => item.FullName).Order().ToArray());
        var manifest = await ReadEntryAsync(archive, "manifest.json");
        Assert.Contains("\"format\":\"onlinejudge-theme\"", manifest);
        Assert.DoesNotContain(context.Paths.ThemeAssetsRoot, manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportThenImport_CreatesPresetAndFreshAssetWithoutApplying()
    {
        await using var sourceDb = CreateDb();
        var source = CreateService(sourceDb, "source");
        var assetId = await AddAssetAsync(source.Paths, ".png", Png());
        var request = Request("Portable");
        request.Appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = Ref(assetId) };
        Assert.True((await source.Service.RegisterAssetDisplayNameAsync(assetId, "problem-icon-blue.webp", Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var preset = (await source.Service.CreateAsync(request, Guid.NewGuid(), UserRole.Root)).Value!;
        using var export = (await source.Service.ExportAsync(preset.Id!.Value, UserRole.Root)).Value!;

        await using var targetDb = CreateDb();
        var target = CreateService(targetDb, "target");
        var imported = await target.Service.ImportAsync("portable.zip", "application/zip", export.Content.Length, export.Content, Guid.NewGuid(), UserRole.Root);

        Assert.True(imported.IsSuccess);
        Assert.NotEqual(assetId, imported.Value!.Appearance.Background.Asset!.AssetId);
        Assert.True(File.Exists(target.Paths.ResolveThemeAssetPath(imported.Value.Appearance.Background.Asset.AssetId)));
        Assert.Equal("problem-icon-blue.webp", (await target.Service.GetAssetDisplayNamesAsync()).Value![imported.Value.Appearance.Background.Asset.AssetId]);
        Assert.DoesNotContain(targetDb.SiteSettings, item => item.Key == "appearance");
        Assert.Contains(target.Audits, item => item.Action == SecurityAuditActions.ThemePresetImported);

        var applied = await target.Service.ApplyAsync(imported.Value.Id, Guid.NewGuid(), UserRole.Root, "localhost");
        Assert.True(applied.IsSuccess);
        Assert.Equal(imported.Value.Appearance.Background.Asset.AssetId, applied.Value!.Background.Asset!.AssetId);
        Assert.Single(targetDb.SiteSettings, item => item.Key == "appearance");
    }

    [Fact]
    public async Task ImportCollision_UsesSuggestedSuffixWithoutOverwrite()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var pack = CreatePack("Alpha", context.Appearance.GetDefaultAppearance());
        Assert.True((await context.Service.CreateAsync(Request("Alpha"), Guid.NewGuid(), UserRole.Root)).IsSuccess);

        var imported = await context.Service.ImportAsync("theme.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root);

        Assert.Equal("Alpha (2)", imported.Value!.Name);
        Assert.Equal(3, (await context.Service.ListAsync(UserRole.Root)).Value!.Items.Count);
    }

    [Fact]
    public async Task ImportPreflight_ValidatesSummaryAndDoesNotCommitAnything()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        Assert.True((await context.Service.CreateAsync(Request("Alpha"), Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = new ThemeAssetReferenceDto { AssetId = "assets/001.png", Url = "assets/001.png" } };
        var pack = CreatePackWithEntries("Alpha", appearance, ("assets/001.png", Png()));
        var before = await db.SiteSettings.Where(item => item.Key == "theme-library").Select(item => item.Value).SingleAsync();

        var result = await context.Service.PreflightImportAsync("theme.zip", "application/zip", pack.Length, pack, UserRole.Root);

        Assert.True(result.IsSuccess);
        Assert.Equal("Alpha (2)", result.Value!.ResolvedName);
        Assert.True(result.Value.HasNameCollision);
        Assert.True(result.Value.HasBackground);
        Assert.Equal(1, result.Value.AssetCount);
        Assert.Equal(Png().LongLength, result.Value.TotalAssetBytes);
        Assert.Equal(before, await db.SiteSettings.Where(item => item.Key == "theme-library").Select(item => item.Value).SingleAsync());
        Assert.False(Directory.Exists(context.Paths.ThemeAssetsRoot));
    }

    [Fact]
    public async Task ImportPreflight_RejectsMissingReferencedAssetWithoutCommit()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = new ThemeAssetReferenceDto { AssetId = "assets/001.png", Url = "assets/001.png" } };
        var pack = CreatePack("Missing", appearance);

        Assert.True((await context.Service.PreflightImportAsync("theme.zip", "application/zip", pack.Length, pack, UserRole.Root)).IsFailure);
        Assert.Empty(db.SiteSettings);
        Assert.False(Directory.Exists(context.Paths.ThemeAssetsRoot));
    }

    [Fact]
    public async Task V1PackWithoutAssetDisplayNames_RemainsImportableWithSafeFallback()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = new ThemeAssetReferenceDto { AssetId = "assets/001.png", Url = "assets/001.png" } };
        var pack = CreatePackWithEntries("Legacy V1", appearance, ("assets/001.png", Png()));

        var imported = await context.Service.ImportAsync("legacy.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root);

        Assert.True(imported.IsSuccess);
        var assetId = imported.Value!.Appearance.Background.Asset!.AssetId;
        Assert.Equal("001.png", (await context.Service.GetAssetDisplayNamesAsync()).Value![assetId]);
    }

    [Theory]
    [InlineData("../../problem-icon-blue.webp", "problem-icon-blue.webp")]
    [InlineData("C:\\art\\panel.png", "panel.png")]
    [InlineData("bad\u0001name.png", "badname.png")]
    [InlineData("图标-蓝色.png", "图标-蓝色.png")]
    public async Task AssetDisplayName_IsNormalizedMetadataOnly(string suppliedName, string expected)
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var assetId = $"{Guid.NewGuid():N}.png";

        var result = await context.Service.RegisterAssetDisplayNameAsync(assetId, suppliedName, Guid.NewGuid(), UserRole.Root);

        Assert.Equal(expected, result.Value);
        Assert.Equal(expected, (await context.Service.GetAssetDisplayNamesAsync()).Value![assetId]);
        Assert.DoesNotContain(suppliedName, assetId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssetDisplayNames_AllowDuplicatesAndBoundLength()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var first = $"{Guid.NewGuid():N}.png";
        var second = $"{Guid.NewGuid():N}.webp";
        var longName = new string('a', 160) + ".png";

        Assert.True((await context.Service.RegisterAssetDisplayNameAsync(first, "shared.png", Guid.NewGuid(), UserRole.Root)).IsSuccess);
        Assert.True((await context.Service.RegisterAssetDisplayNameAsync(second, "shared.png", Guid.NewGuid(), UserRole.Root)).IsSuccess);
        var bounded = await context.Service.RenameAssetDisplayNameAsync(first, longName, Guid.NewGuid(), UserRole.Root);
        var unicodeBounded = await context.Service.RenameAssetDisplayNameAsync(first, new string('界', 127) + "😀.png", Guid.NewGuid(), UserRole.Root);

        Assert.Equal(128, bounded.Value!.Length);
        Assert.Equal(127, unicodeBounded.Value!.Length);
        Assert.False(char.IsHighSurrogate(unicodeBounded.Value[^1]));
        Assert.Equal("shared.png", (await context.Service.GetAssetDisplayNamesAsync()).Value![second]);
    }

    [Theory]
    [InlineData("wrong-format", 1)]
    [InlineData("onlinejudge-theme", 99)]
    public async Task Import_RejectsWrongFormatOrVersion(string format, int version)
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var pack = CreatePack("Theme", context.Appearance.GetDefaultAppearance(), format, version);
        Assert.True((await context.Service.PreflightImportAsync("theme.zip", "application/zip", pack.Length, pack, UserRole.Root)).IsFailure);
        pack.Position = 0;
        Assert.True((await context.Service.ImportAsync("theme.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root)).IsFailure);
        Assert.Empty(db.SiteSettings);
    }

    [Fact]
    public async Task Import_RejectsUnknownManifestFields()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var json = "{\"format\":\"onlinejudge-theme\",\"version\":1,\"name\":\"Bad\",\"schemaVersion\":1,\"appearance\":{},\"script\":\"x\"}";
        var pack = CreateZip(("manifest.json", System.Text.Encoding.UTF8.GetBytes(json)));
        Assert.Equal("Theme pack manifest contains unknown fields.", (await context.Service.ImportAsync("bad.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root)).ErrorMessage);
    }

    [Fact]
    public async Task Import_RejectsForgedImageAndLeavesNoPartialState()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = new ThemeAssetReferenceDto { AssetId = "assets/001.png", Url = "assets/001.png" } };
        var pack = CreatePackWithEntries("Forged", appearance, ("assets/001.png", "<script/>"u8.ToArray()));

        Assert.True((await context.Service.PreflightImportAsync("bad.zip", "application/zip", pack.Length, pack, UserRole.Root)).IsFailure);
        pack.Position = 0;
        Assert.True((await context.Service.ImportAsync("bad.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root)).IsFailure);
        Assert.Empty(db.SiteSettings);
        Assert.False(Directory.Exists(context.Paths.ThemeAssetsRoot));
    }

    [Fact]
    public async Task Import_RejectsUnknownSlotAndLeavesNoAssets()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Icons["unknown"] = null;
        var pack = CreatePack("Unknown Slot", appearance);

        Assert.Contains("Unknown theme icon slot", (await context.Service.PreflightImportAsync("bad.zip", "application/zip", pack.Length, pack, UserRole.Root)).ErrorMessage);
        pack.Position = 0;
        Assert.Contains("Unknown theme icon slot", (await context.Service.ImportAsync("bad.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Root)).ErrorMessage);
        Assert.Empty(db.SiteSettings);
    }

    [Fact]
    public async Task Library_EnforcesThirtyPresetBoundAndSerializedSizeRemainsBounded()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        for (var index = 1; index <= ThemePackContract.MaxPresets; index++)
            Assert.True((await context.Service.CreateAsync(Request($"Theme {index}"), Guid.NewGuid(), UserRole.Root)).IsSuccess);

        Assert.Equal("Theme preset limit of 30 has been reached.", (await context.Service.CreateAsync(Request("Overflow"), Guid.NewGuid(), UserRole.Root)).ErrorMessage);
        var json = await db.SiteSettings.Where(item => item.Key == "theme-library").Select(item => item.Value).SingleAsync();
        Assert.True(json.Length < 5 * 1024 * 1024);
    }

    [Fact]
    public async Task ImportAndExport_AreForbiddenForNonRoot()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var pack = CreatePack("Denied", context.Appearance.GetDefaultAppearance());
        Assert.Equal("Forbidden.", (await context.Service.ImportAsync("theme.zip", "application/zip", pack.Length, pack, Guid.NewGuid(), UserRole.Answerer)).ErrorMessage);
        Assert.Equal("Forbidden.", (await context.Service.ExportAsync(Guid.NewGuid(), UserRole.ProblemSetter)).ErrorMessage);
    }

    [Theory]
    [InlineData("../escape.png")]
    [InlineData("C:/escape.png")]
    [InlineData("assets/run.js")]
    [InlineData("extra.txt")]
    public async Task SecureExtractor_RejectsUnsafeOrUnexpectedEntries(string entryName)
    {
        var extractor = new SecureArchiveExtractor(new SecureUploadOptions());
        var zip = CreateZip(("manifest.json", "{}"u8.ToArray()), (entryName, Png()));
        Assert.True((await extractor.ExtractThemePackAsync(zip)).IsFailure);
    }

    [Fact]
    public async Task SecureExtractor_RejectsSymlinkEntry()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = archive.CreateEntry("manifest.json");
            await using (var output = manifest.Open()) await output.WriteAsync("{}"u8.ToArray());
            var link = archive.CreateEntry("assets/link.png");
            link.ExternalAttributes = 0xa000 << 16;
        }
        stream.Position = 0;
        Assert.True((await new SecureArchiveExtractor(new SecureUploadOptions()).ExtractThemePackAsync(stream)).IsFailure);
    }

    [Fact]
    public async Task ImportPreflight_RejectsZipSlipAndCompressionBombBeforePreview()
    {
        await using var db = CreateDb();
        var context = CreateService(db);
        var zipSlip = CreateZip(("manifest.json", "{}"u8.ToArray()), ("../escape.png", Png()));
        Assert.True((await context.Service.PreflightImportAsync("bad.zip", "application/zip", zipSlip.Length, zipSlip, UserRole.Root)).IsFailure);
        var svg = CreateZip(("manifest.json", "{}"u8.ToArray()), ("assets/001.svg", "<svg/>"u8.ToArray()));
        Assert.True((await context.Service.PreflightImportAsync("svg.zip", "application/zip", svg.Length, svg, UserRole.Root)).IsFailure);

        var appearance = context.Appearance.GetDefaultAppearance();
        appearance.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = new ThemeAssetReferenceDto { AssetId = "assets/001.png", Url = "assets/001.png" } };
        var bomb = CreatePackWithEntries("Bomb", appearance, ("assets/001.png", new byte[1024 * 1024]));
        Assert.True((await context.Service.PreflightImportAsync("bomb.zip", "application/zip", bomb.Length, bomb, UserRole.Root)).IsFailure);
        Assert.Empty(db.SiteSettings);
    }

    [Fact]
    public void Endpoints_AreRootOnlyAndMutationsRetainRateLimits()
    {
        var authorize = Assert.Single(typeof(ThemeLibraryController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("RequireRoot", authorize.Policy);
        foreach (var method in new[] { nameof(ThemeLibraryController.Create), nameof(ThemeLibraryController.Update), nameof(ThemeLibraryController.Apply), nameof(ThemeLibraryController.ApplyDefault), nameof(ThemeLibraryController.PreflightImport), nameof(ThemeLibraryController.Import) })
        {
            Assert.NotEmpty(typeof(ThemeLibraryController).GetMethod(method)!.GetCustomAttributes(typeof(RiskRateLimitAttribute), true));
        }
    }

    [Fact]
    public void AuditMetadata_ContainsNoAppearanceArchiveOrPath()
    {
        var allowed = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "OnlineJudge.Infrastructure", "SecurityAudit", "SecurityAuditWriter.cs"));
        Assert.Contains("presetName", allowed);
        Assert.DoesNotContain("appearanceJson", allowed);
        Assert.DoesNotContain("archiveBytes", allowed);
        Assert.DoesNotContain("serverPath", allowed);
    }

    private ServiceContext CreateService(OnlineJudgeDbContext db, string directory = "default")
    {
        var paths = new RuntimeStoragePathProvider(Path.Combine(root, directory, "api"), themeAssetsRoot: Path.Combine(root, directory, "theme-assets"));
        var options = new SecureUploadOptions();
        var validator = new SecureUploadValidator(options);
        var appearance = new SiteSettingsService(db, storagePaths: paths);
        var audit = new CapturingAuditWriter();
        var service = new ThemeLibraryService(db, appearance, paths, new SecureArchiveExtractor(options), validator, audit, TimeProvider.System);
        return new ServiceContext(service, appearance, paths, validator, audit.Records);
    }

    private static CreateThemePresetRequest Request(string name, string? description = null) => new()
    {
        Name = name,
        Description = description,
        Appearance = new SiteSettingsService(CreateDb()).GetDefaultAppearance()
    };

    private static OnlineJudgeDbContext CreateDb() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static ThemeAssetReferenceDto Ref(string assetId) => new() { AssetId = assetId, Url = $"/theme-assets/{assetId}" };
    private static byte[] Png() => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0];

    private static async Task<string> AddAssetAsync(RuntimeStoragePathProvider paths, string extension, byte[] bytes)
    {
        var assetId = $"{Guid.NewGuid():N}{extension}";
        await using var content = new MemoryStream(bytes);
        await paths.WriteThemeAssetAsync(assetId, content, 1024);
        return assetId;
    }

    private static MemoryStream CreatePack(string name, SiteAppearanceDto appearance, string format = ThemePackContract.Format, int version = 1)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { format, version, name, description = (string?)null, schemaVersion = 1, appearance }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return CreateZip(("manifest.json", manifest));
    }

    private static MemoryStream CreatePackWithEntries(string name, SiteAppearanceDto appearance, params (string Name, byte[] Content)[] entries)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { format = ThemePackContract.Format, version = 1, name, description = (string?)null, schemaVersion = 1, appearance }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return CreateZip([("manifest.json", manifest), .. entries]);
    }

    private static MemoryStream CreateZip(params (string Name, byte[] Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var output = entry.Open();
                output.Write(content);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
    {
        await using var input = archive.GetEntry(name)!.Open();
        using var reader = new StreamReader(input);
        return await reader.ReadToEndAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class CapturingAuditWriter : ISecurityAuditWriter
    {
        public List<SecurityAuditRecord> Records { get; } = [];
        public void Stage(SecurityAuditRecord record) => Records.Add(record);
        public Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) { Records.Add(record); return Task.CompletedTask; }
    }

    private sealed record ServiceContext(ThemeLibraryService Service, SiteSettingsService Appearance, RuntimeStoragePathProvider Paths, SecureUploadValidator Validator, IReadOnlyList<SecurityAuditRecord> Audits);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
