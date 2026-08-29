using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Leaderboards.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Route("api/leaderboard-seasons")]
public class LeaderboardSeasonHistoryController(ILeaderboardSeasonService seasonService) : ControllerBase
{
    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetHistoryAsync(cancellationToken));

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("history/{seasonId:guid}")]
    public async Task<IActionResult> GetHistory(Guid seasonId, CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetHistoryAsync(seasonId, cancellationToken));

    [Authorize]
    [HttpGet("current/me")]
    public async Task<IActionResult> GetCurrentPersonal(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetCurrentPersonalAsync(cancellationToken));

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("me/history")]
    public async Task<IActionResult> GetPersonalHistory(CancellationToken cancellationToken) =>
        ToActionResult(await seasonService.GetPersonalHistoryAsync(cancellationToken));

    private IActionResult ToActionResult<T>(OnlineJudge.Application.Common.Result<T> result) => result.ErrorMessage switch
    {
        null => Ok(result.Value),
        "Unauthorized." => Unauthorized(result.ErrorMessage),
        "Forbidden." => Forbid(),
        "Leaderboard season history not found." => NotFound(result.ErrorMessage),
        _ => BadRequest(result.ErrorMessage)
    };
}
