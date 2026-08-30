using OnlineJudge.Application.SiteSettings.Dtos;

namespace OnlineJudge.Application.SiteSettings.Requests;

public class UpdateSiteAppearanceRequest
{
    public SiteAppearanceThemeDto Theme { get; set; } = new();

    public Dictionary<string, SitePageBackgroundDto> Pages { get; set; } = new();

    public SiteThemeBackgroundDto Background { get; set; } = new();

    public SitePanelSkinDto PanelSkin { get; set; } = new();
}
