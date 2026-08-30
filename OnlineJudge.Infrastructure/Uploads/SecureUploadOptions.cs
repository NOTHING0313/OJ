using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Uploads;

public sealed class SecureUploadOptions
{
    public const string SectionName = "SecureUpload";

    public long ImageMaxBytes { get; init; } = 5L * 1024 * 1024;

    public long ChallengeArchiveMaxBytes { get; init; } = 50L * 1024 * 1024;

    public long JudgeSourceMaxBytes { get; init; } = 512L * 1024;

    public int ArchiveMaxEntryCount { get; init; } = 1000;

    public long ArchiveMaxExpandedBytes { get; init; } = 256L * 1024 * 1024;

    public long ArchiveMaxSingleEntryBytes { get; init; } = 64L * 1024 * 1024;

    public double ArchiveMaxCompressionRatio { get; init; } = 100;

    public static SecureUploadOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        return new SecureUploadOptions
        {
            ImageMaxBytes = ReadPositiveLong(section, nameof(ImageMaxBytes), 5L * 1024 * 1024),
            ChallengeArchiveMaxBytes = ReadPositiveLong(section, nameof(ChallengeArchiveMaxBytes), 50L * 1024 * 1024),
            JudgeSourceMaxBytes = ReadPositiveLong(section, nameof(JudgeSourceMaxBytes), 512L * 1024),
            ArchiveMaxEntryCount = ReadPositiveInt(section, nameof(ArchiveMaxEntryCount), 1000),
            ArchiveMaxExpandedBytes = ReadPositiveLong(section, nameof(ArchiveMaxExpandedBytes), 256L * 1024 * 1024),
            ArchiveMaxSingleEntryBytes = ReadPositiveLong(section, nameof(ArchiveMaxSingleEntryBytes), 64L * 1024 * 1024),
            ArchiveMaxCompressionRatio = ReadPositiveDouble(section, nameof(ArchiveMaxCompressionRatio), 100)
        };
    }

    private static long ReadPositiveLong(IConfiguration section, string key, long fallback) =>
        long.TryParse(section[key], out var value) && value > 0 ? value : fallback;

    private static int ReadPositiveInt(IConfiguration section, string key, int fallback) =>
        int.TryParse(section[key], out var value) && value > 0 ? value : fallback;

    private static double ReadPositiveDouble(IConfiguration section, string key, double fallback) =>
        double.TryParse(section[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value > 0
                ? value
                : fallback;
}
