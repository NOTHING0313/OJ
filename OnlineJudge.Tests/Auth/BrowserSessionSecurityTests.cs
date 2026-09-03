using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Auth;

public sealed class BrowserSessionSecurityTests
{
    [Fact]
    public async Task MessageReceived_UsesSessionCookieWhenAuthorizationHeaderIsAbsent()
    {
        await using var dbContext = CreateDbContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Cookie = $"{BrowserSessionConstants.SessionCookieName}=cookie-token";
        var context = new MessageReceivedContext(httpContext, BearerScheme(), new JwtBearerOptions());

        await CreateEvents(dbContext).MessageReceived(context);

        Assert.Equal("cookie-token", context.Token);
        Assert.True(httpContext.Items.ContainsKey("OnlineJudge.Auth.Cookie"));
    }

    [Fact]
    public async Task MessageReceived_AuthorizationHeaderTakesPriorityOverSessionCookie()
    {
        await using var dbContext = CreateDbContext();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = "Bearer explicit-token";
        httpContext.Request.Headers.Cookie = $"{BrowserSessionConstants.SessionCookieName}=cookie-token";
        var context = new MessageReceivedContext(httpContext, BearerScheme(), new JwtBearerOptions());

        await CreateEvents(dbContext).MessageReceived(context);

        Assert.Null(context.Token);
        Assert.False(httpContext.Items.ContainsKey("OnlineJudge.Auth.Cookie"));
    }

    [Fact]
    public async Task CookieAuthenticatedUnsafeRequest_RequiresValidAntiforgeryToken()
    {
        var reachedNext = false;
        var middleware = new CookieAntiforgeryMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });
        var httpContext = AuthenticatedContext(HttpMethods.Post, cookieAuthenticated: true);
        httpContext.Response.Body = new MemoryStream();
        var antiforgery = new TestAntiforgery(throwOnValidate: true);

        await middleware.InvokeAsync(httpContext, antiforgery);

        Assert.False(reachedNext);
        Assert.Equal(1, antiforgery.ValidationCount);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST", false)]
    [InlineData("GET", true)]
    public async Task BearerOrSafeRequests_AreExemptFromAntiforgeryValidation(string method, bool cookieAuthenticated)
    {
        var reachedNext = false;
        var middleware = new CookieAntiforgeryMiddleware(_ =>
        {
            reachedNext = true;
            return Task.CompletedTask;
        });
        var httpContext = AuthenticatedContext(method, cookieAuthenticated);
        var antiforgery = new TestAntiforgery(throwOnValidate: true);

        await middleware.InvokeAsync(httpContext, antiforgery);

        Assert.True(reachedNext);
        Assert.Equal(0, antiforgery.ValidationCount);
    }

    private static DefaultHttpContext AuthenticatedContext(string method, bool cookieAuthenticated)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            JwtBearerDefaults.AuthenticationScheme));
        if (cookieAuthenticated)
        {
            context.Items["OnlineJudge.Auth.Cookie"] = true;
        }

        return context;
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static ActiveSessionJwtBearerEvents CreateEvents(OnlineJudgeDbContext dbContext) =>
        new(new UserSessionValidator(dbContext), NullLogger<ActiveSessionJwtBearerEvents>.Instance);

    private static AuthenticationScheme BearerScheme() =>
        new(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));

    private sealed class TestAntiforgery(bool throwOnValidate) : IAntiforgery
    {
        public int ValidationCount { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();
        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => throw new NotSupportedException();
        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext)
        {
            ValidationCount++;
            return throwOnValidate
                ? Task.FromException(new AntiforgeryValidationException("invalid"))
                : Task.CompletedTask;
        }
    }
}
