using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Account.Services;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Auth.Responses;
using OnlineJudge.Application.Auth.Services;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Email.Requests;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Route("api/auth")]
[RequestSizeLimit(16 * 1024)]
public class AuthController(
    IAuthService authService,
    IAccountService accountService,
    ICurrentUser currentUser,
    ILoginAbuseProtection loginAbuseProtection,
    IAntiforgery antiforgery,
    IConfiguration configuration,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [HttpPost("register/send-code")]
    public async Task<IActionResult> SendRegisterEmailCode(SendRegisterEmailCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.SendRegisterEmailCodeAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.AuthLogin)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var (failure, response) = await TryLoginAsync(request, cancellationToken);
        return failure ?? Ok(response);
    }

    [RiskRateLimit(RateLimitPolicies.AuthLogin)]
    [HttpPost("session")]
    public async Task<IActionResult> CreateSession(LoginRequest request, CancellationToken cancellationToken)
    {
        var (failure, response) = await TryLoginAsync(request, cancellationToken);
        if (failure is not null || response is null)
        {
            return failure ?? Unauthorized();
        }

        var cookieOptions = BrowserCookieOptions(httpOnly: true);
        Response.Cookies.Append(BrowserSessionConstants.SessionCookieName, response.AccessToken, cookieOptions);

        return Ok(response.User);
    }

    private async Task<(IActionResult? Failure, LoginResponse? Response)> TryLoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var protection = await loginAbuseProtection.CheckAsync(request.Account, cancellationToken);
        if (!protection.IsAllowed)
        {
            RateLimitResponseWriter.SetRetryAfterHeader(Response, protection.RetryAfterSeconds);
            logger.LogWarning(
                "Rate limit rejected. Policy={PolicyName} RetryAfter={RetryAfter} Path={Path}",
                RateLimitPolicies.AuthLogin,
                protection.RetryAfterSeconds,
                Request.Path.Value);
            return (StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    RateLimitResponseWriter.CreatePayload(RateLimitPolicies.AuthLogin, protection.RetryAfterSeconds)),
                null);
        }

        var attempt = await authService.LoginWithOutcomeAsync(request, cancellationToken);
        var result = attempt.Result;

        if (result.IsSuccess)
        {
            await loginAbuseProtection.ResetAsync(request.Account, cancellationToken);
        }
        else if (attempt.FailureKind == LoginFailureKind.InvalidPassword)
        {
            await loginAbuseProtection.RecordFailedPasswordAsync(request.Account, cancellationToken);
        }

        if (result.IsFailure)
        {
            return (Unauthorized(result.ErrorMessage), null);
        }

        return (null, result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionIdValue = User.FindFirstValue(AuthSessionConstants.SessionIdClaim)
            ?? User.FindFirstValue(ClaimTypes.Sid);
        if (!Guid.TryParse(userIdValue, out var userId) || !Guid.TryParse(sessionIdValue, out var sessionId))
        {
            return Unauthorized();
        }

        await authService.LogoutAsync(userId, sessionId, cancellationToken);
        ClearBrowserCookies();
        return NoContent();
    }

    [RiskRateLimit(RateLimitPolicies.PasswordReset)]
    [HttpPost("password-reset/send-code")]
    public async Task<IActionResult> SendPasswordResetCode(SendPasswordResetCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.SendPasswordResetCodeAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.PasswordReset)]
    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordResetRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.ConfirmPasswordResetAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok();
    }

    [RiskRateLimit(RateLimitPolicies.PasswordReset)]
    [HttpPost("email-password-reset/send-code")]
    public async Task<IActionResult> SendEmailPasswordResetCode(SendEmailPasswordResetCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.SendEmailPasswordResetCodeAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.PasswordReset)]
    [HttpPost("email-password-reset/confirm")]
    public async Task<IActionResult> ConfirmEmailPasswordReset(ConfirmEmailPasswordResetRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.ConfirmEmailPasswordResetAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await authService.GetCurrentUserAsync(userId, cancellationToken);

        if (result.IsFailure)
        {
            return result.ErrorMessage is "Account is blacklisted." or "Account has been deleted."
                ? Forbid()
                : NotFound(result.ErrorMessage);
        }

        if (HttpContext.Items.ContainsKey(BrowserSessionConstants.CookieAuthenticationItem))
        {
            IssueCsrfCookies();
        }

        return Ok(result.Value);
    }

    private void IssueCsrfCookies()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        if (string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            throw new InvalidOperationException("Antiforgery request token was not generated.");
        }

        Response.Cookies.Append(
            BrowserSessionConstants.CsrfCookieName,
            tokens.RequestToken,
            BrowserCookieOptions(httpOnly: false));
    }

    private CookieOptions BrowserCookieOptions(bool httpOnly)
    {
        var expireMinutes = int.TryParse(configuration["Jwt:ExpireMinutes"], out var configuredExpireMinutes)
            ? configuredExpireMinutes
            : 120;
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(expireMinutes)
        };
    }

    private void ClearBrowserCookies()
    {
        var options = new CookieOptions { Path = "/", Secure = true, SameSite = SameSiteMode.Lax };
        Response.Cookies.Delete(BrowserSessionConstants.SessionCookieName, options);
        Response.Cookies.Delete(BrowserSessionConstants.CsrfCookieName, options);
        Response.Cookies.Delete(BrowserSessionConstants.AntiforgeryCookieName, options);
    }
}
