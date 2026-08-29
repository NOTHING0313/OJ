namespace OnlineJudge.Application.SecurityAudit;

public sealed class SecurityAuditLogDto
{
    public Guid Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorNameSnapshot { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? ClientIp { get; set; }
}

public sealed class SecurityAuditPageDto
{
    public IReadOnlyList<SecurityAuditLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
