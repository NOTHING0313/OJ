namespace OnlineJudge.Application.SecurityAudit;

public sealed class SecurityAuditQuery
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public string? Actor { get; set; }

    public string? Action { get; set; }

    public string? Result { get; set; }

    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
