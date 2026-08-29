namespace OnlineJudge.Application.SecurityAudit;

public sealed record SecurityAuditRecord(
    string Action,
    string TargetType,
    string? TargetId = null,
    string Result = SecurityAuditResults.Succeeded,
    IReadOnlyDictionary<string, string?>? Metadata = null,
    Guid? ActorUserId = null,
    string? ActorNameSnapshot = null,
    string? ClientIp = null);
