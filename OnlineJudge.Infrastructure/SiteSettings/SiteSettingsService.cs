using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Infrastructure.Storage;

namespace OnlineJudge.Infrastructure.SiteSettings;

public class SiteSettingsService(OnlineJudgeDbContext dbContext, ISecurityAuditWriter? auditWriter = null, IRuntimeStoragePathProvider? storagePaths = null) : ISiteSettingsService
{
    private const string AppearanceKey = "appearance";
    private const string UploadImagePrefix = "/uploads/images/";
    private const string ThemeAssetPrefix = "/theme-assets/";

    private static readonly string[] SupportedPageKeys =
    [
        "global",
        "problems",
        "challenges",
        "leaderboards",
        "profile",
        "account-settings",
        "admin-problems",
        "admin-challenges",
        "file-task",
        "submissions"
    ];

    private static readonly Dictionary<string, string[]> LegacyPageAliases = new()
    {
        ["problems"] = ["problems-list", "problem-detail"],
        ["admin-problems"] = ["admin"],
        ["admin-challenges"] = ["admin"],
        ["file-task"] = ["challenges"]
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<SiteAppearanceDto>> GetAppearanceAsync(CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == AppearanceKey, cancellationToken);

        if (setting is null)
        {
            return Result<SiteAppearanceDto>.Success(CreateDefaultAppearance());
        }

        try
        {
            return Result<SiteAppearanceDto>.Success(ReadAppearance(setting.Value));
        }
        catch (JsonException)
        {
            return Result<SiteAppearanceDto>.Success(CreateDefaultAppearance());
        }
    }

    public async Task<Result<SiteAppearanceDto>> UpdateAppearanceAsync(UpdateSiteAppearanceRequest request, Guid currentUserId, UserRole currentUserRole, string? requestHost = null, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Root)
        {
            return Result<SiteAppearanceDto>.Failure("Forbidden.");
        }

        var validationError = ValidateTheme(request.Theme);
        if (validationError is not null)
        {
            return Result<SiteAppearanceDto>.Failure(validationError);
        }

        validationError = ValidateBackground(request.Background);
        if (validationError is not null)
        {
            return Result<SiteAppearanceDto>.Failure(validationError);
        }

        validationError = ValidatePanelSkin(request.PanelSkin);
        if (validationError is not null)
        {
            return Result<SiteAppearanceDto>.Failure(validationError);
        }

        var requestedIcons = request.Icons ?? new Dictionary<string, SiteThemeIconSlotDto?>();
        var requestedDecorations = request.Decorations ?? new Dictionary<string, SiteThemeDecorationSlotDto?>();

        validationError = ValidateIconSlots(requestedIcons);
        if (validationError is not null)
        {
            return Result<SiteAppearanceDto>.Failure(validationError);
        }

        validationError = ValidateDecorationSlots(requestedDecorations);
        if (validationError is not null)
        {
            return Result<SiteAppearanceDto>.Failure(validationError);
        }

        if (!TryNormalizeThemeAssetReference(request.Background.Asset, out var backgroundAsset)
            || !TryNormalizeThemeAssetReference(request.PanelSkin.BackgroundTexture, out var panelBackgroundAsset)
            || !TryNormalizeThemeAssetReference(request.PanelSkin.HeaderTexture, out var panelHeaderAsset)
            || !TryNormalizeThemeAssetReference(request.PanelSkin.BorderTexture, out var panelBorderAsset))
        {
            return Result<SiteAppearanceDto>.Failure("Theme assets must use server-managed same-origin references.");
        }

