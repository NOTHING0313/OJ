using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OnlineJudge.Infrastructure.Auth;

namespace OnlineJudge.Api.Authentication;

public sealed class ActiveSessionJwtBearerEvents(
    UserSessionValidator sessionValidator,
    ILogger<ActiveSessionJwtBearerEvents> logger) : JwtBearerEvents
{
    public override Task MessageReceived(MessageReceivedContext context)
    {
        if (!context.Request.Headers.ContainsKey("Authorization")
            && context.Request.Cookies.TryGetValue(BrowserSessionConstants.SessionCookieName, out var token)
            && !string.IsNullOrWhiteSpace(token))
        {
            context.Token = token;
            context.HttpContext.Items[BrowserSessionConstants.CookieAuthenticationItem] = true;
        }

        return Task.CompletedTask;
    }

    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdValue = context.Principal?.FindFirstValue(AuthSessionConstants.SessionIdClaim)
            ?? context.Principal?.FindFirstValue(ClaimTypes.Sid);

        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            Reject(context, AuthSessionConstants.SessionInvalid, userIdValue);
            return;
        }

        var validation = await sessionValidator.ValidateAsync(userId, sessionId, context.HttpContext.RequestAborted);
        if (validation.Status != UserSessionValidationStatus.Valid || validation.Role is null)
        {
            Reject(
                context,
                validation.Status == UserSessionValidationStatus.Replaced
                    ? AuthSessionConstants.SessionReplaced
                    : AuthSessionConstants.SessionInvalid,
                userIdValue);
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            Reject(context, AuthSessionConstants.SessionInvalid, userIdValue);
            return;
        }

        foreach (var claim in identity.FindAll(ClaimTypes.Role).Concat(identity.FindAll(AuthSessionConstants.AuthoritativeRoleClaim)).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Role, validation.Role.Value.ToString()));
        identity.AddClaim(new Claim(AuthSessionConstants.AuthoritativeRoleClaim, validation.Role.Value.ToString()));
    }

    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        if (context.Exception is SecurityTokenExpiredException)
        {
            context.HttpContext.Items[AuthSessionConstants.ErrorCodeItem] = AuthSessionConstants.TokenExpired;
        }

        return Task.CompletedTask;
    }

    public override async Task Challenge(JwtBearerChallengeContext context)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

        if (context.HttpContext.Items.ContainsKey(BrowserSessionConstants.CookieAuthenticationItem))
        {
            ClearBrowserCookies(context.Response);
        }

        var errorCode = context.HttpContext.Items[AuthSessionConstants.ErrorCodeItem] as string;
        await context.Response.WriteAsJsonAsync(new
        {
            errorCode,
            message = errorCode switch
            {
                AuthSessionConstants.SessionReplaced => "账号已在其他设备登录，请重新登录。",
                AuthSessionConstants.SessionInvalid => "登录状态已失效，请重新登录。",
                AuthSessionConstants.TokenExpired => "登录已过期，请重新登录。",
                _ => "未授权访问。"
            }
        }, context.HttpContext.RequestAborted);
    }

    private static void ClearBrowserCookies(HttpResponse response)
    {
        var options = new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Lax };
        response.Cookies.Delete(BrowserSessionConstants.SessionCookieName, options);
        response.Cookies.Delete(BrowserSessionConstants.CsrfCookieName, options);
        response.Cookies.Delete(BrowserSessionConstants.AntiforgeryCookieName, options);
    }

    private void Reject(TokenValidatedContext context, string errorCode, string? userId)
    {
        context.HttpContext.Items[AuthSessionConstants.ErrorCodeItem] = errorCode;
        logger.LogWarning("Authentication rejected for user {UserId}. Reason: {Reason}", userId, errorCode);
        context.Fail(errorCode);
    }
}
