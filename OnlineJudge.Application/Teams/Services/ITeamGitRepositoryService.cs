using OnlineJudge.Application.Common;
using OnlineJudge.Application.Teams.Dtos;

namespace OnlineJudge.Application.Teams.Services;

public interface ITeamGitRepositoryService
{
    Task<Result<IReadOnlyList<TeamProjectAuditDto>>> GetProjectsAsync(Guid teamId, CancellationToken cancellationToken = default);

    Task<Result<TeamProjectAuditDto>> SyncAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default);

    Task<Result<TeamProjectGitHistoryDto>> GetHistoryAsync(Guid teamId, Guid projectId, int skip = 0, int limit = 50, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TeamGitCommitDto>>> GetCommitHistoryAsync(Guid teamId, Guid projectId, int skip = 0, int limit = 50, CancellationToken cancellationToken = default);
}
