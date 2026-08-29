namespace OnlineJudge.Application.SecurityAudit;

public interface ISecurityAuditWriter
{
    void Stage(SecurityAuditRecord record);

    Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default);
}
