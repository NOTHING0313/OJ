using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Account.Services;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await accountService.GetMeAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPut("avatar")]
    public async Task<IActionResult> UpdateAvatar(UpdateAvatarRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.UpdateAvatarAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.UpdateProfileAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPut("leaderboard-anonymity")]
    public async Task<IActionResult> UpdateLeaderboardAnonymity(UpdateLeaderboardAnonymityRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.UpdateLeaderboardAnonymityAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpGet("appearance")]
    public async Task<IActionResult> GetAppearance(CancellationToken cancellationToken)
    {
        var result = await accountService.GetAppearanceAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPut("appearance")]
    public async Task<IActionResult> UpdateAppearance(UpdateUserAppearanceRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.UpdateAppearanceAsync(request, Request.Host.Host, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("phone/send-code")]
    public async Task<IActionResult> SendPhoneCode(SendPhoneCodeRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.SendBindPhoneCodeAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("phone/verify")]
    public async Task<IActionResult> VerifyPhone(VerifyPhoneRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.VerifyAndBindPhoneAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("delete/send-code")]
    public async Task<IActionResult> SendDeleteCode(CancellationToken cancellationToken)
    {
        var result = await accountService.SendAccountDeleteCodeAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("delete/confirm")]
    [SecurityAudit(SecurityAuditActions.UserDeleted, "User")]
    public async Task<IActionResult> ConfirmDelete(ConfirmAccountDeleteRequest request, CancellationToken cancellationToken)
    {
        var result = await accountService.ConfirmAccountDeleteAsync(request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    private IActionResult ToFailureResult(string? errorMessage)
    {
        return errorMessage switch
        {
            "Unauthorized." => Unauthorized(errorMessage),
            "Forbidden." or "Account is blacklisted." or "Account has been deleted." => Forbid(),
            _ => BadRequest(errorMessage)
        };
    }
}
