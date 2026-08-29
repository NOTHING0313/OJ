using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Application.Teams.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Teams;

public sealed class TeamChatService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ITeamChatService
{
    private const int PageSize = 50;
    private const int MaximumContentLength = 2000;
    private static readonly TimeSpan RecentlyEndedWindow = TimeSpan.FromDays(1);

    public async Task<Result<TeamChatPageDto>> GetMessagesAsync(
        Guid teamId,
        DateTimeOffset? beforeCreatedAt,
        Guid? beforeId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireActiveMemberAsync(teamId, cancellationToken);
        if (access.IsFailure) return Result<TeamChatPageDto>.Failure(access.ErrorMessage!);
        if (beforeCreatedAt.HasValue != beforeId.HasValue)
        {
            return Result<TeamChatPageDto>.Failure("Both chat cursor values are required.");
        }

        IQueryable<TeamChatMessage> query = dbContext.TeamChatMessages;
        if (beforeCreatedAt is { } createdAt && beforeId is { } id)
        {
            query = dbContext.Database.IsRelational()
                ? dbContext.TeamChatMessages.FromSqlInterpolated(
                    $"""
                     SELECT * FROM "TeamChatMessages"
                     WHERE "TeamId" = {teamId}
                       AND ("CreatedAt" < {createdAt} OR ("CreatedAt" = {createdAt} AND "Id" < {id}))
                     """)
                : query.Where(message => message.TeamId == teamId && (message.CreatedAt < createdAt
                    || (message.CreatedAt == createdAt && message.Id.CompareTo(id) < 0)));
        }
        else query = query.Where(message => message.TeamId == teamId);

        var rows = await query.AsNoTracking().Include(message => message.SenderUser)
            .OrderByDescending(message => message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > PageSize;
        var messages = rows.Take(PageSize).Select(ToDto).Reverse().ToList();
        return Result<TeamChatPageDto>.Success(new TeamChatPageDto { Messages = messages, HasMore = hasMore });
    }

    public async Task<Result<TeamChatMessageDto>> SendAsync(
        Guid teamId,
        SendTeamChatMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireActiveMemberAsync(teamId, cancellationToken);
        if (access.IsFailure || access.Value is null)
        {
            return Result<TeamChatMessageDto>.Failure(access.ErrorMessage!);
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) return Result<TeamChatMessageDto>.Failure("Message content is required.");
        if (content.Length > MaximumContentLength)
        {
            return Result<TeamChatMessageDto>.Failure($"Message content must not exceed {MaximumContentLength} characters.");
        }

        var message = new TeamChatMessage
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            SenderUserId = access.Value.Id,
            Type = TeamChatMessageType.User,
            Content = content,
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.TeamChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TeamChatMessageDto>.Success(ToDto(message, access.Value));
    }

    public async Task<Result<IReadOnlyList<TeamChallengeAnnouncementDto>>> GetChallengeAnnouncementsAsync(
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var access = await RequireActiveMemberAsync(teamId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<TeamChallengeAnnouncementDto>>.Failure(access.ErrorMessage!);
        }

        var now = timeProvider.GetUtcNow();
        var recentCutoff = now - RecentlyEndedWindow;
        var participants = await dbContext.ChallengeTeamParticipants.AsNoTracking()
            .Include(participant => participant.Challenge)
            .Where(participant => participant.TeamId == teamId
                && participant.Challenge!.IsPublished
                && (participant.Challenge.EndAt >= recentCutoff
                    || (participant.Challenge.PeerReviewEnabled
                        && participant.Challenge.PeerReviewEndAt >= now)))
            .OrderBy(participant => participant.Challenge!.EndAt)
            .ToListAsync(cancellationToken);

        var announcements = participants
            .Select(participant => ToAnnouncement(participant.Challenge!, now))
            .Where(announcement => announcement is not null)
            .Cast<TeamChallengeAnnouncementDto>()
            .ToList();
        return Result<IReadOnlyList<TeamChallengeAnnouncementDto>>.Success(announcements);
    }

    private async Task<Result<User>> RequireActiveMemberAsync(Guid teamId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null) return Result<User>.Failure("Unauthorized.");
        if (user.IsBlacklisted) return Result<User>.Failure("Account is blacklisted.");
        var isActiveMember = await dbContext.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.TeamId == teamId && member.UserId == userId
                && member.IsActive && !member.Team!.IsDeleted, cancellationToken);
        return isActiveMember ? Result<User>.Success(user) : Result<User>.Failure("Forbidden.");
    }

    private static TeamChallengeAnnouncementDto? ToAnnouncement(Challenge challenge, DateTimeOffset now)
    {
        string? status = null;
        if (now < challenge.StartAt) status = "scheduled";
        else if (now <= challenge.EndAt) status = "active";
        else if (challenge.PeerReviewEnabled && challenge.PeerReviewEndAt >= now) status = "peerReview";
        else if (now - challenge.EndAt <= RecentlyEndedWindow) status = "ended";
        return status is null ? null : new TeamChallengeAnnouncementDto
        {
            ChallengeId = challenge.Id,
            Title = challenge.Title,
            Status = status,
            StartAt = challenge.StartAt,
            EndAt = challenge.EndAt
        };
    }

    private static TeamChatMessageDto ToDto(TeamChatMessage message) => new()
    {
        Id = message.Id,
        Type = message.Type,
        Content = message.Content,
        Sender = message.SenderUser is null ? null : new TeamUserDto
        {
            Id = message.SenderUser.Id,
            UserName = message.SenderUser.UserName,
            AvatarUrl = message.SenderUser.AvatarUrl
        },
        RelatedChallengeId = message.RelatedChallengeId,
        RelatedPeerReviewAssignmentId = message.RelatedPeerReviewAssignmentId,
        CreatedAt = message.CreatedAt
    };

    private static TeamChatMessageDto ToDto(TeamChatMessage message, User sender)
    {
        var dto = ToDto(message);
        dto.Sender = new TeamUserDto { Id = sender.Id, UserName = sender.UserName, AvatarUrl = sender.AvatarUrl };
        return dto;
    }
}
