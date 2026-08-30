namespace OnlineJudge.Application.SiteSettings.Dtos;

public class SiteAppearanceDto
{
    public SiteAppearanceThemeDto Theme { get; set; } = new();

    public Dictionary<string, SitePageBackgroundDto> Pages { get; set; } = new();

    public SiteThemeBackgroundDto Background { get; set; } = new();

    public SitePanelSkinDto PanelSkin { get; set; } = new();
}

public class SiteThemeBackgroundDto
{
    public bool Enabled { get; set; }

    public ThemeAssetReferenceDto? Asset { get; set; }

    public double? PositionX { get; set; }

    public double? PositionY { get; set; }

    public string? SizeMode { get; set; }

    public string? Repeat { get; set; }

    public string? Attachment { get; set; }

    public string? OverlayColor { get; set; }

    public double? OverlayOpacity { get; set; }

    public double? Blur { get; set; }

    public double? Brightness { get; set; }
}

public class SitePanelSkinDto
{
    public bool Enabled { get; set; }

    public ThemeAssetReferenceDto? BackgroundTexture { get; set; }

    public ThemeAssetReferenceDto? HeaderTexture { get; set; }

    public ThemeAssetReferenceDto? BorderTexture { get; set; }

    public double? BackgroundOpacity { get; set; }

    public double? TextureOpacity { get; set; }

    public double? Radius { get; set; }

    public double? ShadowStrength { get; set; }
}

public class ThemeAssetReferenceDto
{
    public string AssetId { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;
}

public class ThemeAssetDto : ThemeAssetReferenceDto
{
    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }
}

public class SiteAppearanceThemeDto
{
    public bool BackgroundEnabled { get; set; }

    public double BackgroundOverlayOpacity { get; set; } = 0.65;

    public double PanelOpacity { get; set; } = 0.72;

    public int PanelBlur { get; set; } = 12;

    public string PanelColor { get; set; } = "#11141A";

    public double PanelBorderOpacity { get; set; } = 0.14;

    public string TextPrimaryColor { get; set; } = "#F2F4F8";

    public string TextSecondaryColor { get; set; } = "#AEB6CA";

    public string TextMutedColor { get; set; } = "#7F8798";

    public string AccentColor { get; set; } = "#6E7BFF";

    public double NavOpacity { get; set; } = 0.58;

    public int NavBlur { get; set; } = 18;

    public string NavTextColor { get; set; } = "#D9DEE9";

    public string NavActiveColor { get; set; } = "#F2F4F8";

    public string FontPreset { get; set; } = "system";
}

public class SitePageBackgroundDto
{
    public bool Enabled { get; set; }

    public string? ImageUrl { get; set; }

    public double PositionX { get; set; } = 50;

    public double PositionY { get; set; } = 50;

    public double Scale { get; set; } = 1;

    public double? OverlayOpacity { get; set; }
}
