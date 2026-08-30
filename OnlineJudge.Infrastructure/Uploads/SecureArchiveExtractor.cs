using System.IO.Compression;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.SiteSettings;
using OnlineJudge.Application.Uploads;

namespace OnlineJudge.Infrastructure.Uploads;

public sealed class SecureArchiveExtractor(SecureUploadOptions options) : ISecureArchiveExtractor
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };

    public async Task<Result<IReadOnlyDictionary<string, byte[]>>> ExtractThemePackAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (!content.CanRead || !content.CanSeek || content.Length - content.Position > ThemePackContract.MaxPackBytes)
        {
            return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack exceeds the 50 MiB limit or cannot be safely inspected.");
        }

        try
        {
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count is < 1 or > ThemePackContract.MaxAssets + 1)
            {
                return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack contains an invalid number of entries.");
            }

            var extracted = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            long expandedTotal = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryGetSafeEntryName(entry, out var entryName) || !IsAllowedEntry(entryName))
                {
                    return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack contains an unsafe or unsupported entry.");
                }

                if (!extracted.TryAdd(entryName, []))
                {
                    return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack contains duplicate entries.");
                }

                var maxEntryBytes = entryName == "manifest.json" ? 2L * 1024 * 1024 : options.ImageMaxBytes;
                if (entry.Length > maxEntryBytes
                    || expandedTotal > ThemePackContract.MaxExpandedBytes - entry.Length
                    || entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length / (double)entry.CompressedLength > options.ArchiveMaxCompressionRatio))
                {
                    return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack expanded size or compression ratio exceeds the safety limit.");
                }

                await using var input = entry.Open();
                using var output = new MemoryStream((int)entry.Length);
                await input.CopyToAsync(output, cancellationToken);
                if (output.Length != entry.Length)
                {
                    return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack entry length is invalid.");
                }

                extracted[entryName] = output.ToArray();
                expandedTotal += entry.Length;
            }

            if (!extracted.ContainsKey("manifest.json"))
            {
                return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack manifest.json is required.");
            }

            return Result<IReadOnlyDictionary<string, byte[]>>.Success(extracted);
        }
        catch (InvalidDataException)
        {
            return Result<IReadOnlyDictionary<string, byte[]>>.Failure("Theme pack ZIP is invalid or unsafe.");
        }
    }

    private static bool TryGetSafeEntryName(ZipArchiveEntry entry, out string entryName)
    {
        entryName = string.Empty;
        if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.Any(char.IsControl) || entry.FullName.Contains('\\') || IsSymbolicLink(entry)) return false;

        try
        {
            entryName = Uri.UnescapeDataString(Uri.UnescapeDataString(entry.FullName)).Replace('\\', '/');
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (entryName.StartsWith('/') || entryName.StartsWith("//", StringComparison.Ordinal)
            || entryName.Length >= 2 && char.IsLetter(entryName[0]) && entryName[1] == ':') return false;

        var segments = entryName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 && segments.All(segment => segment is not "." and not "..");
    }

    private static bool IsAllowedEntry(string entryName)
    {
        if (entryName == "manifest.json") return true;
        var segments = entryName.Split('/');
        return segments.Length == 2
            && segments[0] == "assets"
            && string.Equals(Path.GetFileName(segments[1]), segments[1], StringComparison.Ordinal)
            && AllowedExtensions.Contains(Path.GetExtension(segments[1]));
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000;
}
