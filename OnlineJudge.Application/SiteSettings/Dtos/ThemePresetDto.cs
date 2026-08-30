namespace OnlineJudge.Application.SiteSettings.Dtos;

public sealed class ThemePresetDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SchemaVersion { get; set; } = 1;

    public SiteAppearanceDto Appearance { get; set; } = new();

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsBuiltIn { get; set; }

    public int AssetCount { get; set; }
}

public sealed class ThemePresetListDto
{
    public IReadOnlyList<ThemePresetDto> Items { get; set; } = [];

    public Guid? LastAppliedPresetId { get; set; }
}

public sealed class ThemePackExportDto : IDisposable
{
    public Stream Content { get; init; } = Stream.Null;

    public string FileName { get; init; } = "theme.zip";

    public void Dispose() => Content.Dispose();
}
