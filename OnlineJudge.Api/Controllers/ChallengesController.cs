using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Challenges.Services;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Route("api/challenges")]
public class ChallengesController(IChallengeService challengeService, IChallengePeerReviewService peerReviewService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetChallenges(CancellationToken cancellationToken)
    {
        var result = await challengeService.GetChallengesAsync(cancellationToken);

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetChallenge(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetChallengeAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/leaderboard")]
    public async Task<IActionResult> GetLeaderboard(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetLeaderboardAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/leaderboard/progress")]
    public async Task<IActionResult> GetLeaderboardProgress(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetLeaderboardProgressAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/leaderboard/history")]
    public async Task<IActionResult> GetLeaderboardHistory(Guid id, [FromQuery] int days = 10, CancellationToken cancellationToken = default)
    {
        var result = await challengeService.GetLeaderboardHistoryAsync(id, days, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("{id:guid}/admin-summary")]
    public async Task<IActionResult> GetAdminSummary(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetAdminSummaryAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("{challengeId:guid}/admin-summary/export/users.csv")]
    public async Task<IActionResult> ExportAdminUsersCsv(Guid challengeId, CancellationToken cancellationToken)
    {
        var result = await challengeService.ExportAdminUsersCsvAsync(challengeId, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [Authorize]
    [HttpGet("{challengeId:guid}/admin-summary/export/tasks.csv")]
    public async Task<IActionResult> ExportAdminTasksCsv(Guid challengeId, CancellationToken cancellationToken)
    {
        var result = await challengeService.ExportAdminTasksCsvAsync(challengeId, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [Authorize]
    [HttpGet("{challengeId:guid}/file-submissions/{fileSubmissionId:guid}/download")]
    public async Task<IActionResult> DownloadFileSubmission(Guid challengeId, Guid fileSubmissionId, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetFileSubmissionDownloadAsync(challengeId, fileSubmissionId, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return PhysicalFile(result.Value.FilePath, result.Value.ContentType, result.Value.DownloadFileName);
    }

    [Authorize]
    [HttpGet("{challengeId:guid}/tasks/{taskId:guid}/file-answer/me")]
    public async Task<IActionResult> GetMyFileSubmission(Guid challengeId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await challengeService.GetMyFileSubmissionAsync(challengeId, taskId, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPut("{challengeId:guid}/file-submissions/{fileSubmissionId:guid}/review")]
    public async Task<IActionResult> ReviewFileSubmission(
        Guid challengeId,
        Guid fileSubmissionId,
        ReviewChallengeFileSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.ReviewFileSubmissionAsync(challengeId, fileSubmissionId, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize]
    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> JoinChallenge(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.JoinChallengeAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize]
    [HttpPost("{id:guid}/team-registration")]
    public async Task<IActionResult> RegisterTeam(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RegisterChallengeTeamRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await challengeService.RegisterTeamAsync(id, request ?? new RegisterChallengeTeamRequest(), cancellationToken);
        if (result.IsFailure) return ToFailureResult(result.ErrorMessage);
        return Ok(result.Value);
    }

    [Authorize]
    [HttpGet("{id:guid}/peer-review")]
    public async Task<IActionResult> GetPeerReview(Guid id, CancellationToken cancellationToken)
    {
        var result = await peerReviewService.GetMyWorkspaceAsync(id, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [Authorize]
    [HttpPut("{id:guid}/peer-review/draft")]
    public async Task<IActionResult> SavePeerReviewDraft(Guid id, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await peerReviewService.SaveDraftAsync(id, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [Authorize]
    [HttpPost("{id:guid}/peer-review/submit")]
    public async Task<IActionResult> SubmitPeerReview(Guid id, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await peerReviewService.SubmitAsync(id, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [Authorize]
    [HttpGet("{id:guid}/admin-peer-reviews")]
    public async Task<IActionResult> GetAdminPeerReviews(Guid id, CancellationToken cancellationToken)
    {
        var result = await peerReviewService.GetAdminAuditAsync(id, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.ChallengeCreated, "Challenge")]
    [HttpPost]
    public async Task<IActionResult> CreateChallenge(CreateChallengeRequest request, CancellationToken cancellationToken)
    {
        var result = await challengeService.CreateChallengeAsync(request, cancellationToken);

        if (result.IsFailure || result.Value is null)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return CreatedAtAction(nameof(GetChallenge), new { id = result.Value.Id }, result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.ChallengeUpdated, "Challenge", "id")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateChallenge(Guid id, UpdateChallengeRequest request, CancellationToken cancellationToken)
    {
        var result = await challengeService.UpdateChallengeAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.ChallengeDeleted, "Challenge", "id")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteChallenge(Guid id, CancellationToken cancellationToken)
    {
        var result = await challengeService.DeleteChallengeAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> AddTask(Guid id, CreateChallengeTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await challengeService.AddTaskAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpPut("{challengeId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid challengeId, Guid taskId, UpdateChallengeTaskRequest request, CancellationToken cancellationToken)
    {
        var result = await challengeService.UpdateTaskAsync(challengeId, taskId, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [HttpDelete("{challengeId:guid}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid challengeId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await challengeService.DeleteTaskAsync(challengeId, taskId, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok();
    }

    [Authorize]
    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpPost("{challengeId:guid}/tasks/{taskId:guid}/file-answer")]
    [RequestSizeLimit(55L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 55L * 1024 * 1024)]
    public async Task<IActionResult> SubmitFileAnswer(Guid challengeId, Guid taskId, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest("File is required.");
        }

        await using var stream = file.OpenReadStream();
        var request = new SubmitChallengeTaskFileRequest
        {
            OriginalFileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            FileStream = stream
        };

        var result = await challengeService.SubmitFileAnswerAsync(challengeId, taskId, request, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpDelete("{challengeId:guid}/tasks/{taskId:guid}/file-answer/me")]
    public async Task<IActionResult> WithdrawMyFileSubmission(Guid challengeId, Guid taskId, CancellationToken cancellationToken)
    {
        var result = await challengeService.WithdrawMyFileSubmissionAsync(challengeId, taskId, cancellationToken);

        if (result.IsFailure)
        {
            return ToFailureResult(result.ErrorMessage);
        }

        return NoContent();
    }

    private IActionResult ToFailureResult(string? errorMessage)
    {
        return errorMessage switch
        {
            "Unauthorized." => Unauthorized(errorMessage),
            "Forbidden." or "Account is blacklisted." or "Challenge has ended." or "Challenge is not open." => Forbid(),
            "Challenge not found." or "Challenge task not found." or "Algorithm problem not found." or "File submission not found." or "File not found." => NotFound(errorMessage),
            _ => BadRequest(errorMessage)
        };
    }
}
