using OnlineJudge.Application.Common;

namespace OnlineJudge.Application.SecurityAudit;

public interface ISecurityAuditQueryService
{
    Task<Result<SecurityAuditPageDto>> QueryAsync(SecurityAuditQuery query, CancellationToken cancellationToken = default);

    Task<Result<SecurityAuditLogDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
