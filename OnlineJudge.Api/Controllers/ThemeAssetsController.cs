using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireRoot")]
[Route("api/site-settings/theme-assets")]
public sealed class ThemeAssetsController(IThemeAssetService themeAssetService, ICurrentUser currentUser) : ControllerBase
{
    private const long MaxFileSize = 5L * 1024 * 1024;
    private const long MaxRequestSize = MaxFileSize + 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role)
        {
            return Unauthorized();
        }

        var result = await themeAssetService.ListAsync(role, cancellationToken);
        if (result.IsFailure)
        {
            return result.ErrorMessage == "Forbidden." ? Forbid() : BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSize)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        await using var content = file.OpenReadStream();
        var result = await themeAssetService.UploadAsync(userId, role, file.FileName, file.ContentType, file.Length, content, cancellationToken);
        if (result.IsFailure)
        {
            return result.ErrorMessage == "Forbidden." ? Forbid() : BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPatch("{assetId}/name")]
    public async Task<IActionResult> Rename(string assetId, RenameThemeAssetRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await themeAssetService.RenameAsync(userId, role, assetId, request.DisplayName, cancellationToken);
        if (result.IsFailure)
        {
            return result.ErrorMessage == "Forbidden." ? Forbid() : BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpDelete("{assetId}")]
    public async Task<IActionResult> Delete(string assetId, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role || currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await themeAssetService.DeleteAsync(userId, role, assetId, cancellationToken);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return result.ErrorMessage switch
        {
            "Forbidden." => Forbid(),
            "Theme asset is currently referenced." => Conflict(result.ErrorMessage),
            _ => BadRequest(result.ErrorMessage)
        };
    }
}
