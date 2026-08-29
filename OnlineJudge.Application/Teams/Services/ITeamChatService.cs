using OnlineJudge.Application.Common;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Requests;

namespace OnlineJudge.Application.Teams.Services;

public interface ITeamChatService
{
    Task<Result<TeamChatPageDto>> GetMessagesAsync(Guid teamId, DateTimeOffset? beforeCreatedAt, Guid? beforeId, CancellationToken cancellationToken = default);
    Task<Result<TeamChatMessageDto>> SendAsync(Guid teamId, SendTeamChatMessageRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TeamChallengeAnnouncementDto>>> GetChallengeAnnouncementsAsync(Guid teamId, CancellationToken cancellationToken = default);
}
