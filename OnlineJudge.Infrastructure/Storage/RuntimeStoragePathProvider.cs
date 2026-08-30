using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Storage;

public interface IRuntimeStoragePathProvider
{
    string UploadImagesRoot { get; }

    string ChallengeFilesRoot { get; }

    string ResolveUploadImagePath(string storedFileName);

    string ResolveChallengeFilePath(string storedFileName);

    Task<long> WriteUploadImageAsync(string storedFileName, Stream content, long maxBytes, CancellationToken cancellationToken = default);

    Task<long> WriteChallengeFileAsync(string storedFileName, Stream content, long maxBytes, CancellationToken cancellationToken = default);
}

public sealed class RuntimeStoragePathProvider : IRuntimeStoragePathProvider
{
    public RuntimeStoragePathProvider(IConfiguration configuration)
        : this(
            ResolveApiContentRoot(),
            configuration[$"{RuntimeStorageOptions.SectionName}:UploadImagesRoot"],
            configuration[$"{RuntimeStorageOptions.SectionName}:ChallengeFilesRoot"])
    {
    }

    public RuntimeStoragePathProvider(string contentRootPath, string? uploadImagesRoot = null, string? challengeFilesRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        var contentRoot = Path.GetFullPath(contentRootPath);
        UploadImagesRoot = ResolveRoot(uploadImagesRoot, Path.Combine(contentRoot, "wwwroot", "uploads", "images"));
        ChallengeFilesRoot = ResolveRoot(challengeFilesRoot, Path.Combine(contentRoot, "App_Data", "challenge-file-submissions"));
    }

    public string UploadImagesRoot { get; }

    public string ChallengeFilesRoot { get; }

    public string ResolveUploadImagePath(string storedFileName) => ResolveFile(UploadImagesRoot, storedFileName);

    public string ResolveChallengeFilePath(string storedFileName) => ResolveFile(ChallengeFilesRoot, storedFileName);

    public Task<long> WriteUploadImageAsync(string storedFileName, Stream content, long maxBytes, CancellationToken cancellationToken = default) =>
        WriteAtomicallyAsync(UploadImagesRoot, ResolveUploadImagePath(storedFileName), content, maxBytes, cancellationToken);

    public Task<long> WriteChallengeFileAsync(string storedFileName, Stream content, long maxBytes, CancellationToken cancellationToken = default) =>
        WriteAtomicallyAsync(ChallengeFilesRoot, ResolveChallengeFilePath(storedFileName), content, maxBytes, cancellationToken);

    public static RuntimeStoragePathProvider CreateDevelopmentDefault() => new(ResolveApiContentRoot());

    private static string ResolveRoot(string? configuredRoot, string defaultRoot)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot) ? defaultRoot : configuredRoot);
    }

    private static string ResolveFile(string root, string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName)
            || Path.IsPathRooted(storedFileName)
            || !string.Equals(Path.GetFileName(storedFileName), storedFileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Stored file name is invalid.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(root, storedFileName));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, GetPathComparison()))
        {
            throw new InvalidDataException("Stored file path escapes the configured root.");
        }

        return fullPath;
    }

    private static async Task<long> WriteAtomicallyAsync(string root, string fullPath, Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));

        Directory.CreateDirectory(root);
        var temporaryPath = Path.Combine(root, $".{Guid.NewGuid():N}.uploading");
        long totalBytes = 0;
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    if (totalBytes > maxBytes - read) throw new InvalidDataException("Uploaded file exceeds the configured size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalBytes += read;
                }

                await output.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath);
            return totalBytes;
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
        }
    }

    private static string ResolveApiContentRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDirectory, "OnlineJudge.Api.csproj"))) return currentDirectory;

        var apiDirectory = Path.Combine(currentDirectory, "OnlineJudge.Api");
        if (File.Exists(Path.Combine(apiDirectory, "OnlineJudge.Api.csproj"))) return apiDirectory;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OnlineJudge.Api.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        return currentDirectory;
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }
}