        var appearance = CreateDefaultAppearance();
        appearance.Theme = new SiteAppearanceThemeDto
        {
            BackgroundEnabled = request.Theme.BackgroundEnabled,
            BackgroundOverlayOpacity = request.Theme.BackgroundOverlayOpacity,
            PanelOpacity = request.Theme.PanelOpacity,
            PanelBlur = request.Theme.PanelBlur,
            PanelColor = request.Theme.PanelColor,
            PanelBorderOpacity = request.Theme.PanelBorderOpacity,
            TextPrimaryColor = request.Theme.TextPrimaryColor,
            TextSecondaryColor = request.Theme.TextSecondaryColor,
            TextMutedColor = request.Theme.TextMutedColor,
            AccentColor = request.Theme.AccentColor,
            NavOpacity = request.Theme.NavOpacity,
            NavBlur = request.Theme.NavBlur,
            NavTextColor = request.Theme.NavTextColor,
            NavActiveColor = request.Theme.NavActiveColor,
            FontPreset = request.Theme.FontPreset
        };
        appearance.Background = new SiteThemeBackgroundDto
        {
            Enabled = request.Background.Enabled,
            Asset = backgroundAsset,
            PositionX = request.Background.PositionX,
            PositionY = request.Background.PositionY,
            SizeMode = request.Background.SizeMode?.ToLowerInvariant(),
            Repeat = request.Background.Repeat?.ToLowerInvariant(),
            Attachment = request.Background.Attachment?.ToLowerInvariant(),
            OverlayColor = request.Background.OverlayColor?.ToUpperInvariant(),
            OverlayOpacity = request.Background.OverlayOpacity,
            Blur = request.Background.Blur,
            Brightness = request.Background.Brightness
        };
        appearance.PanelSkin = new SitePanelSkinDto
        {
            Enabled = request.PanelSkin.Enabled,
            BackgroundTexture = panelBackgroundAsset,
            HeaderTexture = panelHeaderAsset,
            BorderTexture = panelBorderAsset,
            BackgroundOpacity = request.PanelSkin.BackgroundOpacity,
            TextureOpacity = request.PanelSkin.TextureOpacity,
            Radius = request.PanelSkin.Radius,
            ShadowStrength = request.PanelSkin.ShadowStrength
        };

        foreach (var slotKey in ThemeSlotKeys.Icons)
        {
            if (!requestedIcons.TryGetValue(slotKey, out var slot) || slot is null)
            {
                continue;
            }

            if (!TryNormalizeThemeAssetReference(slot.Asset, out var asset))
            {
                return Result<SiteAppearanceDto>.Failure($"Theme icon slot '{slotKey}' must use a server-managed same-origin asset.");
            }

            appearance.Icons[slotKey] = new SiteThemeIconSlotDto
            {
                Enabled = slot.Enabled,
                Asset = asset,
                Opacity = slot.Opacity,
                Scale = slot.Scale,
                OffsetX = slot.OffsetX,
                OffsetY = slot.OffsetY
            };
        }

        foreach (var slotKey in ThemeSlotKeys.Decorations)
        {
            if (!requestedDecorations.TryGetValue(slotKey, out var slot) || slot is null)
            {
                continue;
            }

            if (!TryNormalizeThemeAssetReference(slot.Asset, out var asset))
            {
                return Result<SiteAppearanceDto>.Failure($"Theme decoration slot '{slotKey}' must use a server-managed same-origin asset.");
            }

            appearance.Decorations[slotKey] = new SiteThemeDecorationSlotDto
            {
                Enabled = slot.Enabled,
                Asset = asset,
                Opacity = slot.Opacity,
                Scale = slot.Scale,
                OffsetX = slot.OffsetX,
                OffsetY = slot.OffsetY,
                Alignment = slot.Alignment?.ToLowerInvariant(),
                Corner = slot.Corner?.ToLowerInvariant()
            };
        }

        foreach (var pageKey in SupportedPageKeys)
        {
            var source = TryGetPageSource(request.Pages, pageKey, out var page)
                ? page
                : new SitePageBackgroundDto();

            validationError = ValidatePage(pageKey, source);
            if (validationError is not null)
            {
                return Result<SiteAppearanceDto>.Failure(validationError);
            }

            if (!TryNormalizeUploadedImagePath(source.ImageUrl, requestHost, out var imageUrl))
            {
                return Result<SiteAppearanceDto>.Failure("Background image URL must point to uploaded images.");
            }

            appearance.Pages[pageKey] = new SitePageBackgroundDto
            {
                Enabled = source.Enabled,
                ImageUrl = imageUrl,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                Scale = source.Scale,
                OverlayOpacity = source.OverlayOpacity
            };
        }

        var value = JsonSerializer.Serialize(appearance, JsonOptions);
        var setting = await dbContext.SiteSettings
            .FirstOrDefaultAsync(item => item.Key == AppearanceKey, cancellationToken);
        var previousAppearance = ReadAppearanceOrDefault(setting?.Value);

