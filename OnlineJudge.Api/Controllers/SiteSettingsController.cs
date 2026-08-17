using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController(ISiteSettingsService siteSettingsService, ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("appearance")]
    public async Task<IActionResult> GetAppearance(CancellationToken cancellationToken)
    {
        var result = await siteSettingsService.GetAppearanceAsync(cancellationToken);
        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireRoot")]
    [HttpPut("appearance")]
    public async Task<IActionResult> UpdateAppearance(UpdateSiteAppearanceRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.Role is not { } role)
        {
            return Unauthorized();
        }

        var result = await siteSettingsService.UpdateAppearanceAsync(request, userId, role, Request.Host.Host, cancellationToken);

        if (result.IsFailure)
        {
            return result.ErrorMessage switch
            {
                "Forbidden." => Forbid(),
                _ => BadRequest(result.ErrorMessage)
            };
        }

        return Ok(result.Value);
    }
}
