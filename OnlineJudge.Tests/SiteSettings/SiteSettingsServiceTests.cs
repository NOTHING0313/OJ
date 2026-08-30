using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.SiteSettings;

namespace OnlineJudge.Tests.SiteSettings;

public class SiteSettingsServiceTests
{
    [Fact]
    public async Task MissingAppearance_ReturnsDefault()
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.65, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.Equal(0.72, result.Value.Theme.PanelOpacity);
        Assert.Equal(12, result.Value.Theme.PanelBlur);
        Assert.Equal("#11141A", result.Value.Theme.PanelColor);
        Assert.Equal(0.14, result.Value.Theme.PanelBorderOpacity);
        Assert.Equal("#F2F4F8", result.Value.Theme.TextPrimaryColor);
        Assert.Equal("#6E7BFF", result.Value.Theme.AccentColor);
        Assert.Equal(0.58, result.Value.Theme.NavOpacity);
        Assert.Equal(18, result.Value.Theme.NavBlur);
        Assert.Equal("system", result.Value.Theme.FontPreset);
        Assert.Contains("global", result.Value.Pages.Keys);
        Assert.Contains("file-task", result.Value.Pages.Keys);
        Assert.False(result.Value.Background.Enabled);
        Assert.Null(result.Value.Background.Asset);
        Assert.False(result.Value.PanelSkin.Enabled);
        Assert.Null(result.Value.PanelSkin.BackgroundTexture);
        Assert.Empty(result.Value.Icons);
        Assert.Empty(result.Value.Decorations);
    }

    [Fact]
    public async Task Root_CanUpdateMultiPageAppearance()
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var userId = Guid.NewGuid();

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: "/uploads/images/problems.png"), userId, UserRole.Root);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.5, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.Equal(0.7, result.Value.Theme.PanelOpacity);
        Assert.Equal(14, result.Value.Theme.PanelBlur);
        Assert.Equal("#101820", result.Value.Theme.PanelColor);
        Assert.Equal("#F8FAFC", result.Value.Theme.TextPrimaryColor);
        Assert.Equal("#7080FF", result.Value.Theme.AccentColor);
        Assert.Equal(0.76, result.Value.Theme.NavOpacity);
        Assert.Equal("readable", result.Value.Theme.FontPreset);
        Assert.Equal("/uploads/images/problems.png", result.Value.Pages["problems"].ImageUrl);
        Assert.True(result.Value.Pages["problems"].Enabled);
        Assert.Equal(1.2, result.Value.Pages["problems"].Scale);
        Assert.Equal(0.35, result.Value.Pages["problems"].OverlayOpacity);

        var setting = await dbContext.SiteSettings.SingleAsync();
        Assert.Equal("appearance", setting.Key);
        Assert.Equal(userId, setting.UpdatedByUserId);
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    public async Task NonRoot_CannotUpdateSiteAppearance(UserRole role)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(), Guid.NewGuid(), role);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden.", result.ErrorMessage);
        Assert.Empty(dbContext.SiteSettings);
    }

    [Theory]
    [InlineData("https://example.com/background.png")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("/uploads/images/../secret.png")]
    [InlineData("/other/background.png")]
    public async Task UpdateSiteAppearance_RejectsUnsafeUrl(string url)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: url), Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background image URL must point to uploaded images.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("http://localhost:5101/uploads/images/background.png")]
    [InlineData("http://127.0.0.1:5101/uploads/images/background.png")]
    [InlineData("https://oj.local/uploads/images/background.png")]
    public async Task LocalUploadUrl_NormalizedToRelativePath(string url)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: url), Guid.NewGuid(), UserRole.Root, "oj.local");

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/images/background.png", result.Value!.Pages["problems"].ImageUrl);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task UpdateSiteAppearance_RejectsInvalidOpacity(double opacity)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Theme.BackgroundOverlayOpacity = opacity;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background overlay opacity must be between 0 and 1.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0.34)]
    [InlineData(0.96)]
    public async Task UpdateSiteAppearance_RejectsInvalidPanelOpacity(double opacity)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Theme.PanelOpacity = opacity;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Panel opacity must be between 0.35 and 0.95.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("#fff")]
    [InlineData("FFFFFF")]
    [InlineData("#GGGGGG")]
    public async Task UpdateSiteAppearance_RejectsInvalidThemeColor(string color)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Theme.PanelColor = color;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Theme colors must use #RRGGBB format.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task UpdateSiteAppearance_RejectsInvalidPageOverlay(double opacity)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Pages["problems"].OverlayOpacity = opacity;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background overlay opacity for problems must be between 0 and 1.", result.ErrorMessage);
    }

    [Fact]
    public async Task OldStructuredAppearance_GetsNewThemeDefaults()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SiteSettings.Add(new SiteSetting
        {
            Id = Guid.NewGuid(),
            Key = "appearance",
            Value = "{\"theme\":{\"backgroundEnabled\":true,\"backgroundOverlayOpacity\":0.4,\"panelOpacity\":0.7,\"panelBlur\":8},\"pages\":{\"global\":{\"enabled\":true,\"imageUrl\":\"/uploads/images/old.png\",\"positionX\":50,\"positionY\":50,\"scale\":1}}}",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new SiteSettingsService(dbContext);

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("#11141A", result.Value!.Theme.PanelColor);
        Assert.Equal("#F2F4F8", result.Value.Theme.TextPrimaryColor);
        Assert.Equal("system", result.Value.Theme.FontPreset);
        Assert.Null(result.Value.Pages["global"].OverlayOpacity);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.51)]
    public async Task UpdateSiteAppearance_RejectsInvalidScale(double scale)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Pages["problems"].Scale = scale;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background scale for problems must be between 0.5 and 2.5.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(101, 50)]
    [InlineData(50, -1)]
    [InlineData(50, 101)]
    public async Task UpdateSiteAppearance_RejectsInvalidPosition(double positionX, double positionY)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Pages["problems"].PositionX = positionX;
        request.Pages["problems"].PositionY = positionY;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Contains("must be between 0 and 100.", result.ErrorMessage);
    }

    [Fact]
    public async Task OldAppearanceJson_CanBeReadWithoutThrowing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SiteSettings.Add(new SiteSetting
        {
            Id = Guid.NewGuid(),
            Key = "appearance",
            Value = "{\"backgroundImageUrl\":\"/uploads/images/old.png\",\"backgroundOverlayOpacity\":0.4,\"backgroundEnabled\":true}",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new SiteSettingsService(dbContext);

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.4, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.True(result.Value.Pages["global"].Enabled);
        Assert.Equal("/uploads/images/old.png", result.Value.Pages["global"].ImageUrl);
    }

    [Theory]
    [InlineData("stretch", "no-repeat", "scroll")]
    [InlineData("cover", "space", "scroll")]
    [InlineData("cover", "no-repeat", "local")]
    public async Task UpdateSiteAppearance_RejectsUnsupportedBackgroundModes(string sizeMode, string repeat, string attachment)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Background = new SiteThemeBackgroundDto { SizeMode = sizeMode, Repeat = repeat, Attachment = attachment };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(101, 50)]
    [InlineData(50, -1)]
    [InlineData(50, 101)]
    public async Task UpdateSiteAppearance_RejectsOutOfRangeGenericBackgroundPosition(double positionX, double positionY)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Background = new SiteThemeBackgroundDto { PositionX = positionX, PositionY = positionY };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.Equal("Theme background position must be between 0 and 100.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(21, 100)]
    [InlineData(0, 49)]
    [InlineData(0, 151)]
    public async Task UpdateSiteAppearance_RejectsOutOfRangeBackgroundEffects(double blur, double brightness)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Background = new SiteThemeBackgroundDto { Blur = blur, Brightness = brightness };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(-0.01, null, null)]
    [InlineData(1.01, null, null)]
    [InlineData(null, -1.0, null)]
    [InlineData(null, 33.0, null)]
    [InlineData(null, null, -0.01)]
    [InlineData(null, null, 1.01)]
    public async Task UpdateSiteAppearance_RejectsOutOfRangePanelSkin(double? opacity, double? radius, double? shadow)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.PanelSkin = new SitePanelSkinDto { BackgroundOpacity = opacity, Radius = radius, ShadowStrength = shadow };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("unknown", true)]
    [InlineData("Problem", true)]
    [InlineData("unknown", false)]
    [InlineData("PageHeader", false)]
    public async Task UpdateSiteAppearance_RejectsUnknownThemeSlots(string slot, bool icon)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        if (icon) request.Icons[slot] = null;
        else request.Decorations[slot] = null;

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Contains("Unknown theme", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.01, 1, 0, 0)]
    [InlineData(1.01, 1, 0, 0)]
    [InlineData(1, 0.49, 0, 0)]
    [InlineData(1, 2.01, 0, 0)]
    [InlineData(1, 1, -65, 0)]
    [InlineData(1, 1, 0, 65)]
    public async Task UpdateSiteAppearance_RejectsOutOfRangeSlotValues(double opacity, double scale, double offsetX, double offsetY)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Icons["problem"] = new SiteThemeIconSlotDto { Opacity = opacity, Scale = scale, OffsetX = offsetX, OffsetY = offsetY };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("left", "top-right")]
    [InlineData("end", "center")]
    public async Task UpdateSiteAppearance_RejectsUnsupportedDecorationPlacement(string alignment, string corner)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Decorations["panelCorner"] = new SiteThemeDecorationSlotDto { Alignment = alignment, Corner = corner };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("https://example.com/icon.png")]
    [InlineData("/theme-assets/not-managed.png")]
    public async Task UpdateSiteAppearance_RejectsRemoteOrUnmanagedSlotAsset(string url)
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        request.Icons["problem"] = new SiteThemeIconSlotDto
        {
            Enabled = true,
            Asset = new ThemeAssetReferenceDto { AssetId = "not-managed.png", Url = url }
        };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Root_CanAssignOneAssetToMultipleSlotsAndPreserveExistingThemeNodes()
    {
        await using var dbContext = CreateDbContext();
        var request = CreateRequest();
        var assetId = $"{Guid.NewGuid():N}.webp";
        var asset = new ThemeAssetReferenceDto { AssetId = assetId, Url = $"/theme-assets/{assetId}" };
        request.Background = new SiteThemeBackgroundDto { Enabled = true, Asset = asset };
        request.PanelSkin = new SitePanelSkinDto { Enabled = true, Radius = 10 };
        request.Icons["problem"] = new SiteThemeIconSlotDto { Enabled = true, Asset = asset, Opacity = 0.9, Scale = 1.1 };
        request.Icons["leaderboard"] = new SiteThemeIconSlotDto { Enabled = true, Asset = asset };
        request.Decorations["pageHeader"] = new SiteThemeDecorationSlotDto { Enabled = true, Asset = asset, Alignment = "end", Corner = "top-right" };

        var result = await new SiteSettingsService(dbContext).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsSuccess);
        Assert.Equal(assetId, result.Value!.Background.Asset!.AssetId);
        Assert.True(result.Value.PanelSkin.Enabled);
        Assert.Equal(assetId, result.Value.Icons["problem"]!.Asset!.AssetId);
        Assert.Equal(assetId, result.Value.Icons["leaderboard"]!.Asset!.AssetId);
        Assert.Equal(assetId, result.Value.Decorations["pageHeader"]!.Asset!.AssetId);
    }

    [Fact]
    public async Task Root_CanSaveGenericThemeAndAuditContainsOnlySafeChangeMetadata()
    {
        await using var dbContext = CreateDbContext();
        var audit = new CapturingAuditWriter();
        var request = CreateRequest();
        var assetId = $"{Guid.NewGuid():N}.png";
        request.Background = new SiteThemeBackgroundDto
        {
            Enabled = true,
            Asset = new ThemeAssetReferenceDto { AssetId = assetId, Url = $"/theme-assets/{assetId}" },
            PositionX = 42,
            PositionY = 58,
            SizeMode = "cover",
            Repeat = "no-repeat",
            Attachment = "fixed",
            OverlayColor = "#101820",
            OverlayOpacity = 0.45,
            Blur = 4,
            Brightness = 90
        };
        request.PanelSkin = new SitePanelSkinDto { Enabled = true, BackgroundOpacity = 0.8, TextureOpacity = 0.2, Radius = 12, ShadowStrength = 0.3 };

        var result = await new SiteSettingsService(dbContext, audit).UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsSuccess);
        Assert.Equal("cover", result.Value!.Background.SizeMode);
        Assert.True(result.Value.PanelSkin.Enabled);
        var record = Assert.Single(audit.Records);
        Assert.Equal(SecurityAuditActions.SiteAppearanceUpdated, record.Action);
        Assert.Equal(["backgroundEnabledChanged", "changedAssetSlots", "changedDecorationSlots", "changedIconSlots", "panelSkinEnabledChanged"], record.Metadata!.Keys.Order());
        Assert.DoesNotContain(assetId, string.Join('|', record.Metadata.Values), StringComparison.Ordinal);
    }

    private static UpdateSiteAppearanceRequest CreateRequest(string pageImageUrl = "/uploads/images/background.png")
    {
        return new UpdateSiteAppearanceRequest
        {
            Theme = new SiteAppearanceThemeDto
            {
                BackgroundEnabled = true,
                BackgroundOverlayOpacity = 0.5,
                PanelOpacity = 0.7,
                PanelBlur = 14,
                PanelColor = "#101820",
                PanelBorderOpacity = 0.18,
                TextPrimaryColor = "#F8FAFC",
                TextSecondaryColor = "#C2CAD8",
                TextMutedColor = "#8893A5",
                AccentColor = "#7080FF",
                NavOpacity = 0.76,
                NavBlur = 12,
                NavTextColor = "#DDE3ED",
                NavActiveColor = "#FFFFFF",
                FontPreset = "readable"
            },
            Pages = new Dictionary<string, SitePageBackgroundDto>
            {
                ["global"] = new()
                {
                    Enabled = false,
                    ImageUrl = null,
                    PositionX = 50,
                    PositionY = 50,
                    Scale = 1
                },
                ["problems"] = new()
                {
                    Enabled = true,
                    ImageUrl = pageImageUrl,
                    PositionX = 52,
                    PositionY = 46,
                    Scale = 1.2,
                    OverlayOpacity = 0.35
                }
            }
        };
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private sealed class CapturingAuditWriter : ISecurityAuditWriter
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public void Stage(SecurityAuditRecord record) => Records.Add(record);

        public Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }
    }
}
