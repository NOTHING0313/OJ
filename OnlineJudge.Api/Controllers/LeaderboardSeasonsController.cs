using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Leaderboards.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/leaderboards/season")]
public class LeaderboardSeasonsController(ILeaderboardSeasonService seasonService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await seasonService.GetCurrentLeaderboardAsync(cancellationToken);
        return result.IsFailure ? BadRequest(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpGet("current/problems/{problemId:guid}")]
    public async Task<IActionResult> GetCurrentProblem(Guid problemId, CancellationToken cancellationToken)
    {
        var result = await seasonService.GetCurrentProblemLeaderboardAsync(problemId, cancellationToken);
        return result.IsFailure ? BadRequest(result.ErrorMessage) : Ok(result.Value);
    }
}
