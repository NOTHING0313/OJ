using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.HelpDocuments.Requests;
using OnlineJudge.Application.HelpDocuments.Services;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireProblemSetter")]
[Route("api/admin/help-documents")]
public sealed class AdminHelpDocumentsController(IHelpDocumentService helpDocumentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.GetAllAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.HelpMutation)]
    [SecurityAudit(SecurityAuditActions.HelpCreated, "HelpDocument")]
    [HttpPost]
    public async Task<IActionResult> Create(UpsertHelpDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.CreateAsync(request, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : ToFailure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.HelpMutation)]
    [SecurityAudit(SecurityAuditActions.HelpUpdated, "HelpDocument", "id")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpsertHelpDocumentRequest request, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.UpdateAsync(id, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.HelpMutation)]
    [SecurityAudit(SecurityAuditActions.HelpPublished, "HelpDocument", "id")]
    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.PublishAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.HelpMutation)]
    [SecurityAudit(SecurityAuditActions.HelpUnpublished, "HelpDocument", "id")]
    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.UnpublishAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [RiskRateLimit(RateLimitPolicies.HelpMutation)]
    [SecurityAudit(SecurityAuditActions.HelpDeleted, "HelpDocument", "id")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : ToFailure(result.ErrorMessage);
    }

    private IActionResult ToFailure(string? error) => error switch
    {
        "Unauthorized." => Unauthorized(error),
        "Forbidden." => Forbid(),
        "Help document not found." => NotFound(error),
        "Slug already exists." => Conflict(error),
        _ => BadRequest(error)
    };
}
