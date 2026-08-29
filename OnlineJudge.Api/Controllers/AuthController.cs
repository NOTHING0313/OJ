using Microsoft.AspNetCore.Authorization;
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
public class AuthController(
    IAuthService authService,
    IAccountService accountService,
    ICurrentUser currentUser,
    ILoginAbuseProtection loginAbuseProtection,
    ILogger<AuthController> logger) : ControllerBase
{
    [RiskRateLimit(RateLimitPolicies.AuthRegister)]
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

    [RiskRateLimit(RateLimitPolicies.AuthRegister)]
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
        var protection = await loginAbuseProtection.CheckAsync(request.Account, cancellationToken);
        if (!protection.IsAllowed)
        {
            RateLimitResponseWriter.SetRetryAfterHeader(Response, protection.RetryAfterSeconds);
            logger.LogWarning(
                "Rate limit rejected. Policy={PolicyName} RetryAfter={RetryAfter} Path={Path}",
                RateLimitPolicies.AuthLogin,
                protection.RetryAfterSeconds,
                Request.Path.Value);
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                RateLimitResponseWriter.CreatePayload(RateLimitPolicies.AuthLogin, protection.RetryAfterSeconds));
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
            return Unauthorized(result.ErrorMessage);
        }

        return Ok(result.Value);
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

        return Ok(result.Value);
    }
}
