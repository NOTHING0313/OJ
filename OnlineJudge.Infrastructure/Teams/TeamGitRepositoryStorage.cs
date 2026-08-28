namespace OnlineJudge.Infrastructure.Teams;

public interface ITeamGitRepositoryCache
{
    Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default);
}

public interface ITeamGitRepositoryStorage : ITeamGitRepositoryCache
{
    string HomeDirectory { get; }
    string GlobalConfigPath { get; }
    string HooksDirectory { get; }
    string GetRepositoryPath(Guid projectId);
    string CreateTemporaryRepositoryPath(Guid projectId);
    bool Exists(Guid projectId);
    Task CopyToTemporaryAsync(Guid projectId, string temporaryPath, CancellationToken cancellationToken);
    Task PromoteAsync(Guid projectId, string temporaryPath, CancellationToken cancellationToken);
    long GetSizeBytes(string repositoryPath);
    void DeleteTemporary(string temporaryPath);
}

public sealed class TeamGitRepositoryStorage : ITeamGitRepositoryStorage
{
    private readonly string storageRoot;

    public TeamGitRepositoryStorage(TeamProjectOptions options)
    {
        storageRoot = Path.GetFullPath(options.RepositoryStorageRoot);
        Directory.CreateDirectory(storageRoot);
        var controlRoot = ResolveContainedPath("_control");
        HomeDirectory = Path.Combine(controlRoot, "home");
        HooksDirectory = Path.Combine(controlRoot, "hooks");
        GlobalConfigPath = Path.Combine(controlRoot, "gitconfig");
        Directory.CreateDirectory(HomeDirectory);
        Directory.CreateDirectory(HooksDirectory);
        if (!File.Exists(GlobalConfigPath))
        {
            File.WriteAllText(GlobalConfigPath, string.Empty);
        }
    }

    public string HomeDirectory { get; }
    public string GlobalConfigPath { get; }
    public string HooksDirectory { get; }

    public string GetRepositoryPath(Guid projectId) => ResolveContainedPath($"{projectId:N}.git");

    public string CreateTemporaryRepositoryPath(Guid projectId)
    {
        return ResolveContainedPath($"{projectId:N}.{Guid.NewGuid():N}.tmp.git");
    }

    public bool Exists(Guid projectId) => Directory.Exists(GetRepositoryPath(projectId));

    public Task CopyToTemporaryAsync(Guid projectId, string temporaryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureContained(temporaryPath);
        CopyDirectory(GetRepositoryPath(projectId), temporaryPath, cancellationToken);
        return Task.CompletedTask;
    }

    public Task PromoteAsync(Guid projectId, string temporaryPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureContained(temporaryPath);
        var finalPath = GetRepositoryPath(projectId);
        var backupPath = ResolveContainedPath($"{projectId:N}.{Guid.NewGuid():N}.backup.git");
        var hadExisting = Directory.Exists(finalPath);
        if (hadExisting)
        {
            Directory.Move(finalPath, backupPath);
        }

        try
        {
            Directory.Move(temporaryPath, finalPath);
        }
        catch
        {
            if (!Directory.Exists(finalPath) && Directory.Exists(backupPath))
            {
                Directory.Move(backupPath, finalPath);
            }

            throw;
        }

        if (hadExisting)
        {
            try { DeleteDirectorySafe(backupPath); } catch { }
        }

        return Task.CompletedTask;
    }

    public long GetSizeBytes(string repositoryPath)
    {
        EnsureContained(repositoryPath);
        long total = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(repositoryPath));
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in directory.EnumerateFileSystemInfos())
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Repository cache contains an unsafe link.");
                }

                if (entry is DirectoryInfo child)
                {
                    pending.Push(child);
                }
                else if (entry is FileInfo file)
                {
                    total = checked(total + file.Length);
                }
            }
        }

        return total;
    }

    public Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteDirectorySafe(GetRepositoryPath(projectId));
        return Task.CompletedTask;
    }

    public void DeleteTemporary(string temporaryPath)
    {
        EnsureContained(temporaryPath);
        DeleteDirectorySafe(temporaryPath);
    }

    private string ResolveContainedPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(storageRoot, relativePath));
        EnsureContained(fullPath);
        return fullPath;
    }

    private void EnsureContained(string fullPath)
    {
        var resolved = Path.GetFullPath(fullPath);
        var prefix = storageRoot.EndsWith(Path.DirectorySeparatorChar) ? storageRoot : storageRoot + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("Repository path escapes the storage root.");
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var source = new DirectoryInfo(sourcePath);
        if (!source.Exists)
        {
            throw new DirectoryNotFoundException("Repository cache is missing.");
        }

        Directory.CreateDirectory(destinationPath);
        foreach (var entry in source.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Repository cache contains an unsafe link.");
            }

            var destination = Path.Combine(destinationPath, entry.Name);
            if (entry is DirectoryInfo directory)
            {
                CopyDirectory(directory.FullName, destination, cancellationToken);
            }
            else if (entry is FileInfo file)
            {
                file.CopyTo(destination, overwrite: false);
            }
        }
    }

    private static void DeleteDirectorySafe(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        var directory = new DirectoryInfo(path);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            directory.Delete();
            return;
        }

        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                if (entry is DirectoryInfo linkDirectory) linkDirectory.Delete();
                else entry.Delete();
            }
            else if (entry is DirectoryInfo child)
            {
                DeleteDirectorySafe(child.FullName);
            }
            else
            {
                entry.Delete();
            }
        }

        directory.Delete();
    }
}

internal sealed class NullTeamGitRepositoryCache : ITeamGitRepositoryCache
{
    public Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
