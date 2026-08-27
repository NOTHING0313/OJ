using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Leaderboards.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/leaderboards")]
public class LeaderboardsController(ILeaderboardService leaderboardService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> GetGlobalUsers(CancellationToken cancellationToken)
    {
        var result = await leaderboardService.GetGlobalUserLeaderboardAsync(cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [HttpGet("users/history")]
    public async Task<IActionResult> GetGlobalUserHistory([FromQuery] int days = 10, CancellationToken cancellationToken = default)
    {
        var result = await leaderboardService.GetGlobalUserRankHistoryAsync(days, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }

    [HttpGet("challenges")]
    public async Task<IActionResult> GetChallenges(CancellationToken cancellationToken)
    {
        var result = await leaderboardService.GetChallengeLeaderboardIndexAsync(cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Value);
    }
}