        if (setting is null)
        {
            setting = new SiteSetting
            {
                Id = Guid.NewGuid(),
                Key = AppearanceKey
            };
            dbContext.SiteSettings.Add(setting);
        }

        setting.Value = value;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        setting.UpdatedByUserId = currentUserId;

        auditWriter?.Stage(new SecurityAuditRecord(
            SecurityAuditActions.SiteAppearanceUpdated,
            "SiteAppearance",
            AppearanceKey,
            Metadata: BuildAuditMetadata(previousAppearance, appearance)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SiteAppearanceDto>.Success(appearance);
    }

    private static SiteAppearanceDto ReadAppearance(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("backgroundImageUrl", out _)
            || root.TryGetProperty("backgroundEnabled", out _)
            || root.TryGetProperty("backgroundOverlayOpacity", out _))
        {
            return ReadLegacyAppearance(root);
        }

        var appearance = JsonSerializer.Deserialize<SiteAppearanceDto>(json, JsonOptions)
            ?? CreateDefaultAppearance();

        return NormalizeAppearance(appearance);
    }

    private static SiteAppearanceDto ReadLegacyAppearance(JsonElement root)
    {
        var appearance = CreateDefaultAppearance();

        if (root.TryGetProperty("backgroundEnabled", out var enabled) && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            appearance.Theme.BackgroundEnabled = enabled.GetBoolean();
            appearance.Pages["global"].Enabled = enabled.GetBoolean();
        }

        if (root.TryGetProperty("backgroundOverlayOpacity", out var overlay) && overlay.TryGetDouble(out var overlayValue))
        {
            appearance.Theme.BackgroundOverlayOpacity = overlayValue is >= 0 and <= 1 ? overlayValue : 0.65;
        }

        if (root.TryGetProperty("backgroundImageUrl", out var imageUrl) && imageUrl.ValueKind == JsonValueKind.String)
        {
            var rawUrl = imageUrl.GetString();
            if (TryNormalizeUploadedImagePath(rawUrl, null, out var normalizedUrl))
            {
                appearance.Pages["global"].ImageUrl = normalizedUrl;
            }
        }

        return appearance;
    }

    private static SiteAppearanceDto CreateDefaultAppearance()
    {
        var appearance = new SiteAppearanceDto
        {
            Theme = new SiteAppearanceThemeDto
            {
                BackgroundEnabled = false,
                BackgroundOverlayOpacity = 0.65,
                PanelOpacity = 0.72,
                PanelBlur = 12,
                PanelColor = "#11141A",
                PanelBorderOpacity = 0.14,
                TextPrimaryColor = "#F2F4F8",
                TextSecondaryColor = "#AEB6CA",
                TextMutedColor = "#7F8798",
                AccentColor = "#6E7BFF",
                NavOpacity = 0.58,
                NavBlur = 18,
                NavTextColor = "#D9DEE9",
                NavActiveColor = "#F2F4F8",
                FontPreset = "system"
            },
            Background = new SiteThemeBackgroundDto(),
            PanelSkin = new SitePanelSkinDto()
        };

        foreach (var pageKey in SupportedPageKeys)
        {
            appearance.Pages[pageKey] = new SitePageBackgroundDto
            {
                Enabled = false,
                ImageUrl = null,
                PositionX = 50,
                PositionY = 50,
                Scale = 1,
                OverlayOpacity = null
            };
        }

        return appearance;
    }

