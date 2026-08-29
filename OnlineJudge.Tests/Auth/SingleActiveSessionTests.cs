using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Auth.Responses;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Users;

namespace OnlineJudge.Tests.Auth;

public class SingleActiveSessionTests
{
    private const string ErrorCodeItem = "OnlineJudge.Auth.ErrorCode";

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    [InlineData(UserRole.Root)]
    public async Task SuccessfulLogin_CreatesIssuedSessionAndMatchingSid(UserRole role)
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, role: role);
        await dbContext.SaveChangesAsync();

        var before = DateTimeOffset.UtcNow;
        var result = await CreateAuthService(dbContext).LoginAsync(ValidLogin());
        var after = DateTimeOffset.UtcNow;

        Assert.True(result.IsSuccess);
        Assert.NotNull(user.ActiveSessionId);
        Assert.InRange(user.ActiveSessionIssuedAt!.Value, before, after);
        Assert.Equal(user.ActiveSessionId, ReadSessionId(result.Value!.AccessToken));
    }

    [Fact]
    public async Task SecondLogin_ReplacesFirstSessionAndOnlyLatestTokenValidates()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        await dbContext.SaveChangesAsync();
        var service = CreateAuthService(dbContext);

        var first = await service.LoginAsync(ValidLogin());
        var firstSessionId = ReadSessionId(first.Value!.AccessToken);
        var second = await service.LoginAsync(ValidLogin());
        var secondSessionId = ReadSessionId(second.Value!.AccessToken);
        var validator = new UserSessionValidator(dbContext);

        Assert.NotEqual(firstSessionId, secondSessionId);
        Assert.Equal(UserSessionValidationStatus.Replaced, (await validator.ValidateAsync(user.Id, firstSessionId)).Status);
        Assert.Equal(UserSessionValidationStatus.Valid, (await validator.ValidateAsync(user.Id, secondSessionId)).Status);
    }

    [Fact]
    public void CurrentLogout_UsesIdempotentConditionalSessionRevocation()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.Infrastructure", "Auth", "AuthService.cs"));

        Assert.Contains("user.Id == userId && user.ActiveSessionId == sessionId", source, StringComparison.Ordinal);
        Assert.Contains("SetProperty(user => user.ActiveSessionId, (Guid?)null)", source, StringComparison.Ordinal);
        Assert.Contains("SetProperty(user => user.ActiveSessionIssuedAt, (DateTimeOffset?)null)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StaleLogout_CannotMatchOrRevokeReplacementSession()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "OnlineJudge.Infrastructure", "Auth", "AuthService.cs"));

        Assert.DoesNotContain("FirstAsync(user => user.Id == userId", source, StringComparison.Ordinal);
        Assert.Contains("Where(user => user.Id == userId && user.ActiveSessionId == sessionId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedPassword_DoesNotRotateExistingSession()
    {
        await using var dbContext = CreateDbContext();
        var existingSessionId = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var user = AddUser(dbContext, activeSessionId: existingSessionId, activeSessionIssuedAt: issuedAt);
        await dbContext.SaveChangesAsync();

        var attempt = await CreateAuthService(dbContext).LoginWithOutcomeAsync(new LoginRequest
        {
            Account = user.UserName,
            Password = "wrong-password"
        });
        var result = attempt.Result;

        Assert.True(result.IsFailure);
        Assert.Equal(LoginFailureKind.InvalidPassword, attempt.FailureKind);
        Assert.Equal(existingSessionId, user.ActiveSessionId);
        Assert.Equal(issuedAt, user.ActiveSessionIssuedAt);
    }

    [Fact]
    public async Task UnknownUser_DoesNotRotateAnySession()
    {
        await using var dbContext = CreateDbContext();
        var existingSessionId = Guid.NewGuid();
        var user = AddUser(dbContext, activeSessionId: existingSessionId);
        await dbContext.SaveChangesAsync();

        var attempt = await CreateAuthService(dbContext).LoginWithOutcomeAsync(new LoginRequest
        {
            Account = "missing-user",
            Password = "password"
        });
        var result = attempt.Result;

        Assert.True(result.IsFailure);
        Assert.Equal(LoginFailureKind.Other, attempt.FailureKind);
        Assert.Equal(existingSessionId, user.ActiveSessionId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DisabledUserLogin_DoesNotRotateSession(bool isBlacklisted, bool isDeleted)
    {
        await using var dbContext = CreateDbContext();
        var existingSessionId = Guid.NewGuid();
        var user = AddUser(dbContext, isBlacklisted: isBlacklisted, isDeleted: isDeleted, activeSessionId: existingSessionId);
        await dbContext.SaveChangesAsync();

        var result = await CreateAuthService(dbContext).LoginAsync(ValidLogin());

        Assert.True(result.IsFailure);
        Assert.Equal(existingSessionId, user.ActiveSessionId);
    }

    [Theory]
    [InlineData(false, false, true, UserSessionValidationStatus.Invalid)]
    [InlineData(true, false, true, UserSessionValidationStatus.Invalid)]
    [InlineData(false, true, true, UserSessionValidationStatus.Invalid)]
    [InlineData(false, false, false, UserSessionValidationStatus.Invalid)]
    public async Task Validator_RejectsMissingDisabledOrSessionlessUsers(
        bool isBlacklisted,
        bool isDeleted,
        bool createUser,
        UserSessionValidationStatus expected)
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        if (createUser)
        {
            AddUser(dbContext, userId, isBlacklisted: isBlacklisted, isDeleted: isDeleted);
            await dbContext.SaveChangesAsync();
        }

        var result = await new UserSessionValidator(dbContext).ValidateAsync(userId, Guid.NewGuid());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task SameJwt_MultipleTabSimulation_RemainsValid()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        await dbContext.SaveChangesAsync();
        var login = await CreateAuthService(dbContext).LoginAsync(ValidLogin());
        var sessionId = ReadSessionId(login.Value!.AccessToken);
        var validator = new UserSessionValidator(dbContext);

        var validations = await Task.WhenAll(
            validator.ValidateAsync(user.Id, sessionId),
            validator.ValidateAsync(user.Id, sessionId),
            validator.ValidateAsync(user.Id, sessionId));

        Assert.All(validations, result => Assert.Equal(UserSessionValidationStatus.Valid, result.Status));
    }

    [Fact]
    public async Task TokenValidated_ValidSidPassesAndReplacesStaleRoleWithAuthoritativeRole()
    {
        await using var dbContext = CreateDbContext();
        var sessionId = Guid.NewGuid();
        var user = AddUser(dbContext, role: UserRole.ProblemSetter, activeSessionId: sessionId);
        await dbContext.SaveChangesAsync();
        var context = CreateTokenValidatedContext(CreatePrincipal(user.Id, sessionId, UserRole.Answerer));

        await CreateEvents(dbContext).TokenValidated(context);

        Assert.Null(context.Result?.Failure);
        var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
        Assert.Equal(UserRole.ProblemSetter.ToString(), principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(UserRole.ProblemSetter.ToString(), principal.FindFirstValue(AuthSessionConstants.AuthoritativeRoleClaim));
    }

    [Fact]
    public async Task TokenValidated_MissingSidFailsWithSessionInvalid()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, activeSessionId: Guid.NewGuid());
        await dbContext.SaveChangesAsync();
        var principal = CreatePrincipal(user.Id, null, user.Role);
        var context = CreateTokenValidatedContext(principal);

        await CreateEvents(dbContext).TokenValidated(context);

        Assert.NotNull(context.Result?.Failure);
        Assert.Equal(AuthSessionConstants.SessionInvalid, context.HttpContext.Items[ErrorCodeItem]);
    }

    [Fact]
    public async Task TokenValidated_WrongSidFailsWithSessionReplaced()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, activeSessionId: Guid.NewGuid());
        await dbContext.SaveChangesAsync();
        var context = CreateTokenValidatedContext(CreatePrincipal(user.Id, Guid.NewGuid(), user.Role));

        await CreateEvents(dbContext).TokenValidated(context);

        Assert.NotNull(context.Result?.Failure);
        Assert.Equal(AuthSessionConstants.SessionReplaced, context.HttpContext.Items[ErrorCodeItem]);
    }

    [Theory]
    [InlineData(AuthSessionConstants.SessionReplaced, "账号已在其他设备登录，请重新登录。")]
    [InlineData(AuthSessionConstants.SessionInvalid, "登录状态已失效，请重新登录。")]
    [InlineData(AuthSessionConstants.TokenExpired, "登录已过期，请重新登录。")]
    public async Task Challenge_WritesStableAuthenticationErrorContract(string errorCode, string expectedMessage)
    {
        await using var dbContext = CreateDbContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Items[ErrorCodeItem] = errorCode;
        var context = new JwtBearerChallengeContext(httpContext, BearerScheme(), new JwtBearerOptions(), new AuthenticationProperties());

        await CreateEvents(dbContext).Challenge(context);

        httpContext.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(httpContext.Response.Body);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.Equal(errorCode, json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(expectedMessage, json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AuthenticationFailed_ExpiredTokenUsesExpiredErrorCode()
    {
        await using var dbContext = CreateDbContext();
        var httpContext = new DefaultHttpContext();
        var context = new AuthenticationFailedContext(httpContext, BearerScheme(), new JwtBearerOptions())
        {
            Exception = new SecurityTokenExpiredException()
        };

        await CreateEvents(dbContext).AuthenticationFailed(context);

        Assert.Equal(AuthSessionConstants.TokenExpired, httpContext.Items[ErrorCodeItem]);
    }

    [Fact]
    public async Task ConcurrentLogins_FinalDatabaseSessionMatchesExactlyOneReturnedToken()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var seedContext = new OnlineJudgeDbContext(options))
        {
            AddUser(seedContext);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = new OnlineJudgeDbContext(options);
        await using var secondContext = new OnlineJudgeDbContext(options);
        var results = await Task.WhenAll(
            CreateAuthService(firstContext).LoginAsync(ValidLogin()),
            CreateAuthService(secondContext).LoginAsync(ValidLogin()));

        await using var verifyContext = new OnlineJudgeDbContext(options);
        var activeSessionId = (await verifyContext.Users.SingleAsync()).ActiveSessionId;
        var returnedSessionIds = results.Select(result => ReadSessionId(result.Value!.AccessToken)).ToArray();

        Assert.NotNull(activeSessionId);
        Assert.Equal(1, returnedSessionIds.Count(sessionId => sessionId == activeSessionId));
    }

    [Fact]
    public async Task Blacklist_ClearsTargetSessionWithoutRevokingAdministrator()
    {
        await using var dbContext = CreateDbContext();
        var administratorSession = Guid.NewGuid();
        var targetSession = Guid.NewGuid();
        var administrator = AddUser(dbContext, role: UserRole.Root, activeSessionId: administratorSession);
        var target = AddUser(dbContext, userId: Guid.NewGuid(), userName: "target", email: "target@example.test", activeSessionId: targetSession);
        await dbContext.SaveChangesAsync();
        var service = new UserService(dbContext, new TestCurrentUser(administrator.Id, administrator.Role));

        var result = await service.BlacklistAsync(target.Id);

        Assert.True(result.IsSuccess);
        Assert.True(target.IsBlacklisted);
        Assert.Null(target.ActiveSessionId);
        Assert.Null(target.ActiveSessionIssuedAt);
        Assert.Equal(administratorSession, administrator.ActiveSessionId);
    }

    [Fact]
    public async Task DatabaseValidationFailure_DoesNotReturnAValidSession()
    {
        var dbContext = CreateDbContext();
        var validator = new UserSessionValidator(dbContext);
        await dbContext.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => validator.ValidateAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void SecurityPipeline_UsesGlobalValidationConditionalLogoutAndSafeLogging()
    {
        var root = ProjectRoot();
        var program = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Program.cs"));
        var events = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Authentication", "ActiveSessionJwtBearerEvents.cs"));
        var constants = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Authentication", "AuthSessionConstants.cs"));
        var authService = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Auth", "AuthService.cs"));
        var workers = File.ReadAllText(Path.Combine(root, "OnlineJudge.JudgeWorker", "Worker.cs"));

        Assert.Contains("options.EventsType = typeof(ActiveSessionJwtBearerEvents)", program, StringComparison.Ordinal);
        Assert.Contains("AUTH_SESSION_INVALID", constants, StringComparison.Ordinal);
        Assert.Contains("AUTH_SESSION_REPLACED", constants, StringComparison.Ordinal);
        Assert.Contains("SecurityTokenExpiredException", events, StringComparison.Ordinal);
        Assert.Contains("user.Id == userId && user.ActiveSessionId == sessionId", authService, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessToken", events, StringComparison.Ordinal);
        Assert.DoesNotContain("Authorization", events, StringComparison.Ordinal);
        Assert.DoesNotContain("ActiveSessionId", workers, StringComparison.Ordinal);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static User AddUser(
        OnlineJudgeDbContext dbContext,
        Guid? userId = null,
        string userName = "answerer",
        string email = "answerer@example.test",
        UserRole role = UserRole.Answerer,
        bool isBlacklisted = false,
        bool isDeleted = false,
        Guid? activeSessionId = null,
        DateTimeOffset? activeSessionIssuedAt = null)
    {
        var user = new User
        {
            Id = userId ?? Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = new PasswordHasher().HashPassword("password"),
            Role = role,
            IsBlacklisted = isBlacklisted,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null,
            ActiveSessionId = activeSessionId,
            ActiveSessionIssuedAt = activeSessionId is null ? null : activeSessionIssuedAt ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static AuthService CreateAuthService(OnlineJudgeDbContext dbContext) =>
        new(dbContext, new PasswordHasher(), new JwtTokenGenerator(CreateConfiguration()), new FakeEmailVerificationService());

    private static LoginRequest ValidLogin() => new()
    {
        Account = "answerer",
        Password = "password"
    };

    private static Guid ReadSessionId(string accessToken)
    {
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(token.Claims.Single(claim => claim.Type == "sid").Value);
    }

    private static ActiveSessionJwtBearerEvents CreateEvents(OnlineJudgeDbContext dbContext) =>
        new(new UserSessionValidator(dbContext), NullLogger<ActiveSessionJwtBearerEvents>.Instance);

    private static TokenValidatedContext CreateTokenValidatedContext(ClaimsPrincipal principal) =>
        new(new DefaultHttpContext(), BearerScheme(), new JwtBearerOptions()) { Principal = principal };

    private static ClaimsPrincipal CreatePrincipal(Guid userId, Guid? sessionId, UserRole role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };
        if (sessionId is not null)
        {
            claims.Add(new Claim(AuthSessionConstants.SessionIdClaim, sessionId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme));
    }

    private static AuthenticationScheme BearerScheme() =>
        new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "OnlineJudge.Tests",
            ["Jwt:Audience"] = "OnlineJudge.Tests",
            ["Jwt:Secret"] = new string('x', 64),
            ["Jwt:ExpireMinutes"] = "120"
        }).Build();

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "administrator";
        public UserRole? Role => role;
    }

    private sealed class FakeEmailVerificationService : IEmailVerificationService
    {
        public Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<EmailSendResultDto>.Success(new EmailSendResultDto()));

        public Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
