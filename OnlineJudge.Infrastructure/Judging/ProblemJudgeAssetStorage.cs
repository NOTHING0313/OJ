using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Judging;

public class ProblemJudgeAssetStorage : IProblemJudgeAssetStorage
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private readonly string storageRoot;

    public ProblemJudgeAssetStorage(IConfiguration configuration)
    {
        var configuredRoot = configuration["JudgeAssets:StorageRoot"];
        storageRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine("App_Data", "judge-assets")
            : configuredRoot);
    }

    public async Task<StoredJudgeAssetFile> WriteAsync(Guid problemId, JudgeLanguage language, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        _ = StrictUtf8.GetString(content.Span);

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = $"{problemId:N}/{GetLanguageDirectory(language)}/{storedFileName}";
        var fullPath = ResolvePath(relativePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Judge asset directory is invalid.");
        Directory.CreateDirectory(directory);

        var temporaryPath = fullPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await output.WriteAsync(content, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }

        return new StoredJudgeAssetFile
        {
            StoredFileName = storedFileName,
            StorageRelativePath = relativePath,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(content.Span)),
            FileSizeBytes = content.Length
        };
    }

    public async Task<string> ReadTextAsync(string storageRelativePath, long expectedFileSizeBytes, string expectedSha256, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(ResolvePath(storageRelativePath), cancellationToken);
        if (bytes.LongLength != expectedFileSizeBytes
            || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Judge asset integrity validation failed.");
        }

        return StrictUtf8.GetString(bytes);
    }

    public async Task<byte[]?> DeleteWithBackupAsync(string storageRelativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storageRelativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        File.Delete(fullPath);
        return content;
    }

    public async Task RestoreAsync(string storageRelativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storageRelativePath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Judge asset directory is invalid.");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(fullPath, content.ToArray(), cancellationToken);
    }

    public Task DeleteIfExistsAsync(string storageRelativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = ResolvePath(storageRelativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storageRelativePath)
            || Path.IsPathRooted(storageRelativePath)
            || storageRelativePath.Contains('\\'))
        {
            throw new InvalidDataException("Judge asset path is invalid.");
        }

        var platformRelativePath = storageRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, platformRelativePath));
        var rootPrefix = storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? storageRoot
            : storageRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new InvalidDataException("Judge asset path escapes the storage root.");
        }

        return fullPath;
    }

    private static string GetLanguageDirectory(JudgeLanguage language)
    {
        return language switch
        {
            JudgeLanguage.Cpp17 => "cpp17",
            JudgeLanguage.C11 => "c11",
            JudgeLanguage.CSharp => "csharp",
            _ => throw new InvalidDataException("Unsupported judge asset language.")
        };
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
