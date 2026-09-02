using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.SecurityAudit;

namespace OnlineJudge.Tests.Security;

public sealed class SecurityHeadersAndAuditTests
{
    [Theory]
    [InlineData("default-src 'self'")]
    [InlineData("script-src 'self'")]
    [InlineData("object-src 'none'")]
    [InlineData("base-uri 'self'")]
    [InlineData("form-action 'self'")]
    [InlineData("frame-ancestors 'none'")]
    [InlineData("img-src 'self' data: blob:")]
    [InlineData("worker-src 'self' blob:")]
    public void ContentSecurityPolicy_ContainsRequiredDirective(string directive)
    {
        Assert.Contains(directive, SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentSecurityPolicy_DoesNotPermitDangerousScriptSources()
    {
        Assert.DoesNotContain("script-src *", SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-eval'", SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("http:", SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("https:", SecurityHeadersMiddleware.ContentSecurityPolicy, StringComparison.Ordinal);
    }

    [Fact]
    public void Middleware_AppliesRequiredHeadersWithoutHsts()
    {
        var headers = new HeaderDictionary { ["Strict-Transport-Security"] = "max-age=1" };
        SecurityHeadersMiddleware.Apply(headers);

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Equal("camera=(), microphone=(), geolocation=()", headers["Permissions-Policy"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, headers["Content-Security-Policy"]);
        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Writer_PersistsActorTargetAndTrustedContext()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var writer = CreateWriter(db, userId, "root", "127.0.0.1");
        writer.Stage(new SecurityAuditRecord(SecurityAuditActions.UserBlacklisted, "User", Guid.NewGuid().ToString()));
        await db.SaveChangesAsync();

        var log = Assert.Single(await db.SecurityAuditLogs.AsNoTracking().ToListAsync());
        Assert.Equal(userId, log.ActorUserId);
        Assert.Equal("root", log.ActorNameSnapshot);
        Assert.Equal("127.0.0.1", log.ClientIp);
        Assert.Equal(SecurityAuditResults.Succeeded, log.Result);
    }

    [Fact]
    public void Writer_RejectsMetadataOutsideWhitelist()
    {
        using var db = CreateDb();
        var writer = CreateWriter(db, Guid.NewGuid(), "root", null);
        Assert.Throws<ArgumentException>(() => writer.Stage(new SecurityAuditRecord(
            SecurityAuditActions.UserPasswordReset,
            "User",
            Metadata: new Dictionary<string, string?> { ["password"] = "redacted-test-value" })));
    }

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("jwt")]
    [InlineData("authorization")]
    [InlineData("cookie")]
    [InlineData("refreshToken")]
    [InlineData("activeSessionId")]
    [InlineData("verificationCode")]
    [InlineData("connectionString")]
    [InlineData("gitCredential")]
    public void Writer_RejectsSensitiveMetadataKeys(string key)
    {
        using var db = CreateDb();
        var writer = CreateWriter(db, Guid.NewGuid(), "root", null);
        Assert.Throws<ArgumentException>(() => writer.Stage(new SecurityAuditRecord(
            SecurityAuditActions.UserPasswordReset,
            "User",
            Metadata: new Dictionary<string, string?> { [key] = "synthetic-test-value" })));
    }

    [Fact]
    public async Task AuditRows_AreAppendOnlyInApplicationDbContext()
    {
        await using var db = CreateDb();
        var log = NewLog(DateTimeOffset.UtcNow, SecurityAuditActions.HelpCreated, "root", "HelpDocument", Guid.NewGuid().ToString());
        db.SecurityAuditLogs.Add(log);
        await db.SaveChangesAsync();
        log.Result = SecurityAuditResults.Failed;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Query_IsStableOrderedAndPaged()
    {
        await using var db = CreateDb();
        var timestamp = DateTimeOffset.UtcNow;
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        db.SecurityAuditLogs.AddRange(
            NewLog(timestamp, SecurityAuditActions.HelpCreated, "root", "HelpDocument", "one", lowId),
            NewLog(timestamp, SecurityAuditActions.HelpUpdated, "root", "HelpDocument", "two", highId));
        await db.SaveChangesAsync();
        var service = new SecurityAuditQueryService(db);

        var result = await service.QueryAsync(new SecurityAuditQuery { Page = 1, PageSize = 1 });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal(highId, Assert.Single(result.Value.Items).Id);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("action")]
    [InlineData("result")]
    [InlineData("target")]
    [InlineData("date")]
    public async Task Query_SupportsRequiredFilter(string filter)
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        db.SecurityAuditLogs.AddRange(
            NewLog(now, SecurityAuditActions.HelpCreated, "root-user", "HelpDocument", "match"),
            NewLog(now.AddDays(-2), SecurityAuditActions.UserBlacklisted, "other", "User", "other"));
        await db.SaveChangesAsync();
        var query = new SecurityAuditQuery { PageSize = 100 };
        switch (filter)
        {
            case "actor": query.Actor = "root"; break;
            case "action": query.Action = SecurityAuditActions.HelpCreated; break;
            case "result": query.Result = SecurityAuditResults.Succeeded; break;
            case "target": query.TargetId = "match"; break;
            case "date": query.From = now.AddHours(-1); break;
        }

        var result = await new SecurityAuditQueryService(db).QueryAsync(query);
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.Items);
        if (filter != "result") Assert.Single(result.Value.Items);
    }

    [Fact]
    public async Task AuditQuery_DoesNotCreateRecursiveAuditRow()
    {
        await using var db = CreateDb();
        db.SecurityAuditLogs.Add(NewLog(DateTimeOffset.UtcNow, SecurityAuditActions.HelpCreated, "root", "HelpDocument", "one"));
        await db.SaveChangesAsync();
        var before = await db.SecurityAuditLogs.CountAsync();

        await new SecurityAuditQueryService(db).QueryAsync(new SecurityAuditQuery());

        Assert.Equal(before, await db.SecurityAuditLogs.CountAsync());
    }

    [Fact]
    public void QueryController_IsRootOnlyAndReadOnly()
    {
        var authorize = typeof(SecurityAuditController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("RequireRoot", authorize?.Policy);
        var httpMethods = typeof(SecurityAuditController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes().Select(attribute => attribute.GetType().Name)).ToArray();
        Assert.DoesNotContain("HttpPostAttribute", httpMethods);
        Assert.DoesNotContain("HttpPutAttribute", httpMethods);
        Assert.DoesNotContain("HttpDeleteAttribute", httpMethods);
    }

    [Fact]
    public void FrontendRouteAndNavigation_AreRootOnly()
    {
        var main = Read("frontend", "src", "main.tsx");
        var layout = Read("frontend", "src", "components", "AppHeaderView.tsx");
        Assert.Contains("path=\"/admin/security-audit\"", main, StringComparison.Ordinal);
        Assert.Contains("<ProtectedRoute allowedRoles={[3]}>", main, StringComparison.Ordinal);
        Assert.Contains("isRoot(role) && <NavLink to=\"/admin/security-audit\"", layout, StringComparison.Ordinal);
        Assert.Contains(">安全审计</NavLink>", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Monaco_UsesLocalAssetsInsteadOfCdnLoader()
    {
        var editor = Read("frontend", "src", "components", "CodeEditor.tsx");
        Assert.Contains("loader.config({ monaco })", editor, StringComparison.Ordinal);
        Assert.Contains("editor.worker?worker", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("cdn.jsdelivr", editor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuditCoverage_UsesCentralActionsWithoutSensitivePayloads()
    {
        var actions = Read("OnlineJudge.Application", "SecurityAudit", "SecurityAuditActions.cs");
        foreach (var action in new[] { "User.RoleChanged", "User.Blacklisted", "User.Deleted", "Problem.Created", "Problem.TestCasesChanged", "Challenge.Created", "Season.Frozen", "Help.Published", "TeamGit.SyncRequested", "SiteAppearance.Updated", "User.PasswordReset" })
        {
            Assert.Contains(action, actions, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("RequestBody", actions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", actions, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("OnlineJudge.Infrastructure", "Users", "UserService.cs", "SecurityAuditActions.UserRoleChanged")]
    [InlineData("OnlineJudge.Infrastructure", "Users", "UserService.cs", "SecurityAuditActions.UserBlacklisted")]
    [InlineData("OnlineJudge.Infrastructure", "Problems", "ProblemService.cs", "SecurityAuditActions.ProblemTestCasesChanged")]
    [InlineData("OnlineJudge.Infrastructure", "Challenges", "ChallengeService.cs", "SecurityAuditActions.ChallengeCreated")]
    [InlineData("OnlineJudge.Infrastructure", "Leaderboards", "LeaderboardSeasonService.cs", "SecurityAuditActions.SeasonPublished")]
    [InlineData("OnlineJudge.Infrastructure", "HelpDocuments", "HelpDocumentService.cs", "SecurityAuditActions.HelpDeleted")]
    [InlineData("OnlineJudge.Infrastructure", "Teams", "TeamGitRepositoryService.cs", "SecurityAuditActions.TeamGitSyncRequested")]
    [InlineData("OnlineJudge.Infrastructure", "SiteSettings", "SiteSettingsService.cs", "SecurityAuditActions.SiteAppearanceUpdated")]
    [InlineData("OnlineJudge.Infrastructure", "Account", "AccountService.cs", "SecurityAuditActions.UserPasswordReset")]
    [InlineData("OnlineJudge.Infrastructure", "Account", "AccountService.cs", "SecurityAuditActions.UserDeleted")]
    public void RequiredMutation_IsWiredToCentralAuditAction(string project, string area, string file, string action)
    {
        Assert.Contains(action, Read(project, area, file), StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_IsAfterSingleSessionAndContainsOnlyAuditSchema()
    {
        var directory = Path.Combine(ProjectRoot(), "OnlineJudge.Infrastructure", "Persistence", "Migrations");
        var migration = Assert.Single(Directory.GetFiles(directory, "*_AddSecurityAuditLogs.cs"));
        Assert.True(string.CompareOrdinal(Path.GetFileName(migration), "20260829152958_AddSingleActiveUserSession.cs") > 0);
        var source = File.ReadAllText(migration);
        Assert.Contains("SecurityAuditLogs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AlterColumn", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddColumn", source, StringComparison.Ordinal);
    }

    private static OnlineJudgeDbContext CreateDb() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SecurityAuditWriter CreateWriter(OnlineJudgeDbContext db, Guid userId, string userName, string? clientIp) => new(
        db,
        new TestCurrentUser(userId, userName),
        new SecurityAuditRequestContext { ClientIp = clientIp },
        TimeProvider.System);

    private static SecurityAuditLog NewLog(DateTimeOffset createdAt, string action, string actor, string targetType, string targetId, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(), ActorNameSnapshot = actor, Action = action, TargetType = targetType,
        TargetId = targetId, Result = SecurityAuditResults.Succeeded, CreatedAt = createdAt
    };

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([ProjectRoot(), .. parts]));

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class TestCurrentUser(Guid id, string name) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? UserName => name;
        public UserRole? Role => UserRole.Root;
    }
}
