namespace OnlineJudge.Application.SiteSettings;

public static class ThemePackContract
{
    public const string Format = "onlinejudge-theme";
    public const int Version = 1;
    public const int PresetSchemaVersion = 1;
    public const int MaxPresets = 30;
    public const int MaxAssets = 50;
    public const long MaxPackBytes = 50L * 1024 * 1024;
    public const long MaxExpandedBytes = 256L * 1024 * 1024;
}
