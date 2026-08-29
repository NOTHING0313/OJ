using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.HelpDocuments.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/help-documents")]
public sealed class HelpDocumentsController(IHelpDocumentService helpDocumentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPublished(CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.GetPublishedAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetPublishedBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await helpDocumentService.GetPublishedBySlugAsync(slug, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ToFailure(result.ErrorMessage);
    }

    private IActionResult ToFailure(string? error) => error switch
    {
        "Unauthorized." => Unauthorized(error),
        "Forbidden." => Forbid(),
        "Help document not found." => NotFound(error),
        _ => BadRequest(error)
    };
}
