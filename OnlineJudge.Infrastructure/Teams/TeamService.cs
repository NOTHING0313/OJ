using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Application.Teams.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Teams;

public class TeamService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    TeamRepositoryUrlValidator repositoryUrlValidator) : ITeamService
{
    private const int MaximumMemberCount = 10;
    private const int MaximumProjectCount = 5;

    public async Task<Result<TeamDto?>> GetMyTeamAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<TeamDto?>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var team = await TeamDetailsQuery()
            .FirstOrDefaultAsync(team => team.Members.Any(member => member.UserId == userResult.Value.Id && member.IsActive), cancellationToken);
        return Result<TeamDto?>.Success(team is null ? null : ToTeamDto(team));
    }

    public async Task<Result<IReadOnlyList<TeamListItemDto>>> GetAllTeamsAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<TeamListItemDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (!IsAuditRole(userResult.Value.Role))
        {
            return Result<IReadOnlyList<TeamListItemDto>>.Failure("Forbidden.");
        }

        var teams = await dbContext.Teams.AsNoTracking()
            .Where(team => !team.IsDeleted)
            .OrderByDescending(team => team.CreatedAt)
            .Select(team => new TeamListItemDto
            {
                Id = team.Id,
                Name = team.Name,
                Owner = new TeamUserDto
                {
                    Id = team.OwnerUserId,
                    UserName = team.OwnerUser!.UserName,
                    AvatarUrl = team.OwnerUser.AvatarUrl
                },
                MemberCount = team.Members.Count(member => member.IsActive),
                ProjectCount = team.Projects.Count,
                CreatedAt = team.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<TeamListItemDto>>.Success(teams);
    }

    public async Task<Result<TeamDto>> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var access = await GetTeamWithReadAccessAsync(teamId, cancellationToken);
        return access.IsFailure || access.Value is null
            ? Result<TeamDto>.Failure(access.ErrorMessage ?? "Team not found.")
            : Result<TeamDto>.Success(ToTeamDto(access.Value));
    }

    public async Task<Result<TeamDto>> CreateTeamAsync(CreateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<TeamDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Answerer)
        {
            return Result<TeamDto>.Failure("Forbidden.");
        }

        var validation = ValidateTeam(request.Name, request.Description);
        if (validation.IsFailure)
        {
            return Result<TeamDto>.Failure(validation.ErrorMessage!);
        }

        var normalizedName = NormalizeName(request.Name);
        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        if (await HasActiveMembershipAsync(userResult.Value.Id, cancellationToken))
        {
            return Result<TeamDto>.Failure("User already belongs to an active team.");
        }

        if (await dbContext.Teams.AnyAsync(team => !team.IsDeleted && team.NormalizedName == normalizedName, cancellationToken))
        {
            return Result<TeamDto>.Failure("Team name already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            Description = NormalizeDescription(request.Description),
            OwnerUserId = userResult.Value.Id,
            CreatedAt = now,
            UpdatedAt = now,
            OwnerUser = userResult.Value
        };
        team.Members.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = userResult.Value.Id,
            User = userResult.Value,
            Role = TeamMemberRole.Owner,
            IsActive = true,
            JoinedAt = now
        });

        dbContext.Teams.Add(team);
        var saved = await SaveConcurrencyBoundedAsync(transaction, cancellationToken);
        return saved.IsFailure
            ? Result<TeamDto>.Failure(saved.ErrorMessage!)
            : Result<TeamDto>.Success(ToTeamDto(team));
    }

    public async Task<Result<TeamDto>> UpdateTeamAsync(Guid teamId, UpdateTeamRequest request, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result<TeamDto>.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var validation = ValidateTeam(request.Name, request.Description);
        if (validation.IsFailure)
        {
            return Result<TeamDto>.Failure(validation.ErrorMessage!);
        }

        var normalizedName = NormalizeName(request.Name);
        if (await dbContext.Teams.AnyAsync(team => team.Id != teamId && !team.IsDeleted && team.NormalizedName == normalizedName, cancellationToken))
        {
            return Result<TeamDto>.Failure("Team name already exists.");
        }

        ownerResult.Value.Name = request.Name.Trim();
        ownerResult.Value.NormalizedName = normalizedName;
        ownerResult.Value.Description = NormalizeDescription(request.Description);
        ownerResult.Value.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TeamDto>.Success(ToTeamDto(ownerResult.Value));
    }

    public async Task<Result> DissolveTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        ownerResult.Value.IsDeleted = true;
        ownerResult.Value.DeletedAt = now;
        ownerResult.Value.UpdatedAt = now;
        foreach (var member in ownerResult.Value.Members.Where(member => member.IsActive))
        {
            member.IsActive = false;
            member.LeftAt = now;
        }

        foreach (var invitation in ownerResult.Value.Invitations.Where(invitation => invitation.Status == TeamInvitationStatus.Pending))
        {
            invitation.Status = TeamInvitationStatus.Cancelled;
            invitation.RespondedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<TeamInvitationDto>> InviteAsync(Guid teamId, InviteTeamMemberRequest request, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result<TeamInvitationDto>.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var userName = request.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result<TeamInvitationDto>.Failure("Username is required.");
        }

        var invitedUser = await dbContext.Users.FirstOrDefaultAsync(user => user.UserName.ToLower() == userName.ToLower(), cancellationToken);
        if (invitedUser is null || invitedUser.IsDeleted)
        {
            return Result<TeamInvitationDto>.Failure("User not found.");
        }

        if (invitedUser.IsBlacklisted)
        {
            return Result<TeamInvitationDto>.Failure("Account is blacklisted.");
        }

        if (invitedUser.Role != UserRole.Answerer)
        {
            return Result<TeamInvitationDto>.Failure("Only answerers can join teams.");
        }

        if (invitedUser.Id == ownerResult.Value.OwnerUserId)
        {
            return Result<TeamInvitationDto>.Failure("Cannot invite yourself.");
        }

        if (await HasActiveMembershipAsync(invitedUser.Id, cancellationToken))
        {
            return Result<TeamInvitationDto>.Failure("User already belongs to an active team.");
        }

        if (ownerResult.Value.Members.Count(member => member.IsActive) >= MaximumMemberCount)
        {
            return Result<TeamInvitationDto>.Failure("Team is full.");
        }

        if (await dbContext.TeamInvitations.AnyAsync(invitation => invitation.TeamId == teamId
            && invitation.InvitedUserId == invitedUser.Id
            && invitation.Status == TeamInvitationStatus.Pending, cancellationToken))
        {
            return Result<TeamInvitationDto>.Failure("A pending invitation already exists.");
        }

        var inviter = ownerResult.Value.OwnerUser!;
        var invitation = new TeamInvitation
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Team = ownerResult.Value,
            InvitedUserId = invitedUser.Id,
            InvitedUser = invitedUser,
            InvitedByUserId = inviter.Id,
            InvitedByUser = inviter,
            Status = TeamInvitationStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.TeamInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TeamInvitationDto>.Success(ToInvitationDto(invitation));
    }

    public async Task<Result<IReadOnlyList<TeamInvitationDto>>> GetTeamInvitationsAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: false, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result<IReadOnlyList<TeamInvitationDto>>.Failure(ownerResult.ErrorMessage!);
        }

        var invitations = await InvitationQuery()
            .Where(invitation => invitation.TeamId == teamId)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<TeamInvitationDto>>.Success(invitations.Select(ToInvitationDto).ToList());
    }

    public async Task<Result<IReadOnlyList<TeamInvitationDto>>> GetMyInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<TeamInvitationDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var invitations = await InvitationQuery()
            .Where(invitation => invitation.InvitedUserId == userResult.Value.Id && invitation.Status == TeamInvitationStatus.Pending && !invitation.Team!.IsDeleted)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<TeamInvitationDto>>.Success(invitations.Select(ToInvitationDto).ToList());
    }

    public Task<Result> AcceptInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return RespondToInvitationAsync(invitationId, accept: true, cancellationToken);
    }

    public Task<Result> DeclineInvitationAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return RespondToInvitationAsync(invitationId, accept: false, cancellationToken);
    }

    public async Task<Result> CancelInvitationAsync(Guid teamId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: false, cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result.Failure(ownerResult.ErrorMessage!);
        }

        var invitation = await dbContext.TeamInvitations.FirstOrDefaultAsync(item => item.Id == invitationId && item.TeamId == teamId, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure("Invitation not found.");
        }

        if (invitation.Status != TeamInvitationStatus.Pending)
        {
            return Result.Failure("Invitation is no longer pending.");
        }

        invitation.Status = TeamInvitationStatus.Cancelled;
        invitation.RespondedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> LeaveTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var member = await dbContext.TeamMembers.Include(item => item.Team)
            .FirstOrDefaultAsync(item => item.TeamId == teamId && item.UserId == userResult.Value.Id && item.IsActive && !item.Team!.IsDeleted, cancellationToken);
        if (member is null)
        {
            return Result.Failure("Team not found.");
        }

        if (member.Role == TeamMemberRole.Owner)
        {
            return Result.Failure("Owner must transfer ownership or dissolve the team before leaving.");
        }

        DeactivateMember(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        if (userId == ownerResult.Value.OwnerUserId)
        {
            return Result.Failure("Owner cannot remove themselves.");
        }

        var member = ownerResult.Value.Members.FirstOrDefault(item => item.UserId == userId && item.IsActive);
        if (member is null)
        {
            return Result.Failure("Member not found.");
        }

        DeactivateMember(member);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> TransferOwnershipAsync(Guid teamId, TransferTeamOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var oldOwner = ownerResult.Value.Members.Single(member => member.IsActive && member.UserId == ownerResult.Value.OwnerUserId);
        var newOwner = ownerResult.Value.Members.FirstOrDefault(member => member.IsActive && member.UserId == request.UserId);
        if (newOwner is null)
        {
            return Result.Failure("Member not found.");
        }

        if (newOwner.UserId == oldOwner.UserId)
        {
            return Result.Failure("User is already the owner.");
        }

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        oldOwner.Role = TeamMemberRole.Member;
        newOwner.Role = TeamMemberRole.Owner;
        ownerResult.Value.OwnerUserId = newOwner.UserId;
        ownerResult.Value.OwnerUser = newOwner.User;
        ownerResult.Value.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await CommitAsync(transaction, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<TeamProjectDto>>> GetProjectsAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var access = await GetTeamWithReadAccessAsync(teamId, cancellationToken);
        return access.IsFailure || access.Value is null
            ? Result<IReadOnlyList<TeamProjectDto>>.Failure(access.ErrorMessage ?? "Team not found.")
            : Result<IReadOnlyList<TeamProjectDto>>.Success(access.Value.Projects.OrderBy(project => project.CreatedAt).Select(ToProjectDto).ToList());
    }

    public async Task<Result<TeamProjectDto>> CreateProjectAsync(Guid teamId, CreateTeamProjectRequest request, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result<TeamProjectDto>.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var validation = ValidateProject(request.Name, request.RepositoryUrl);
        if (validation.IsFailure || validation.Value is null)
        {
            return Result<TeamProjectDto>.Failure(validation.ErrorMessage!);
        }

        if (ownerResult.Value.Projects.Count >= MaximumProjectCount)
        {
            return Result<TeamProjectDto>.Failure("Team project limit reached.");
        }

        var normalizedName = NormalizeName(request.Name);
        if (ownerResult.Value.Projects.Any(project => project.NormalizedName == normalizedName))
        {
            return Result<TeamProjectDto>.Failure("Project name already exists in this team.");
        }

        if (ownerResult.Value.Projects.Any(project => project.NormalizedRepositoryUrl == validation.Value))
        {
            return Result<TeamProjectDto>.Failure("Repository is already bound to this team.");
        }

        var now = timeProvider.GetUtcNow();
        var project = new TeamProject
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            RepositoryUrl = validation.Value,
            NormalizedRepositoryUrl = validation.Value,
            CreatedByUserId = ownerResult.Value.OwnerUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.TeamProjects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TeamProjectDto>.Success(ToProjectDto(project));
    }

    public async Task<Result<TeamProjectDto>> UpdateProjectAsync(Guid teamId, Guid projectId, UpdateTeamProjectRequest request, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result<TeamProjectDto>.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var project = ownerResult.Value.Projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            return Result<TeamProjectDto>.Failure("Project not found.");
        }

        var validation = ValidateProject(request.Name, request.RepositoryUrl);
        if (validation.IsFailure || validation.Value is null)
        {
            return Result<TeamProjectDto>.Failure(validation.ErrorMessage!);
        }

        var normalizedName = NormalizeName(request.Name);
        if (ownerResult.Value.Projects.Any(item => item.Id != projectId && item.NormalizedName == normalizedName))
        {
            return Result<TeamProjectDto>.Failure("Project name already exists in this team.");
        }

        if (ownerResult.Value.Projects.Any(item => item.Id != projectId && item.NormalizedRepositoryUrl == validation.Value))
        {
            return Result<TeamProjectDto>.Failure("Repository is already bound to this team.");
        }

        project.Name = request.Name.Trim();
        project.NormalizedName = normalizedName;
        project.RepositoryUrl = validation.Value;
        project.NormalizedRepositoryUrl = validation.Value;
        project.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TeamProjectDto>.Success(ToProjectDto(project));
    }

    public async Task<Result> DeleteProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var ownerResult = await GetOwnedTeamAsync(teamId, includeDetails: true, cancellationToken);
        if (ownerResult.IsFailure || ownerResult.Value is null)
        {
            return Result.Failure(ownerResult.ErrorMessage ?? "Team not found.");
        }

        var project = ownerResult.Value.Projects.FirstOrDefault(item => item.Id == projectId);
        if (project is null)
        {
            return Result.Failure("Project not found.");
        }

        dbContext.TeamProjects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> RespondToInvitationAsync(Guid invitationId, bool accept, CancellationToken cancellationToken)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        await using var transaction = await BeginSerializableTransactionAsync(cancellationToken);
        var invitation = await dbContext.TeamInvitations.Include(item => item.Team)
            .FirstOrDefaultAsync(item => item.Id == invitationId && item.InvitedUserId == userResult.Value.Id, cancellationToken);
        if (invitation is null)
        {
            return Result.Failure("Invitation not found.");
        }

        if (invitation.Status != TeamInvitationStatus.Pending)
        {
            return Result.Failure("Invitation is no longer pending.");
        }

        var now = timeProvider.GetUtcNow();
        if (!accept)
        {
            invitation.Status = TeamInvitationStatus.Declined;
            invitation.RespondedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success();
        }

        if (invitation.Team is null || invitation.Team.IsDeleted)
        {
            return Result.Failure("Team not found.");
        }

        if (userResult.Value.IsBlacklisted)
        {
            return Result.Failure("Account is blacklisted.");
        }

        if (await HasActiveMembershipAsync(userResult.Value.Id, cancellationToken))
        {
            return Result.Failure("User already belongs to an active team.");
        }

        if (await dbContext.TeamMembers.CountAsync(member => member.TeamId == invitation.TeamId && member.IsActive, cancellationToken) >= MaximumMemberCount)
        {
            return Result.Failure("Team is full.");
        }

        invitation.Status = TeamInvitationStatus.Accepted;
        invitation.RespondedAt = now;
        dbContext.TeamMembers.Add(new TeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = invitation.TeamId,
            UserId = userResult.Value.Id,
            Role = TeamMemberRole.Member,
            IsActive = true,
            JoinedAt = now
        });

        return await SaveConcurrencyBoundedAsync(transaction, cancellationToken);
    }

    private async Task<Result<User>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        return user.IsBlacklisted
            ? Result<User>.Failure("Account is blacklisted.")
            : Result<User>.Success(user);
    }

    private async Task<Result<Team>> GetOwnedTeamAsync(Guid teamId, bool includeDetails, CancellationToken cancellationToken)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<Team>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        IQueryable<Team> query = includeDetails ? TeamDetailsQuery(tracking: true) : dbContext.Teams;
        var team = await query.FirstOrDefaultAsync(item => item.Id == teamId && !item.IsDeleted, cancellationToken);
        if (team is null)
        {
            return Result<Team>.Failure("Team not found.");
        }

        return team.OwnerUserId != userResult.Value.Id
            ? Result<Team>.Failure("Forbidden.")
            : Result<Team>.Success(team);
    }

    private async Task<Result<Team>> GetTeamWithReadAccessAsync(Guid teamId, CancellationToken cancellationToken)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<Team>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var team = await TeamDetailsQuery().FirstOrDefaultAsync(item => item.Id == teamId, cancellationToken);
        if (team is null)
        {
            return Result<Team>.Failure("Team not found.");
        }

        var canRead = IsAuditRole(userResult.Value.Role)
            || team.Members.Any(member => member.UserId == userResult.Value.Id && member.IsActive);
        return canRead ? Result<Team>.Success(team) : Result<Team>.Failure("Team not found.");
    }

    private IQueryable<Team> TeamDetailsQuery(bool tracking = false)
    {
        var query = dbContext.Teams
            .Where(team => !team.IsDeleted)
            .Include(team => team.OwnerUser)
            .Include(team => team.Members.Where(member => member.IsActive)).ThenInclude(member => member.User)
            .Include(team => team.Projects)
            .Include(team => team.Invitations);
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<TeamInvitation> InvitationQuery()
    {
        return dbContext.TeamInvitations.AsNoTracking()
            .Include(invitation => invitation.Team)
            .Include(invitation => invitation.InvitedUser)
            .Include(invitation => invitation.InvitedByUser);
    }

    private Task<bool> HasActiveMembershipAsync(Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.TeamMembers.AnyAsync(member => member.UserId == userId && member.IsActive, cancellationToken);
    }

    private static Result ValidateTeam(string? name, string? description)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length is < 2 or > 40)
        {
            return Result.Failure("Team name must be between 2 and 40 characters.");
        }

        return description?.Trim().Length > 500
            ? Result.Failure("Team description cannot exceed 500 characters.")
            : Result.Success();
    }

    private Result<string> ValidateProject(string? name, string? repositoryUrl)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 80)
        {
            return Result<string>.Failure("Project name must be between 1 and 80 characters.");
        }

        return repositoryUrlValidator.ValidateAndNormalize(repositoryUrl ?? string.Empty);
    }

    private async Task<Result> SaveConcurrencyBoundedAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await CommitAsync(transaction, cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            return Result.Failure("The team operation conflicted with another request. Please retry.");
        }
    }

    private async Task<IDbContextTransaction?> BeginSerializableTransactionAsync(CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
    }

    private static Task CommitAsync(IDbContextTransaction? transaction, CancellationToken cancellationToken)
    {
        return transaction?.CommitAsync(cancellationToken) ?? Task.CompletedTask;
    }

    private void DeactivateMember(TeamMember member)
    {
        member.IsActive = false;
        member.LeftAt = timeProvider.GetUtcNow();
    }

    private static bool IsAuditRole(UserRole role) => role is UserRole.ProblemSetter or UserRole.Root;
    private static string NormalizeName(string value) => value.Trim().ToUpperInvariant();
    private static string? NormalizeDescription(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TeamDto ToTeamDto(Team team)
    {
        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            Description = team.Description,
            Owner = ToUserDto(team.OwnerUser!),
            Members = team.Members.Where(member => member.IsActive).OrderByDescending(member => member.Role).ThenBy(member => member.JoinedAt).Select(ToMemberDto).ToList(),
            Projects = team.Projects.OrderBy(project => project.CreatedAt).Select(ToProjectDto).ToList(),
            CreatedAt = team.CreatedAt
        };
    }

    private static TeamMemberDto ToMemberDto(TeamMember member) => new()
    {
        Id = member.Id,
        User = ToUserDto(member.User!),
        Role = member.Role,
        JoinedAt = member.JoinedAt
    };

    private static TeamInvitationDto ToInvitationDto(TeamInvitation invitation) => new()
    {
        Id = invitation.Id,
        TeamId = invitation.TeamId,
        TeamName = invitation.Team?.Name ?? string.Empty,
        InvitedUser = ToUserDto(invitation.InvitedUser!),
        InvitedByUser = ToUserDto(invitation.InvitedByUser!),
        Status = invitation.Status,
        CreatedAt = invitation.CreatedAt,
        RespondedAt = invitation.RespondedAt
    };

    private static TeamProjectDto ToProjectDto(TeamProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        RepositoryUrl = project.RepositoryUrl,
        CreatedByUserId = project.CreatedByUserId,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt
    };

    private static TeamUserDto ToUserDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        AvatarUrl = user.AvatarUrl
    };
}
