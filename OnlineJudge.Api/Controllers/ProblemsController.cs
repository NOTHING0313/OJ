using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Problems.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Route("api/problems")]
public class ProblemsController(IProblemService problemService) : ControllerBase
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetProblems(CancellationToken cancellationToken)
    {
        var result = await problemService.GetProblemsAsync(cancellationToken);

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProblem(Guid id, CancellationToken cancellationToken)
    {
        var result = await problemService.GetProblemAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpPost]
    public async Task<IActionResult> CreateProblem(CreateProblemRequest request, CancellationToken cancellationToken)
    {
        var result = await problemService.CreateProblemAsync(request, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GetProblem), new { id = result.Value.Id }, result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProblem(Guid id, UpdateProblemRequest request, CancellationToken cancellationToken)
    {
        var result = await problemService.UpdateProblemAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProblem(Guid id, CancellationToken cancellationToken)
    {
        var result = await problemService.DeleteProblemAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return NoContent();
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpPost("{id:guid}/test-cases")]
    public async Task<IActionResult> AddTestCase(Guid id, CreateTestCaseRequest request, CancellationToken cancellationToken)
    {
        var result = await problemService.AddTestCaseAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpPost("{id:guid}/test-cases/import")]
    public async Task<IActionResult> ImportTestCases(Guid id, ImportTestCasesRequest request, CancellationToken cancellationToken)
    {
        var result = await problemService.ImportTestCasesAsync(id, request, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        if (result.Value.Errors.Count > 0)
        {
            return BadRequest(result.Value);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("{id:guid}/test-cases/export")]
    public async Task<IActionResult> ExportTestCases(Guid id, CancellationToken cancellationToken)
    {
        var result = await problemService.ExportTestCasesAsync(id, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        var json = JsonSerializer.Serialize(result.Value, ExportJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"problem-{id}-test-cases.json");
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("{id:guid}/collaborators")]
    public async Task<IActionResult> GetCollaborators(Guid id, CancellationToken cancellationToken)
    {
        var result = await problemService.GetCollaboratorsAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpPost("{id:guid}/collaborators")]
    public async Task<IActionResult> GrantCollaborator(Guid id, GrantProblemCollaboratorRequest request, CancellationToken cancellationToken)
    {
        var result = await problemService.GrantCollaboratorAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpDelete("{id:guid}/collaborators/{userId:guid}")]
    public async Task<IActionResult> RemoveCollaborator(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var result = await problemService.RemoveCollaboratorAsync(id, userId, cancellationToken);

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
            "Problem not found." => NotFound(errorMessage),
            "User not found." or "Collaborator not found." => NotFound(errorMessage),
            "该题目已被挑战任务引用，请先移除相关挑战任务后再删除。" => Conflict(errorMessage),
            _ => BadRequest(errorMessage)
        };
    }
}
