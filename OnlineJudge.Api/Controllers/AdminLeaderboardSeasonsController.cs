using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Leaderboards.Requests;
using OnlineJudge.Application.Leaderboards.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize(Policy = "RequireProblemSetter")]
[Route("api/admin/leaderboard-seasons")]
public class AdminLeaderboardSeasonsController(ILeaderboardSeasonService seasonService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSeasons(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetSeasonsAsync(cancellationToken));

    [HttpGet("current/leaderboard")]
    public async Task<IActionResult> GetCurrentLeaderboard(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetCurrentAuditLeaderboardAsync(cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpPost]
    public async Task<IActionResult> Create(CreateLeaderboardSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.CreateSeasonAsync(request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpPut("{seasonId:guid}")]
    public async Task<IActionResult> Update(Guid seasonId, UpdateLeaderboardSeasonRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.UpdateSeasonAsync(seasonId, request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpPost("{seasonId:guid}/problems")]
    public async Task<IActionResult> AddProblem(Guid seasonId, AddLeaderboardSeasonProblemRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.AddProblemAsync(seasonId, request, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpDelete("{seasonId:guid}/problems/{problemId:guid}")]
    public async Task<IActionResult> RemoveProblem(Guid seasonId, Guid problemId, CancellationToken cancellationToken)
    {
        var result = await seasonService.RemoveProblemAsync(seasonId, problemId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [Authorize(Policy = "RequireRoot")]
    [HttpPost("{seasonId:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.FreezeSeasonAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
    [HttpPost("{seasonId:guid}/finalize")]
    public async Task<IActionResult> FinalizeSeason(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.FinalizeSeasonAsync(seasonId, cancellationToken));

    [Authorize(Policy = "RequireRoot")]
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
        "Leaderboard season not found." or "Problem not found." or "Season problem not found." => NotFound(errorMessage),
        _ => BadRequest(errorMessage)
    };
}
