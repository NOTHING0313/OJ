namespace OnlineJudge.Application.SiteSettings.Dtos;

public class SiteAppearanceDto
{
    public SiteAppearanceThemeDto Theme { get; set; } = new();

    public Dictionary<string, SitePageBackgroundDto> Pages { get; set; } = new();
}

public class SiteAppearanceThemeDto
{
    public bool BackgroundEnabled { get; set; }

    public double BackgroundOverlayOpacity { get; set; } = 0.65;

    public double PanelOpacity { get; set; } = 0.72;

    public int PanelBlur { get; set; } = 12;
}

public class SitePageBackgroundDto
{
    public bool Enabled { get; set; }

    public string? ImageUrl { get; set; }

    public double PositionX { get; set; } = 50;

    public double PositionY { get; set; } = 50;

    public double Scale { get; set; } = 1;
}
