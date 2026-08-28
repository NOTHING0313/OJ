using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Teams;

namespace OnlineJudge.Tests.Teams;

public class TeamGitSecurityTests
{
    [Fact]
    public async Task RuntimeValidator_AllowsOnlyWhenEveryResolvedAddressIsPublic()
    {
        var validator = RemoteValidator([IPAddress.Parse("140.82.112.4"), IPAddress.Parse("2606:50c0:8000::154")]);
        Assert.True((await validator.ValidateAsync("https://github.com/a/b.git", default)).IsSuccess);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.1.1")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    public async Task RuntimeValidator_RejectsNonPublicDnsResults(string address)
    {
        var result = await RemoteValidator([IPAddress.Parse(address)]).ValidateAsync("https://github.com/a/b.git", default);
        Assert.Equal("Repository host did not resolve exclusively to public addresses.", result.ErrorMessage);
    }

    [Fact]
    public async Task RuntimeValidator_RejectsMixedPublicAndPrivateDnsResults()
    {
        var result = await RemoteValidator([IPAddress.Parse("140.82.112.4"), IPAddress.Loopback]).ValidateAsync("https://github.com/a/b.git", default);
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("http://github.com/a/b.git")]
    [InlineData("https://evil.com/a/b.git")]
    public async Task RuntimeValidator_RevalidatesSchemeAndHost(string url)
    {
        Assert.True((await RemoteValidator([IPAddress.Parse("140.82.112.4")]).ValidateAsync(url, default)).IsFailure);
    }

    [Fact]
    public async Task RuntimeValidator_ReturnsSafeDnsFailure()
    {
        var validator = new TeamGitRemoteSecurityValidator(new TeamRepositoryUrlValidator(new TeamProjectOptions()), new FailingResolver(), new TeamProjectOptions());
        var result = await validator.ValidateAsync("https://github.com/a/b.git", default);
        Assert.Equal("Repository host could not be resolved.", result.ErrorMessage);
    }

    [Fact]
    public void ProcessRunner_UsesArgumentListControlledEnvironmentAndSecurityConfig()
    {
        using var environment = new GitTestEnvironment();
        var runner = new GitProcessRunner(environment.Storage);
        var startInfo = runner.CreateStartInfo(["clone", "https://github.com/a/b.git", "target"]);
        var arguments = startInfo.ArgumentList.ToList();

        Assert.Equal("git", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Contains("credential.helper=", arguments);
        Assert.Contains("protocol.allow=never", arguments);
        Assert.Contains("protocol.https.allow=always", arguments);
        Assert.Contains("protocol.file.allow=never", arguments);
        Assert.Contains("protocol.ext.allow=never", arguments);
        Assert.Contains("http.followRedirects=false", arguments);
        Assert.Contains(arguments, item => item.StartsWith("core.hooksPath=", StringComparison.Ordinal));
        Assert.Equal("0", startInfo.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.Equal("1", startInfo.Environment["GIT_CONFIG_NOSYSTEM"]);
        Assert.Equal("1", startInfo.Environment["GIT_LFS_SKIP_SMUDGE"]);
        Assert.Equal(environment.Storage.GlobalConfigPath, startInfo.Environment["GIT_CONFIG_GLOBAL"]);
        Assert.Equal(environment.Storage.HomeDirectory, startInfo.Environment["HOME"]);
        Assert.False(startInfo.Environment.ContainsKey("HTTPS_PROXY"));
        Assert.False(startInfo.Environment.ContainsKey("http_proxy"));
    }

    [Fact]
    public void RepositoryStorage_UsesUniqueContainedProjectIdPaths()
    {
        using var environment = new GitTestEnvironment();
        var first = environment.Storage.GetRepositoryPath(Guid.NewGuid());
        var second = environment.Storage.GetRepositoryPath(Guid.NewGuid());
        Assert.NotEqual(first, second);
        Assert.StartsWith(environment.Root, first, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        Assert.EndsWith(".git", first);
    }

    [Fact]
    public async Task ProcessOutputReaderCapsButContinuesDrainingStream()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', GitProcessRunner.OutputLimitCharacters + 4096));
        await using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var result = await GitProcessRunner.ReadCappedAsync(reader, default);
        Assert.True(result.Truncated);
        Assert.Equal(GitProcessRunner.OutputLimitCharacters, result.Text.Length);
        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public async Task FirstSyncUsesBoundedBareCloneAndSubsequentSyncUsesFetch()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var runner = new RecordingGitRunner();
        var service = environment.CreateService(db, seed.Auditor, runner);

        Assert.True((await service.SyncAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);
        Assert.Contains(runner.Requests, request => request.Arguments.Contains("clone")
            && request.Arguments.Contains("--bare")
            && request.Arguments.Contains("--depth=300")
            && request.Arguments.Contains("--no-tags")
            && request.Arguments.Contains("--filter=blob:none"));
        Assert.DoesNotContain(runner.Requests.SelectMany(request => request.Arguments), value => value is "checkout" or "switch" or "worktree" or "submodule" or "push");

        environment.Time.Advance(TimeSpan.FromSeconds(11));
        Assert.True((await service.SyncAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);
        Assert.Contains(runner.Requests, request => request.Arguments.Contains("fetch")
            && request.Arguments.Contains("--prune")
            && request.Arguments.Contains("+HEAD:refs/heads/oj-audit"));
        Assert.Contains(runner.Requests, request => request.Arguments.Contains("rev-parse") && request.Arguments.Contains("HEAD^{commit}"));
    }

    [Fact]
    public async Task SyncUpdatesAttemptSuccessAndCooldownState()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var service = environment.CreateService(db, seed.Auditor, new RecordingGitRunner());

        Assert.True((await service.SyncAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);
        var project = await db.TeamProjects.SingleAsync();
        Assert.Equal(TeamProjectSyncStatus.Success, project.LastSyncStatus);
        Assert.NotNull(project.LastSyncAttemptAt);
        Assert.NotNull(project.LastSyncedAt);
        Assert.Equal("main", project.DefaultBranch);
        Assert.Equal("Repository was synchronized too recently.", (await service.SyncAsync(seed.Team.Id, seed.Project.Id)).ErrorMessage);
    }

    [Fact]
    public async Task FailedRefreshRetainsPriorCacheAndStoresOnlySafeError()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var runner = new RecordingGitRunner();
        var service = environment.CreateService(db, seed.Auditor, runner);
        Assert.True((await service.SyncAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);
        var repositoryPath = environment.Storage.GetRepositoryPath(seed.Project.Id);
        Assert.True(Directory.Exists(repositoryPath));

        environment.Time.Advance(TimeSpan.FromSeconds(11));
        runner.FailFetch = true;
        var result = await service.SyncAsync(seed.Team.Id, seed.Project.Id);
        Assert.Equal("Repository synchronization failed.", result.ErrorMessage);
        Assert.True(Directory.Exists(repositoryPath));
        var project = await db.TeamProjects.SingleAsync();
        Assert.Equal(TeamProjectSyncStatus.Failed, project.LastSyncStatus);
        Assert.NotNull(project.LastSyncedAt);
        Assert.DoesNotContain(environment.Root, project.LastSyncError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OversizeFirstSyncFailsAndRemovesInvalidCache()
    {
        using var environment = new GitTestEnvironment(maxRepositorySizeMb: 1);
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var runner = new RecordingGitRunner { LargeClone = true };

        var result = await environment.CreateService(db, seed.Auditor, runner).SyncAsync(seed.Team.Id, seed.Project.Id);

        Assert.Equal("Repository exceeds synchronization size limit.", result.ErrorMessage);
        Assert.False(environment.Storage.Exists(seed.Project.Id));
        Assert.Equal(TeamProjectSyncStatus.Failed, (await db.TeamProjects.SingleAsync()).LastSyncStatus);
    }

    [Fact]
    public async Task HistoryRequiresAuditRoleAndEnforcesBoundsAndSyncedCache()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var runner = new RecordingGitRunner();
        var answererService = environment.CreateService(db, seed.Owner, runner);
        Assert.Equal("Forbidden.", (await answererService.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id)).ErrorMessage);

        var auditService = environment.CreateService(db, seed.Auditor, runner);
        Assert.Equal("Limit must be between 1 and 100.", (await auditService.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id, limit: 101)).ErrorMessage);
        Assert.Equal("Skip must be between 0 and 300.", (await auditService.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id, skip: 301)).ErrorMessage);
        Assert.Equal("Repository has not been synchronized.", (await auditService.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id)).ErrorMessage);
    }

    [Fact]
    public async Task RootCanAuditProjectsAndMissingCacheReturnsSafeError()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var root = User("root", UserRole.Root, DateTimeOffset.UtcNow);
        db.Users.Add(root);
        seed.Project.LastSyncedAt = DateTimeOffset.UtcNow;
        seed.Project.LastSyncStatus = TeamProjectSyncStatus.Success;
        await db.SaveChangesAsync();
        var service = environment.CreateService(db, root, new RecordingGitRunner());
        Assert.True((await service.GetProjectsAsync(seed.Team.Id)).IsSuccess);
        Assert.Equal("Repository cache is unavailable.", (await service.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id)).ErrorMessage);
    }