    private static SiteAppearanceDto NormalizeAppearance(SiteAppearanceDto appearance)
    {
        var normalized = CreateDefaultAppearance();

        normalized.Theme = new SiteAppearanceThemeDto
        {
            BackgroundEnabled = appearance.Theme.BackgroundEnabled,
            BackgroundOverlayOpacity = appearance.Theme.BackgroundOverlayOpacity is >= 0 and <= 1
                ? appearance.Theme.BackgroundOverlayOpacity
                : 0.65,
            PanelOpacity = appearance.Theme.PanelOpacity is >= 0.35 and <= 0.95
                ? appearance.Theme.PanelOpacity
                : 0.72,
            PanelBlur = appearance.Theme.PanelBlur is >= 0 and <= 30
                ? appearance.Theme.PanelBlur
                : 12,
            PanelColor = IsHexColor(appearance.Theme.PanelColor) ? appearance.Theme.PanelColor.ToUpperInvariant() : "#11141A",
            PanelBorderOpacity = appearance.Theme.PanelBorderOpacity is >= 0 and <= 0.5
                ? appearance.Theme.PanelBorderOpacity
                : 0.14,
            TextPrimaryColor = IsHexColor(appearance.Theme.TextPrimaryColor) ? appearance.Theme.TextPrimaryColor.ToUpperInvariant() : "#F2F4F8",
            TextSecondaryColor = IsHexColor(appearance.Theme.TextSecondaryColor) ? appearance.Theme.TextSecondaryColor.ToUpperInvariant() : "#AEB6CA",
            TextMutedColor = IsHexColor(appearance.Theme.TextMutedColor) ? appearance.Theme.TextMutedColor.ToUpperInvariant() : "#7F8798",
            AccentColor = IsHexColor(appearance.Theme.AccentColor) ? appearance.Theme.AccentColor.ToUpperInvariant() : "#6E7BFF",
            NavOpacity = appearance.Theme.NavOpacity is >= 0.35 and <= 1
                ? appearance.Theme.NavOpacity
                : 0.58,
            NavBlur = appearance.Theme.NavBlur is >= 0 and <= 30
                ? appearance.Theme.NavBlur
                : 18,
            NavTextColor = IsHexColor(appearance.Theme.NavTextColor) ? appearance.Theme.NavTextColor.ToUpperInvariant() : "#D9DEE9",
            NavActiveColor = IsHexColor(appearance.Theme.NavActiveColor) ? appearance.Theme.NavActiveColor.ToUpperInvariant() : "#F2F4F8",
            FontPreset = IsFontPreset(appearance.Theme.FontPreset) ? appearance.Theme.FontPreset : "system"
        };

        var background = appearance.Background ?? new SiteThemeBackgroundDto();
        normalized.Background = new SiteThemeBackgroundDto
        {
            Enabled = background.Enabled,
            Asset = NormalizeThemeAssetReference(background.Asset),
            PositionX = background.PositionX is >= 0 and <= 100 ? background.PositionX : null,
            PositionY = background.PositionY is >= 0 and <= 100 ? background.PositionY : null,
            SizeMode = IsBackgroundSizeMode(background.SizeMode) ? background.SizeMode!.ToLowerInvariant() : null,
            Repeat = IsBackgroundRepeat(background.Repeat) ? background.Repeat!.ToLowerInvariant() : null,
            Attachment = IsBackgroundAttachment(background.Attachment) ? background.Attachment!.ToLowerInvariant() : null,
            OverlayColor = IsHexColor(background.OverlayColor) ? background.OverlayColor!.ToUpperInvariant() : null,
            OverlayOpacity = background.OverlayOpacity is >= 0 and <= 1 ? background.OverlayOpacity : null,
            Blur = background.Blur is >= 0 and <= 20 ? background.Blur : null,
            Brightness = background.Brightness is >= 50 and <= 150 ? background.Brightness : null
        };

        var panelSkin = appearance.PanelSkin ?? new SitePanelSkinDto();
        normalized.PanelSkin = new SitePanelSkinDto
        {
            Enabled = panelSkin.Enabled,
            BackgroundTexture = NormalizeThemeAssetReference(panelSkin.BackgroundTexture),
            HeaderTexture = NormalizeThemeAssetReference(panelSkin.HeaderTexture),
            BorderTexture = NormalizeThemeAssetReference(panelSkin.BorderTexture),
            BackgroundOpacity = panelSkin.BackgroundOpacity is >= 0 and <= 1 ? panelSkin.BackgroundOpacity : null,
            TextureOpacity = panelSkin.TextureOpacity is >= 0 and <= 1 ? panelSkin.TextureOpacity : null,
            Radius = panelSkin.Radius is >= 0 and <= 32 ? panelSkin.Radius : null,
            ShadowStrength = panelSkin.ShadowStrength is >= 0 and <= 1 ? panelSkin.ShadowStrength : null
        };

        var iconSlots = appearance.Icons ?? new Dictionary<string, SiteThemeIconSlotDto?>();
        foreach (var slotKey in ThemeSlotKeys.Icons)
        {
            if (!iconSlots.TryGetValue(slotKey, out var slot) || slot is null)
            {
                continue;
            }

            normalized.Icons[slotKey] = NormalizeIconSlot(slot);
        }

        var decorationSlots = appearance.Decorations ?? new Dictionary<string, SiteThemeDecorationSlotDto?>();
        foreach (var slotKey in ThemeSlotKeys.Decorations)
        {
            if (!decorationSlots.TryGetValue(slotKey, out var slot) || slot is null)
            {
                continue;
            }

            normalized.Decorations[slotKey] = NormalizeDecorationSlot(slot);
        }

        foreach (var pageKey in SupportedPageKeys)
        {
            if (!TryGetPageSource(appearance.Pages, pageKey, out var source))
            {
                continue;
            }

            normalized.Pages[pageKey] = new SitePageBackgroundDto
            {
                Enabled = source.Enabled,
                ImageUrl = TryNormalizeUploadedImagePath(source.ImageUrl, null, out var normalizedUrl) ? normalizedUrl : null,
                PositionX = source.PositionX is >= 0 and <= 100 ? source.PositionX : 50,
                PositionY = source.PositionY is >= 0 and <= 100 ? source.PositionY : 50,
                Scale = source.Scale is >= 0.5 and <= 2.5 ? source.Scale : 1,
                OverlayOpacity = source.OverlayOpacity is >= 0 and <= 1 ? source.OverlayOpacity : null
            };
        }

        return normalized;
    }

