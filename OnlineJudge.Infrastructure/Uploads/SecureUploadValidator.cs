using System.IO.Compression;
using OnlineJudge.Application.Uploads;

namespace OnlineJudge.Infrastructure.Uploads;

public sealed class SecureUploadValidator(SecureUploadOptions options) : ISecureUploadValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];
    private static readonly IReadOnlyDictionary<string, string> ImageMimeByExtension =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };
    private static readonly HashSet<string> ArchiveContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/zip",
        "application/x-zip-compressed",
        "application/octet-stream"
    };
    private static readonly HashSet<string> JudgeSourceContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "application/octet-stream",
        "text/plain",
        "text/x-c",
        "text/x-csrc",
        "text/x-c++",
        "text/x-c++src",
        "text/x-csharp"
    };

    public async Task<SecureUploadValidationResult> ValidateAsync(SecureUploadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fileNameResult = ValidateFileName(request.OriginalFileName);
        if (fileNameResult is not null)
        {
            return fileNameResult;
        }

        if (request.Content == Stream.Null || !request.Content.CanRead || !request.Content.CanSeek)
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "The uploaded file could not be safely inspected.");
        }

        var startPosition = request.Content.Position;
        var actualLength = request.Content.Length - startPosition;
        if (request.DeclaredLength <= 0 || actualLength != request.DeclaredLength)
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "The uploaded file length is invalid.");
        }

        try
        {
            return request.Policy switch
            {
                UploadPolicy.Image or UploadPolicy.ThemeImage => await ValidateImageAsync(request, cancellationToken),
                UploadPolicy.ChallengeArchive => ValidateArchive(request),
                UploadPolicy.JudgeSource => ValidateJudgeSource(request),
                _ => Failure(SecureUploadErrorCodes.InvalidType, "The upload policy is not supported.")
            };
        }
        finally
        {
            request.Content.Position = startPosition;
        }
    }

    private async Task<SecureUploadValidationResult> ValidateImageAsync(SecureUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.DeclaredLength > options.ImageMaxBytes)
        {
            return Failure(SecureUploadErrorCodes.TooLarge, "The image exceeds the configured size limit.");
        }

        var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        if (!ImageMimeByExtension.TryGetValue(extension, out var expectedMime))
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "Only PNG, JPEG, and WebP images are allowed.");
        }

        if (!string.Equals(request.DeclaredContentType, expectedMime, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(SecureUploadErrorCodes.TypeMismatch, "The image extension and declared content type do not match.");
        }

        var header = new byte[12];
        var read = await request.Content.ReadAsync(header, cancellationToken);
        var detected = DetectImageType(header.AsSpan(0, read));
        if (detected is null)
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "The image signature is not supported.");
        }

        if (!string.Equals(detected.Value.Mime, expectedMime, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(SecureUploadErrorCodes.TypeMismatch, "The image signature does not match its extension and content type.");
        }

        if (detected.Value.Extension == ".jpg")
        {
            request.Content.Position = request.Content.Length - 2;
            var trailer = new byte[2];
            if (await request.Content.ReadAsync(trailer, cancellationToken) != 2 || trailer[0] != 0xff || trailer[1] != 0xd9)
            {
                return Failure(SecureUploadErrorCodes.InvalidType, "The JPEG file is incomplete.");
            }
        }

        return SecureUploadValidationResult.Success(detected.Value.Extension);
    }

    private SecureUploadValidationResult ValidateArchive(SecureUploadRequest request)
    {
        if (request.DeclaredLength > options.ChallengeArchiveMaxBytes)
        {
            return Failure(SecureUploadErrorCodes.ArchiveTooLarge, "The archive exceeds the configured upload size limit.");
        }

        if (!string.Equals(Path.GetExtension(request.OriginalFileName), ".zip", StringComparison.OrdinalIgnoreCase)
            || !ArchiveContentTypes.Contains(request.DeclaredContentType))
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "Only ZIP archives are allowed.");
        }

        try
        {
            using var archive = new ZipArchive(request.Content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > options.ArchiveMaxEntryCount)
            {
                return Failure(SecureUploadErrorCodes.ArchiveTooComplex, "The archive contains too many entries.");
            }

            long expandedTotal = 0;
            foreach (var entry in archive.Entries)
            {
                if (!IsSafeArchiveEntryName(entry.FullName) || IsSymbolicLink(entry))
                {
                    return Failure(SecureUploadErrorCodes.ArchiveUnsafe, "The archive contains an unsafe entry.");
                }

                if (entry.Length > options.ArchiveMaxSingleEntryBytes)
                {
                    return Failure(SecureUploadErrorCodes.ArchiveTooLarge, "An archive entry exceeds the configured size limit.");
                }

                if (expandedTotal > options.ArchiveMaxExpandedBytes - entry.Length)
                {
                    return Failure(SecureUploadErrorCodes.ArchiveTooLarge, "The archive expanded size exceeds the configured limit.");
                }

                expandedTotal += entry.Length;
                if (entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length / (double)entry.CompressedLength > options.ArchiveMaxCompressionRatio))
                {
                    return Failure(SecureUploadErrorCodes.ArchiveTooLarge, "The archive compression ratio exceeds the configured limit.");
                }
            }

            return SecureUploadValidationResult.Success(".zip");
        }
        catch (InvalidDataException)
        {
            return Failure(SecureUploadErrorCodes.ArchiveUnsafe, "The ZIP archive is invalid or unsafe.");
        }
    }

    private SecureUploadValidationResult ValidateJudgeSource(SecureUploadRequest request)
    {
        if (request.DeclaredLength > options.JudgeSourceMaxBytes)
        {
            return Failure(SecureUploadErrorCodes.TooLarge, "The judge source file exceeds the configured size limit.");
        }

        var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        if (request.AllowedExtensions is null
            || !request.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || !JudgeSourceContentTypes.Contains(request.DeclaredContentType))
        {
            return Failure(SecureUploadErrorCodes.InvalidType, "The judge source file type is not supported.");
        }

        return SecureUploadValidationResult.Success(extension);
    }

    private static SecureUploadValidationResult? ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255 || fileName.Any(char.IsControl))
        {
            return Failure(SecureUploadErrorCodes.InvalidFileName, "The uploaded file name is invalid.");
        }

        string decoded;
        try
        {
            decoded = fileName;
            for (var iteration = 0; iteration < 2; iteration++)
            {
                var next = Uri.UnescapeDataString(decoded);
                if (string.Equals(next, decoded, StringComparison.Ordinal)) break;
                decoded = next;
            }
        }
        catch (UriFormatException)
        {
            return Failure(SecureUploadErrorCodes.InvalidFileName, "The uploaded file name is invalid.");
        }

        var segments = decoded.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(decoded)
            || decoded.Contains('/')
            || decoded.Contains('\\')
            || decoded.StartsWith("//", StringComparison.Ordinal)
            || decoded.Length >= 2 && char.IsLetter(decoded[0]) && decoded[1] == ':'
            || segments.Any(segment => segment == "..")
            || !string.Equals(Path.GetFileName(decoded), decoded, StringComparison.Ordinal))
        {
            return Failure(SecureUploadErrorCodes.InvalidFileName, "The uploaded file name is invalid.");
        }

        return null;
    }

    private static (string Extension, string Mime)? DetectImageType(ReadOnlySpan<byte> header)
    {
        if (header.Length >= PngSignature.Length && header[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return (".png", "image/png");
        }

        if (header.Length >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
        {
            return (".jpg", "image/jpeg");
        }

        if (header.Length >= 12
            && header[..4].SequenceEqual("RIFF"u8)
            && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    private static bool IsSafeArchiveEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Any(char.IsControl)) return false;

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(Uri.UnescapeDataString(entryName)).Replace('\\', '/');
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (decoded.StartsWith('/') || decoded.StartsWith("//", StringComparison.Ordinal)
            || decoded.Length >= 2 && char.IsLetter(decoded[0]) && decoded[1] == ':')
        {
            return false;
        }

        return decoded.Split('/', StringSplitOptions.RemoveEmptyEntries).All(segment => segment != "..");
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000;

    private static SecureUploadValidationResult Failure(string code, string message) =>
        SecureUploadValidationResult.Failure(code, message);
}