    [Fact]
    public void HistoryParser_ParsesUnicodeSanitizesControlsAndTruncatesSubject()
    {
        var subject = "中文😀\t" + new string('x', 600);
        var output = string.Join('\0',
            "0123456789abcdef0123456789abcdef01234567", "0123456", "A\tuthor", "a@example.com",
            "2026-08-28T12:00:00+00:00", "提交者", "c@example.com", "2026-08-28T12:01:00+00:00", subject, string.Empty);

        var result = TeamGitRepositoryService.ParseHistory(output);

        Assert.True(result.IsSuccess);
        var commit = Assert.Single(result.Value!);
        Assert.Equal("Author", commit.AuthorName);
        Assert.StartsWith("中文😀", commit.Subject);
        Assert.Equal(500, commit.Subject.Length);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", commit.Sha);
    }

    [Fact]
    public async Task HistoryReturnsNewestFirstRunnerOutputWithoutPatchOrFileContent()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var runner = new RecordingGitRunner();
        var service = environment.CreateService(db, seed.Auditor, runner);
        Assert.True((await service.SyncAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);

        var history = await service.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id);

        Assert.True(history.IsSuccess);
        Assert.Equal(["newest", "older"], history.Value!.Select(commit => commit.Subject));
        var log = runner.Requests.Single(request => request.Arguments.Contains("log"));
        Assert.DoesNotContain("--patch", log.Arguments);
        Assert.DoesNotContain("--stat", log.Arguments);
    }

    [Fact]
    public async Task ProjectSyncLockSerializesSameProject()
    {
        var provider = new TeamGitSyncLockProvider();
        var gate = provider.Get(Guid.Empty);
        var concurrent = 0;
        var maximum = 0;
        async Task Work()
        {
            await gate.WaitAsync();
            try
            {
                maximum = Math.Max(maximum, Interlocked.Increment(ref concurrent));
                await Task.Delay(30);
                Interlocked.Decrement(ref concurrent);
            }
            finally { gate.Release(); }
        }

        await Task.WhenAll(Work(), Work());
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task RepositoryUrlChangeAndProjectDeleteInvalidateCacheAndResetState()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        seed.Project.LastSyncStatus = TeamProjectSyncStatus.Success;
        seed.Project.LastSyncedAt = DateTimeOffset.UtcNow;
        seed.Project.DefaultBranch = "main";
        await db.SaveChangesAsync();
        var cache = new RecordingCache();
        var service = new TeamService(db, new TestCurrentUser(seed.Owner.Id), environment.Time, new TeamRepositoryUrlValidator(environment.Options), cache);

        var updated = await service.UpdateProjectAsync(seed.Team.Id, seed.Project.Id, new UpdateTeamProjectRequest
        {
            Name = seed.Project.Name,
            RepositoryUrl = "https://gitee.com/a/new.git"
        });

        Assert.True(updated.IsSuccess);
        Assert.Equal(TeamProjectSyncStatus.NeverSynced, seed.Project.LastSyncStatus);
        Assert.Null(seed.Project.LastSyncedAt);
        Assert.Null(seed.Project.DefaultBranch);
        Assert.Contains(seed.Project.Id, cache.Deleted);
        cache.Deleted.Clear();
        Assert.True((await service.DeleteProjectAsync(seed.Team.Id, seed.Project.Id)).IsSuccess);
        Assert.Contains(seed.Project.Id, cache.Deleted);
    }

    [Fact]
    public async Task AnonymousCannotReadHistory()
    {
        using var environment = new GitTestEnvironment();
        await using var db = environment.CreateDb();
        var seed = await SeedAsync(db);
        var service = new TeamGitRepositoryService(
            db, new TestCurrentUser(null, authenticated: false),
            new TeamGitRemoteSecurityValidator(new TeamRepositoryUrlValidator(environment.Options), new FixedResolver([IPAddress.Parse("140.82.112.4")]), environment.Options),
            new RecordingGitRunner(), environment.Storage, new TeamGitSyncLockProvider(), environment.Options, environment.Time);
        Assert.Equal("Unauthorized.", (await service.GetCommitHistoryAsync(seed.Team.Id, seed.Project.Id)).ErrorMessage);
    }

    [Fact]
    public void SourceContract_KillsProcessTreeCapsOutputAndContainsNoShellOrPush()
    {
        var root = FindRepositoryRoot();
        var runner = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Teams", "GitProcessRunner.cs"));
        var repository = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Teams", "TeamGitRepositoryService.cs"));
        Assert.Contains("ArgumentList.Add", runner);
        Assert.Contains("Kill(entireProcessTree: true)", runner);
        Assert.Contains("OutputLimitCharacters = 64 * 1024", runner);
        Assert.DoesNotContain("bash", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cmd.exe", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"push\"", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"checkout\"", repository, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"submodule\"", repository, StringComparison.OrdinalIgnoreCase);
    }

    private static TeamGitRemoteSecurityValidator RemoteValidator(IPAddress[] addresses)
    {
        var options = new TeamProjectOptions();
        return new TeamGitRemoteSecurityValidator(new TeamRepositoryUrlValidator(options), new FixedResolver(addresses), options);
    }

    private static async Task<(User Owner, User Auditor, Team Team, TeamProject Project)> SeedAsync(OnlineJudgeDbContext db)
    {
        var now = DateTimeOffset.UtcNow;
        var owner = User("owner", UserRole.Answerer, now);
        var auditor = User("setter", UserRole.ProblemSetter, now);
        var team = new Team { Id = Guid.NewGuid(), Name = "Alpha", NormalizedName = "ALPHA", OwnerUserId = owner.Id, OwnerUser = owner, CreatedAt = now, UpdatedAt = now };
        var project = new TeamProject
        {
            Id = Guid.NewGuid(), TeamId = team.Id, Team = team, Name = "Repo", NormalizedName = "REPO",
            RepositoryUrl = "https://github.com/octocat/Hello-World.git", NormalizedRepositoryUrl = "https://github.com/octocat/Hello-World.git",
            CreatedByUserId = owner.Id, CreatedByUser = owner, LastSyncStatus = TeamProjectSyncStatus.NeverSynced, CreatedAt = now, UpdatedAt = now
        };
        db.AddRange(owner, auditor, team, project);
        await db.SaveChangesAsync();
        return (owner, auditor, team, project);
    }

    private static User User(string name, UserRole role, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.com", PasswordHash = "hash", Role = role, CreatedAt = now, UpdatedAt = now
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private sealed class GitTestEnvironment : IDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString();

        public GitTestEnvironment(int maxRepositorySizeMb = 10)
        {
            Root = Path.Combine(Path.GetTempPath(), $"oj-team-git-tests-{Guid.NewGuid():N}");
            Options = new TeamProjectOptions
            {
                RepositoryStorageRoot = Root, MaxRepositorySizeMb = maxRepositorySizeMb,
                GitTimeoutSeconds = 5, MaxCommitHistory = 300, SyncCooldownSeconds = 10
            };
            Storage = new TeamGitRepositoryStorage(Options);
            Time = new MutableTimeProvider(DateTimeOffset.Parse("2026-08-28T12:00:00Z"));
        }

        public string Root { get; }
        public TeamProjectOptions Options { get; }
        public TeamGitRepositoryStorage Storage { get; }
        public MutableTimeProvider Time { get; }

        public OnlineJudgeDbContext CreateDb() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseInMemoryDatabase(databaseName).Options);

        public TeamGitRepositoryService CreateService(OnlineJudgeDbContext db, User user, IGitProcessRunner runner)
        {
            return new TeamGitRepositoryService(
                db, new TestCurrentUser(user.Id),
                new TeamGitRemoteSecurityValidator(new TeamRepositoryUrlValidator(Options), new FixedResolver([IPAddress.Parse("140.82.112.4")]), Options),
                runner, Storage, new TeamGitSyncLockProvider(), Options, Time);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); } catch { }
        }
    }

    private sealed class RecordingGitRunner : IGitProcessRunner
    {
        public List<GitProcessRequest> Requests { get; } = [];
        public bool FailFetch { get; set; }
        public bool LargeClone { get; set; }

        public Task<GitProcessResult> RunAsync(GitProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (request.Arguments.Contains("--version")) return Success("git version 2.45.0\n");
            if (request.Arguments.Contains("clone"))
            {
                var path = request.Arguments[^1];
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, "objects.dat"), new byte[LargeClone ? 2 * 1024 * 1024 : 32]);
                return Success();
            }

            if (request.Arguments.Contains("fetch")) return FailFetch ? Failure("remote failure with internal detail") : Success();
            if (request.Arguments.Contains("symbolic-ref")) return Success("main\n");
            if (request.Arguments.Contains("log")) return Success(HistoryOutput());
            return Success();
        }

        private static Task<GitProcessResult> Success(string output = "") => Task.FromResult(new GitProcessResult(0, output, string.Empty, false, false));
        private static Task<GitProcessResult> Failure(string error) => Task.FromResult(new GitProcessResult(1, string.Empty, error, false, false));
        private static string HistoryOutput()
        {
            string Record(string sha, string subject) => string.Join('\0', sha, sha[..7], "Author", "a@example.com", "2026-08-28T12:00:00+00:00", "Committer", "c@example.com", "2026-08-28T12:01:00+00:00", subject, string.Empty);
            return Record("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "newest") + "\n" + Record("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "older");
        }
    }

    private sealed class FixedResolver(IPAddress[] addresses) : ITeamGitHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => Task.FromResult(addresses);
    }

    private sealed class FailingResolver : ITeamGitHostResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) => throw new System.Net.Sockets.SocketException();
    }

    private sealed class RecordingCache : ITeamGitRepositoryCache
    {
        public List<Guid> Deleted { get; } = [];
        public Task DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(projectId);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUser(Guid? id, bool authenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => authenticated;
        public Guid? UserId => id;
        public string? UserName => null;
        public UserRole? Role => null;
    }

    public sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
