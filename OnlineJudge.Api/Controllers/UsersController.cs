using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Users.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    [Authorize(Policy = "RequireRoot")]
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] string? keyword, [FromQuery] UserRole? role, [FromQuery] bool? isBlacklisted, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await userService.GetUsersAsync(keyword, role, isBlacklisted, page, pageSize, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("problem-setters")]
    public async Task<IActionResult> GetProblemSetters(CancellationToken cancellationToken)
    {
        var result = await userService.GetProblemSettersAsync(cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.UserRoleChanged, "User", "id")]
    [HttpPost("{id:guid}/promote-to-problem-setter")]
    public async Task<IActionResult> PromoteToProblemSetter(Guid id, CancellationToken cancellationToken)
    {
        var result = await userService.PromoteToProblemSetterAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.UserRoleChanged, "User", "id")]
    [HttpPost("{id:guid}/demote-to-answerer")]
    public async Task<IActionResult> DemoteToAnswerer(Guid id, CancellationToken cancellationToken)
    {
        var result = await userService.DemoteToAnswererAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.UserBlacklisted, "User", "id")]
    [HttpPost("{id:guid}/blacklist")]
    public async Task<IActionResult> Blacklist(Guid id, CancellationToken cancellationToken)
    {
        var result = await userService.BlacklistAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.UserUnblacklisted, "User", "id")]
    [HttpPost("{id:guid}/unblacklist")]
    public async Task<IActionResult> Unblacklist(Guid id, CancellationToken cancellationToken)
    {
        var result = await userService.UnblacklistAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    private IActionResult ToFailureResult(string? errorMessage)
    {
        return errorMessage switch
        {
            "Unauthorized." => Unauthorized(errorMessage),
            "Forbidden." or "Account is blacklisted." => Forbid(),
            "User not found." => NotFound(errorMessage),
            _ => BadRequest(errorMessage)
        };
    }
}
