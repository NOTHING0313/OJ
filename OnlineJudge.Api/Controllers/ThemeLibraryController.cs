using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Application.SiteSettings.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireRoot")]
[Route("api/site-settings/theme-presets")]
public sealed class ThemeLibraryController(IThemeLibraryService themeLibraryService, ICurrentUser currentUser) : ControllerBase
{
    private const long MaxPackBytes = 50L * 1024 * 1024;
    private const long MaxRequestBytes = MaxPackBytes + 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role) return Unauthorized();
        return ToActionResult(await themeLibraryService.ListAsync(role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPost]
    public async Task<IActionResult> Create(CreateThemePresetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.CreateAsync(request, userId, role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPut("{presetId:guid}")]
    public async Task<IActionResult> Update(Guid presetId, UpdateThemePresetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.UpdateAsync(presetId, request, userId, role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPost("{presetId:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid presetId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.DuplicateAsync(presetId, userId, role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPatch("{presetId:guid}/name")]
    public async Task<IActionResult> Rename(Guid presetId, RenameThemePresetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.RenameAsync(presetId, request, userId, role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpDelete("{presetId:guid}")]
    public async Task<IActionResult> Delete(Guid presetId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        var result = await themeLibraryService.DeleteAsync(presetId, userId, role, cancellationToken);
        if (result.IsSuccess) return NoContent();
        return Failure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPost("{presetId:guid}/apply")]
    public async Task<IActionResult> Apply(Guid presetId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.ApplyAsync(presetId, userId, role, Request.Host.Host, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPost("default/apply")]
    public async Task<IActionResult> ApplyDefault(CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        return ToActionResult(await themeLibraryService.ApplyAsync(null, userId, role, Request.Host.Host, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpGet("{presetId:guid}/export")]
    public async Task<IActionResult> Export(Guid presetId, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role) return Unauthorized();
        var result = await themeLibraryService.ExportAsync(presetId, role, cancellationToken);
        if (result.IsFailure || result.Value is null) return Failure(result.ErrorMessage);
        return File(result.Value.Content, "application/zip", result.Value.FileName);
    }

    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpPost("import/preflight")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> PreflightImport(IFormFile? file, CancellationToken cancellationToken)
    {
        if (currentUser.Role is not { } role) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest("Theme pack is required.");
        await using var content = file.OpenReadStream();
        return ToActionResult(await themeLibraryService.PreflightImportAsync(file.FileName, file.ContentType, file.Length, content, role, cancellationToken));
    }

    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (!TryGetIdentity(out var userId, out var role)) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest("Theme pack is required.");
        await using var content = file.OpenReadStream();
        return ToActionResult(await themeLibraryService.ImportAsync(file.FileName, file.ContentType, file.Length, content, userId, role, cancellationToken));
    }

    private bool TryGetIdentity(out Guid userId, out OnlineJudge.Domain.Enums.UserRole role)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        role = currentUser.Role ?? default;
        return currentUser.UserId is not null && currentUser.Role is not null;
    }

    private IActionResult ToActionResult<T>(OnlineJudge.Application.Common.Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : Failure(result.ErrorMessage);

    private IActionResult Failure(string? message) => message switch
    {
        "Forbidden." => Forbid(),
        "Theme preset was not found." => NotFound(message),
        "Theme asset is currently referenced." => Conflict(message),
        _ => BadRequest(message)
    };
}