    private static bool TryGetPageSource(IReadOnlyDictionary<string, SitePageBackgroundDto> pages, string pageKey, out SitePageBackgroundDto source)
    {
        if (pages.TryGetValue(pageKey, out var exact))
        {
            source = exact;
            return true;
        }

        if (!LegacyPageAliases.TryGetValue(pageKey, out var aliases))
        {
            source = null!;
            return false;
        }

        foreach (var alias in aliases)
        {
            if (pages.TryGetValue(alias, out var legacy))
            {
                source = legacy;
                return true;
            }
        }

        source = null!;
        return false;
    }

    private static string? ValidateTheme(SiteAppearanceThemeDto theme)
    {
        if (theme.BackgroundOverlayOpacity is < 0 or > 1)
        {
            return "Background overlay opacity must be between 0 and 1.";
        }

        if (theme.PanelOpacity is < 0.35 or > 0.95)
        {
            return "Panel opacity must be between 0.35 and 0.95.";
        }

        if (theme.PanelBlur is < 0 or > 30)
        {
            return "Panel blur must be between 0 and 30.";
        }

        if (!IsHexColor(theme.PanelColor)
            || !IsHexColor(theme.TextPrimaryColor)
            || !IsHexColor(theme.TextSecondaryColor)
            || !IsHexColor(theme.TextMutedColor)
            || !IsHexColor(theme.AccentColor)
            || !IsHexColor(theme.NavTextColor)
            || !IsHexColor(theme.NavActiveColor))
        {
            return "Theme colors must use #RRGGBB format.";
        }

        if (theme.PanelBorderOpacity is < 0 or > 0.5)
        {
            return "Panel border opacity must be between 0 and 0.5.";
        }

        if (theme.NavOpacity is < 0.35 or > 1)
        {
            return "Navigation opacity must be between 0.35 and 1.";
        }

        if (theme.NavBlur is < 0 or > 30)
        {
            return "Navigation blur must be between 0 and 30.";
        }

        if (!IsFontPreset(theme.FontPreset))
        {
            return "Unsupported font preset.";
        }

        return null;
    }

    private static string? ValidatePage(string pageKey, SitePageBackgroundDto page)
    {
        if (page.PositionX is < 0 or > 100)
        {
            return $"Background positionX for {pageKey} must be between 0 and 100.";
        }

        if (page.PositionY is < 0 or > 100)
        {
            return $"Background positionY for {pageKey} must be between 0 and 100.";
        }

        if (page.Scale is < 0.5 or > 2.5)
        {
            return $"Background scale for {pageKey} must be between 0.5 and 2.5.";
        }

        if (page.OverlayOpacity is < 0 or > 1)
        {
            return $"Background overlay opacity for {pageKey} must be between 0 and 1.";
        }

        return null;
    }

