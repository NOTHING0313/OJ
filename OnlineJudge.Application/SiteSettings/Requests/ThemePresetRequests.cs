using OnlineJudge.Application.SiteSettings.Dtos;

namespace OnlineJudge.Application.SiteSettings.Requests;

public class CreateThemePresetRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public SiteAppearanceDto Appearance { get; set; } = new();
}

public sealed class UpdateThemePresetRequest : CreateThemePresetRequest;

public sealed class RenameThemePresetRequest
{
    public string Name { get; set; } = string.Empty;
}
