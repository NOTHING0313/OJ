using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireRoot")]
[Route("api/admin/security-audit")]
public sealed class SecurityAuditController(ISecurityAuditQueryService auditQueryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] SecurityAuditQuery query, CancellationToken cancellationToken)
    {
        var result = await auditQueryService.QueryAsync(query, cancellationToken);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await auditQueryService.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.ErrorMessage);
    }
}