    private static string? ValidateBackground(SiteThemeBackgroundDto background)
    {
        if (background.Enabled && background.Asset is null)
        {
            return "An enabled theme background requires an uploaded asset.";
        }

        if (background.PositionX is < 0 or > 100 || background.PositionY is < 0 or > 100)
        {
            return "Theme background position must be between 0 and 100.";
        }

        if (background.SizeMode is not null && !IsBackgroundSizeMode(background.SizeMode))
        {
            return "Unsupported theme background size mode.";
        }

        if (background.Repeat is not null && !IsBackgroundRepeat(background.Repeat))
        {
            return "Unsupported theme background repeat mode.";
        }

        if (background.Attachment is not null && !IsBackgroundAttachment(background.Attachment))
        {
            return "Unsupported theme background attachment mode.";
        }

        if (background.OverlayColor is not null && !IsHexColor(background.OverlayColor))
        {
            return "Theme background overlay color must use #RRGGBB format.";
        }

        if (background.OverlayOpacity is < 0 or > 1)
        {
            return "Theme background overlay opacity must be between 0 and 1.";
        }

        if (background.Blur is < 0 or > 20)
        {
            return "Theme background blur must be between 0 and 20.";
        }

        if (background.Brightness is < 50 or > 150)
        {
            return "Theme background brightness must be between 50 and 150.";
        }

        return null;
    }

    private static string? ValidatePanelSkin(SitePanelSkinDto panelSkin)
    {
        if (panelSkin.BackgroundOpacity is < 0 or > 1 || panelSkin.TextureOpacity is < 0 or > 1)
        {
            return "Panel skin opacity must be between 0 and 1.";
        }

        if (panelSkin.Radius is < 0 or > 32)
        {
            return "Panel skin radius must be between 0 and 32.";
        }

        if (panelSkin.ShadowStrength is < 0 or > 1)
        {
            return "Panel skin shadow strength must be between 0 and 1.";
        }

        return null;
    }

    private static string? ValidateIconSlots(IReadOnlyDictionary<string, SiteThemeIconSlotDto?> slots)
    {
        var unknownSlot = slots.Keys.FirstOrDefault(key => !ThemeSlotKeys.Icons.Contains(key, StringComparer.Ordinal));
        if (unknownSlot is not null)
        {
            return $"Unknown theme icon slot '{unknownSlot}'.";
        }

        foreach (var (slotKey, slot) in slots)
        {
            var validationError = ValidateSlotValues(slotKey, slot);
            if (validationError is not null)
            {
                return validationError;
            }
        }

        return null;
    }

    private static string? ValidateDecorationSlots(IReadOnlyDictionary<string, SiteThemeDecorationSlotDto?> slots)
    {
        var unknownSlot = slots.Keys.FirstOrDefault(key => !ThemeSlotKeys.Decorations.Contains(key, StringComparer.Ordinal));
        if (unknownSlot is not null)
        {
            return $"Unknown theme decoration slot '{unknownSlot}'.";
        }

        foreach (var (slotKey, slot) in slots)
        {
            var validationError = ValidateSlotValues(slotKey, slot);
            if (validationError is not null)
            {
                return validationError;
            }

            if (slot?.Alignment is not null && !IsDecorationAlignment(slot.Alignment))
            {
                return $"Unsupported alignment for theme decoration slot '{slotKey}'.";
            }

            if (slot?.Corner is not null && !IsDecorationCorner(slot.Corner))
            {
                return $"Unsupported corner for theme decoration slot '{slotKey}'.";
            }
        }

        return null;
    }

    private static string? ValidateSlotValues(string slotKey, SiteThemeIconSlotDto? slot)
    {
        if (slot is null)
        {
            return null;
        }

        if (slot.Enabled && slot.Asset is null)
        {
            return $"Enabled theme slot '{slotKey}' requires an uploaded asset.";
        }

        if (slot.Opacity is < 0 or > 1)
        {
            return $"Opacity for theme slot '{slotKey}' must be between 0 and 1.";
        }

        if (slot.Scale is < 0.5 or > 2)
        {
            return $"Scale for theme slot '{slotKey}' must be between 0.5 and 2.";
        }

        if (slot.OffsetX is < -64 or > 64 || slot.OffsetY is < -64 or > 64)
        {
            return $"Offset for theme slot '{slotKey}' must be between -64 and 64.";
        }

        return null;
    }

