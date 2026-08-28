using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Application.Teams.Services;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/teams")]
public class TeamsController(ITeamService teamService) : ControllerBase
{
    [HttpGet("my")]
    public async Task<IActionResult> GetMyTeam(CancellationToken cancellationToken)
    {
        var result = await teamService.GetMyTeamAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [Authorize(Policy = "RequireProblemSetter")]
    [HttpGet("/api/admin/teams")]
    public async Task<IActionResult> GetAllTeams(CancellationToken cancellationToken)
    {
        var result = await teamService.GetAllTeamsAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpGet("{teamId:guid}")]
    public async Task<IActionResult> GetTeam(Guid teamId, CancellationToken cancellationToken)
    {
        var result = await teamService.GetTeamAsync(teamId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTeam(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.CreateTeamAsync(request, cancellationToken);
        return result.IsFailure || result.Value is null
            ? ToFailureResult(result.ErrorMessage)
            : CreatedAtAction(nameof(GetTeam), new { teamId = result.Value.Id }, result.Value);
    }

    [HttpPut("{teamId:guid}")]
    public async Task<IActionResult> UpdateTeam(Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.UpdateTeamAsync(teamId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpDelete("{teamId:guid}")]
    public async Task<IActionResult> DissolveTeam(Guid teamId, CancellationToken cancellationToken)
    {
        var result = await teamService.DissolveTeamAsync(teamId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpPost("{teamId:guid}/invitations")]
    public async Task<IActionResult> Invite(Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.InviteAsync(teamId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpGet("{teamId:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid teamId, CancellationToken cancellationToken)
    {
        var result = await teamService.GetTeamInvitationsAsync(teamId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpGet("/api/team-invitations/my")]
    public async Task<IActionResult> GetMyInvitations(CancellationToken cancellationToken)
    {
        var result = await teamService.GetMyInvitationsAsync(cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("/api/team-invitations/{invitationId:guid}/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await teamService.AcceptInvitationAsync(invitationId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpPost("/api/team-invitations/{invitationId:guid}/decline")]
    public async Task<IActionResult> DeclineInvitation(Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await teamService.DeclineInvitationAsync(invitationId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpDelete("{teamId:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> CancelInvitation(Guid teamId, Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await teamService.CancelInvitationAsync(teamId, invitationId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpPost("{teamId:guid}/leave")]
    public async Task<IActionResult> LeaveTeam(Guid teamId, CancellationToken cancellationToken)
    {
        var result = await teamService.LeaveTeamAsync(teamId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpDelete("{teamId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await teamService.RemoveMemberAsync(teamId, userId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpPost("{teamId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(Guid teamId, TransferTeamOwnershipRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.TransferOwnershipAsync(teamId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    [HttpGet("{teamId:guid}/projects")]
    public async Task<IActionResult> GetProjects(Guid teamId, CancellationToken cancellationToken)
    {
        var result = await teamService.GetProjectsAsync(teamId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPost("{teamId:guid}/projects")]
    public async Task<IActionResult> CreateProject(Guid teamId, CreateTeamProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.CreateProjectAsync(teamId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpPut("{teamId:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid teamId, Guid projectId, UpdateTeamProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await teamService.UpdateProjectAsync(teamId, projectId, request, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : Ok(result.Value);
    }

    [HttpDelete("{teamId:guid}/projects/{projectId:guid}")]
    public async Task<IActionResult> DeleteProject(Guid teamId, Guid projectId, CancellationToken cancellationToken)
    {
        var result = await teamService.DeleteProjectAsync(teamId, projectId, cancellationToken);
        return result.IsFailure ? ToFailureResult(result.ErrorMessage) : NoContent();
    }

    private IActionResult ToFailureResult(string? errorMessage)
    {
        return errorMessage switch
        {
            "Unauthorized." => Unauthorized(errorMessage),
            "Forbidden." or "Account is blacklisted." => Forbid(),
            "Team not found." or "Invitation not found." or "Member not found." or "Project not found." or "User not found." => NotFound(errorMessage),
            "Team name already exists." or "User already belongs to an active team." or "A pending invitation already exists."
                or "Invitation is no longer pending." or "Team is full." or "The team operation conflicted with another request. Please retry." => Conflict(errorMessage),
            _ => BadRequest(errorMessage)
        };
    }
}
