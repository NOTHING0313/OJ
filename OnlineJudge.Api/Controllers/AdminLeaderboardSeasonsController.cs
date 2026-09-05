using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Leaderboards.Requests;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Api.Security;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireProblemSetter")]
[Route("api/admin/leaderboard-seasons")]
public class AdminLeaderboardSeasonsController(ILeaderboardSeasonService seasonService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSeasons(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetSeasonsAsync(cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("current/leaderboard")]
    public async Task<IActionResult> GetCurrentLeaderboard(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetCurrentAuditLeaderboardAsync(cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("users/{userId:guid}/current")]
    public async Task<IActionResult> GetUserCurrent(Guid userId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetUserCurrentPersonalAsync(userId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("users/{userId:guid}/history")]
    public async Task<IActionResult> GetUserHistory(Guid userId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetUserPersonalHistoryAsync(userId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetHistoryAsync(cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("history/{seasonId:guid}")]
    public async Task<IActionResult> GetHistory(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetHistoryAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonCreated, "LeaderboardSeason")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateLeaderboardSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.CreateSeasonAsync(request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpPut("{seasonId:guid}")]
    public async Task<IActionResult> Update(Guid seasonId, UpdateLeaderboardSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.UpdateSeasonAsync(seasonId, request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/problems")]
    public async Task<IActionResult> AddProblem(Guid seasonId, AddLeaderboardSeasonProblemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.AddProblemAsync(seasonId, request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/problems/batch")]
    public async Task<IActionResult> AddProblems(Guid seasonId, AddLeaderboardSeasonProblemsRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.AddProblemsAsync(seasonId, request, cancellationToken));

    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpPut("{seasonId:guid}/problems/{problemId:guid}/benchmarks/{language}")]
    public async Task<IActionResult> UpdateProblemBenchmark(
        Guid seasonId,
        Guid problemId,
        JudgeLanguage language,
        UpdateLeaderboardSeasonProblemBenchmarkRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.UpdateProblemBenchmarkAsync(seasonId, problemId, language, request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpDelete("{seasonId:guid}/problems/{problemId:guid}")]
    public async Task<IActionResult> RemoveProblem(Guid seasonId, Guid problemId, CancellationToken cancellationToken)
    {
        var result = await seasonService.RemoveProblemAsync(seasonId, problemId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/problems/batch-remove")]
    public async Task<IActionResult> RemoveProblems(Guid seasonId, RemoveLeaderboardSeasonProblemsRequest request, CancellationToken cancellationToken)
    {
        var result = await seasonService.RemoveProblemsAsync(seasonId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonFrozen, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.FreezeSeasonAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonPublished, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/finalize")]
    public async Task<IActionResult> FinalizeSeason(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.FinalizeSeasonAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [RiskRateLimit(RateLimitPolicies.AdminMutation)]
    [SecurityAudit(SecurityAuditActions.SeasonArchived, "LeaderboardSeason", "seasonId")]
    [HttpPost("{seasonId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.ArchiveSeasonAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpGet("{seasonId:guid}/archive")]
    public async Task<IActionResult> GetArchive(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetArchiveAsync(seasonId, cancellationToken));

    private IActionResult ToActionResult<T>(OnlineJudge.Application.Common.Result<T> result) =>
        result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);

    private IActionResult ToFailureResult(string? errorMessage) => errorMessage switch
    {
        "Unauthorized." => Unauthorized(errorMessage),
        "Forbidden." => Forbid(),
        "User not found." or "Leaderboard season history not found." or "Leaderboard season not found." or "Problem not found." or "Season problem not found." => NotFound(errorMessage),
        _ => BadRequest(errorMessage)
    };
}
