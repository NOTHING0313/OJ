using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.SiteSettings;

public class SiteSettingsService(OnlineJudgeDbContext dbContext) : ISiteSettingsService
{
    private const string AppearanceKey = "appearance";
    private const string UploadImagePrefix = "/uploads/images/";

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

        var appearance = CreateDefaultAppearance();
        appearance.Theme = new SiteAppearanceThemeDto
        {
            BackgroundEnabled = request.Theme.BackgroundEnabled,
            BackgroundOverlayOpacity = request.Theme.BackgroundOverlayOpacity,
            PanelOpacity = request.Theme.PanelOpacity,
            PanelBlur = request.Theme.PanelBlur
        };

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
                Scale = source.Scale
            };
        }

        var value = JsonSerializer.Serialize(appearance, JsonOptions);
        var setting = await dbContext.SiteSettings
            .FirstOrDefaultAsync(item => item.Key == AppearanceKey, cancellationToken);

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
                PanelBlur = 12
            }
        };

        foreach (var pageKey in SupportedPageKeys)
        {
            appearance.Pages[pageKey] = new SitePageBackgroundDto
            {
                Enabled = false,
                ImageUrl = null,
                PositionX = 50,
                PositionY = 50,
                Scale = 1
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
                : 12
        };

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
                Scale = source.Scale is >= 0.5 and <= 2.5 ? source.Scale : 1
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

        return null;
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
