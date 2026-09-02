namespace OnlineJudge.Application.SiteSettings.Dtos;

public sealed class ThemePackPreflightDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Format { get; set; } = string.Empty;

    public int Version { get; set; }

    public int SchemaVersion { get; set; }

    public int AssetCount { get; set; }

    public long TotalAssetBytes { get; set; }

    public bool HasBackground { get; set; }

    public int PanelAssetCount { get; set; }

    public int IconOverrideCount { get; set; }

    public int DecorationCount { get; set; }

    public bool HasNameCollision { get; set; }

    public string ResolvedName { get; set; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; set; } = [];
}
