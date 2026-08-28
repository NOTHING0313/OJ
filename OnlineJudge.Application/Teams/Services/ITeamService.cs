using OnlineJudge.Application.Common;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Requests;

namespace OnlineJudge.Application.Teams.Services;

public interface ITeamService
{
    Task<Result<TeamDto?>> GetMyTeamAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamListItemDto>>> GetAllTeamsAsync(CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default);
    Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken = default);
    Task<Result> DissolveTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Result<TeamInvitationDto>> InviteAsync(Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamInvitationDto>>> GetTeamInvitationsAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamInvitationDto>>> GetMyInvitationsAsync(CancellationToken cancellationToken = default);
    Task<Result> AcceptInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Result> DeclineInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default);
    Task<Result> CancelInvitationAsync(Guid teamId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<Result> LeaveTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Result> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> TransferOwnershipAsync(Guid teamId, TransferTeamOwnershipRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamProjectDto>>> GetProjectsAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Result<TeamProjectDto>> CreateProjectAsync(Guid teamId, CreateTeamProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<TeamProjectDto>> UpdateProjectAsync(Guid teamId, Guid projectId, UpdateTeamProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default);
}