    private static bool IsHexColor(string? color)
    {
        return color is { Length: 7 }
            && color[0] == '#'
            && color.Skip(1).All(Uri.IsHexDigit);
    }

    private static bool IsFontPreset(string? preset)
    {
        return preset is "system" or "readable" or "mono";
    }

    private static bool IsBackgroundSizeMode(string? value) => value?.ToLowerInvariant() is "cover" or "contain" or "auto";

    private static bool IsBackgroundRepeat(string? value) => value?.ToLowerInvariant() is "no-repeat" or "repeat" or "repeat-x" or "repeat-y";

    private static bool IsBackgroundAttachment(string? value) => value?.ToLowerInvariant() is "scroll" or "fixed";

    private static bool IsDecorationAlignment(string? value) => value?.ToLowerInvariant() is "start" or "center" or "end";

    private static bool IsDecorationCorner(string? value) => value?.ToLowerInvariant() is "top-left" or "top-right" or "bottom-left" or "bottom-right";

    private static SiteThemeIconSlotDto NormalizeIconSlot(SiteThemeIconSlotDto slot) => new()
    {
        Enabled = slot.Enabled,
        Asset = NormalizeThemeAssetReference(slot.Asset),
        Opacity = slot.Opacity is >= 0 and <= 1 ? slot.Opacity : null,
        Scale = slot.Scale is >= 0.5 and <= 2 ? slot.Scale : null,
        OffsetX = slot.OffsetX is >= -64 and <= 64 ? slot.OffsetX : null,
        OffsetY = slot.OffsetY is >= -64 and <= 64 ? slot.OffsetY : null
    };

    private static SiteThemeDecorationSlotDto NormalizeDecorationSlot(SiteThemeDecorationSlotDto slot) => new()
    {
        Enabled = slot.Enabled,
        Asset = NormalizeThemeAssetReference(slot.Asset),
        Opacity = slot.Opacity is >= 0 and <= 1 ? slot.Opacity : null,
        Scale = slot.Scale is >= 0.5 and <= 2 ? slot.Scale : null,
        OffsetX = slot.OffsetX is >= -64 and <= 64 ? slot.OffsetX : null,
        OffsetY = slot.OffsetY is >= -64 and <= 64 ? slot.OffsetY : null,
        Alignment = IsDecorationAlignment(slot.Alignment) ? slot.Alignment!.ToLowerInvariant() : null,
        Corner = IsDecorationCorner(slot.Corner) ? slot.Corner!.ToLowerInvariant() : null
    };

    private bool TryNormalizeThemeAssetReference(ThemeAssetReferenceDto? asset, out ThemeAssetReferenceDto? normalized)
    {
        normalized = NormalizeThemeAssetReference(asset);
        if (asset is null)
        {
            return true;
        }

        if (normalized is null)
        {
            return false;
        }

        if (storagePaths is null)
        {
            return true;
        }

        try
        {
            return File.Exists(storagePaths.ResolveThemeAssetPath(normalized.AssetId));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static ThemeAssetReferenceDto? NormalizeThemeAssetReference(ThemeAssetReferenceDto? asset)
    {
        if (asset is null || !IsManagedThemeAssetId(asset.AssetId))
        {
            return null;
        }

        var expectedUrl = ThemeAssetPrefix + asset.AssetId;
        return string.Equals(asset.Url, expectedUrl, StringComparison.Ordinal)
            ? new ThemeAssetReferenceDto { AssetId = asset.AssetId, Url = expectedUrl }
            : null;
    }

    private static bool IsManagedThemeAssetId(string? assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return false;
        }

        var extension = Path.GetExtension(assetId);
        return assetId.Length is >= 36 and <= 37
            && Guid.TryParseExact(Path.GetFileNameWithoutExtension(assetId), "N", out _)
            && extension is ".png" or ".jpg" or ".webp";
    }

    private static SiteAppearanceDto ReadAppearanceOrDefault(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return CreateDefaultAppearance();
        }

        try
        {
            return ReadAppearance(json);
        }
        catch (JsonException)
        {
            return CreateDefaultAppearance();
        }
    }

