using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Application.Submissions.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
public class SubmissionsController(ISubmissionService submissionService) : ControllerBase
{
    [Authorize]
    [HttpPost("api/submissions")]
    public async Task<IActionResult> CreateSubmission(CreateSubmissionRequest request, CancellationToken cancellationToken)
    {
        var result = await submissionService.CreateSubmissionAsync(request, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GetSubmission), new { id = result.Value.Id }, result.Value);
    }

    [Authorize]
    [HttpGet("api/submissions")]
    public async Task<IActionResult> QuerySubmissions([FromQuery] SubmissionQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await submissionService.QuerySubmissionsAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("api/submissions/{id:guid}")]
    public async Task<IActionResult> GetSubmission(Guid id, CancellationToken cancellationToken)
    {
        var result = await submissionService.GetSubmissionAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("api/problems/{problemId:guid}/submissions")]
    public async Task<IActionResult> GetProblemSubmissions(Guid problemId, CancellationToken cancellationToken)
    {
        var result = await submissionService.GetProblemSubmissionsAsync(problemId, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    private IActionResult ToFailureResult(string? errorMessage)
    {
        return errorMessage switch
        {
            "Unauthorized." => Unauthorized(errorMessage),
            "Forbidden." or "Account is blacklisted." => Forbid(),
            "Problem not found." or "Submission not found." => NotFound(errorMessage),
            _ => BadRequest(errorMessage)
        };
    }
}
