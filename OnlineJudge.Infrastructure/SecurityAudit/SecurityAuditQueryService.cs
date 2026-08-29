using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.SecurityAudit;

public sealed class SecurityAuditQueryService(OnlineJudgeDbContext dbContext) : ISecurityAuditQueryService
{
    public async Task<Result<SecurityAuditPageDto>> QueryAsync(SecurityAuditQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = dbContext.SecurityAuditLogs.AsNoTracking();

        if (query.From is { } from) source = source.Where(log => log.CreatedAt >= from);
        if (query.To is { } to) source = source.Where(log => log.CreatedAt <= to);
        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            var actor = query.Actor.Trim().ToLower();
            source = source.Where(log => log.ActorNameSnapshot != null && log.ActorNameSnapshot.ToLower().Contains(actor));
        }
        if (!string.IsNullOrWhiteSpace(query.Action)) source = source.Where(log => log.Action == query.Action.Trim());
        if (!string.IsNullOrWhiteSpace(query.Result)) source = source.Where(log => log.Result == query.Result.Trim());
        if (!string.IsNullOrWhiteSpace(query.TargetType)) source = source.Where(log => log.TargetType == query.TargetType.Trim());
        if (!string.IsNullOrWhiteSpace(query.TargetId)) source = source.Where(log => log.TargetId != null && log.TargetId.Contains(query.TargetId.Trim()));

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(log => new SecurityAuditLogDto
            {
                Id = log.Id, ActorUserId = log.ActorUserId, ActorNameSnapshot = log.ActorNameSnapshot, Action = log.Action,
                TargetType = log.TargetType, TargetId = log.TargetId, Result = log.Result, MetadataJson = log.MetadataJson,
                CreatedAt = log.CreatedAt, ClientIp = log.ClientIp
            }).ToListAsync(cancellationToken);
        return Result<SecurityAuditPageDto>.Success(new SecurityAuditPageDto { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    public async Task<Result<SecurityAuditLogDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.SecurityAuditLogs.AsNoTracking().Where(log => log.Id == id).Select(log => new SecurityAuditLogDto
        {
            Id = log.Id, ActorUserId = log.ActorUserId, ActorNameSnapshot = log.ActorNameSnapshot, Action = log.Action,
            TargetType = log.TargetType, TargetId = log.TargetId, Result = log.Result, MetadataJson = log.MetadataJson,
            CreatedAt = log.CreatedAt, ClientIp = log.ClientIp
        }).FirstOrDefaultAsync(cancellationToken);
        return item is null ? Result<SecurityAuditLogDto>.Failure("Security audit log not found.") : Result<SecurityAuditLogDto>.Success(item);
    }
}