    private static IReadOnlyDictionary<string, string?> BuildAuditMetadata(SiteAppearanceDto previous, SiteAppearanceDto current)
    {
        var changedSlots = new List<string>();
        AddChangedSlot(changedSlots, "background", previous.Background.Asset, current.Background.Asset);
        AddChangedSlot(changedSlots, "panelBackground", previous.PanelSkin.BackgroundTexture, current.PanelSkin.BackgroundTexture);
        AddChangedSlot(changedSlots, "panelHeader", previous.PanelSkin.HeaderTexture, current.PanelSkin.HeaderTexture);
        AddChangedSlot(changedSlots, "panelBorder", previous.PanelSkin.BorderTexture, current.PanelSkin.BorderTexture);

        var changedIconSlots = ThemeSlotKeys.Icons
            .Where(slot => !SlotEquals(previous.Icons.GetValueOrDefault(slot), current.Icons.GetValueOrDefault(slot)))
            .ToArray();
        var changedDecorationSlots = ThemeSlotKeys.Decorations
            .Where(slot => !SlotEquals(previous.Decorations.GetValueOrDefault(slot), current.Decorations.GetValueOrDefault(slot)))
            .ToArray();

        return new Dictionary<string, string?>
        {
            ["backgroundEnabledChanged"] = (previous.Background.Enabled != current.Background.Enabled).ToString(),
            ["panelSkinEnabledChanged"] = (previous.PanelSkin.Enabled != current.PanelSkin.Enabled).ToString(),
            ["changedAssetSlots"] = changedSlots.Count == 0 ? "none" : string.Join(',', changedSlots),
            ["changedIconSlots"] = changedIconSlots.Length == 0 ? "none" : string.Join(',', changedIconSlots),
            ["changedDecorationSlots"] = changedDecorationSlots.Length == 0 ? "none" : string.Join(',', changedDecorationSlots)
        };
    }

    private static bool SlotEquals(SiteThemeIconSlotDto? previous, SiteThemeIconSlotDto? current)
    {
        if (previous is null || current is null)
        {
            return previous is null && current is null;
        }

        return previous.Enabled == current.Enabled
            && string.Equals(previous.Asset?.AssetId, current.Asset?.AssetId, StringComparison.Ordinal)
            && previous.Opacity == current.Opacity
            && previous.Scale == current.Scale
            && previous.OffsetX == current.OffsetX
            && previous.OffsetY == current.OffsetY
            && (previous is not SiteThemeDecorationSlotDto previousDecoration
                || current is not SiteThemeDecorationSlotDto currentDecoration
                || (string.Equals(previousDecoration.Alignment, currentDecoration.Alignment, StringComparison.Ordinal)
                    && string.Equals(previousDecoration.Corner, currentDecoration.Corner, StringComparison.Ordinal)));
    }

    private static void AddChangedSlot(ICollection<string> slots, string slot, ThemeAssetReferenceDto? previous, ThemeAssetReferenceDto? current)
    {
        if (!string.Equals(previous?.AssetId, current?.AssetId, StringComparison.Ordinal))
        {
            slots.Add(slot);
        }
    }

    private static bool TryNormalizeUploadedImagePath(string? url, string? requestHost, out string? normalizedPath)
    {
        normalizedPath = null;
        var trimmed = url?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        if (IsSafeUploadPath(trimmed))
        {
            normalizedPath = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !IsAllowedHost(uri.Host, requestHost)
            || !IsSafeUploadPath(uri.AbsolutePath))
        {
            return false;
        }

        normalizedPath = uri.AbsolutePath;
        return true;
    }

    private static bool IsAllowedHost(string host, string? requestHost)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(requestHost) && host.Equals(requestHost, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSafeUploadPath(string path)
    {
        return path.StartsWith(UploadImagePrefix, StringComparison.OrdinalIgnoreCase)
            && !path.Contains("..", StringComparison.Ordinal)
            && !path.Contains('\\', StringComparison.Ordinal);
    }
}
