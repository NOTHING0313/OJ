using System.Text.Json;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.SecurityAudit;

public sealed class SecurityAuditWriter(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    SecurityAuditRequestContext requestContext,
    TimeProvider timeProvider) : ISecurityAuditWriter
{
    private static readonly HashSet<string> AllowedMetadataKeys = new(StringComparer.Ordinal)
    {
        "oldRole", "newRole", "seasonStateBefore", "seasonStateAfter", "testCaseCountDelta",
        "backgroundEnabledChanged", "panelSkinEnabledChanged", "changedAssetSlots"
    };

    public void Stage(SecurityAuditRecord record) => dbContext.SecurityAuditLogs.Add(CreateEntity(record));

    public async Task WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        Stage(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private SecurityAuditLog CreateEntity(SecurityAuditRecord record)
    {
        Validate(record);
        return new SecurityAuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = record.ActorUserId ?? currentUser.UserId,
            ActorNameSnapshot = Truncate(record.ActorNameSnapshot ?? currentUser.UserName, 100),
            Action = record.Action,
            TargetType = record.TargetType,
            TargetId = Truncate(record.TargetId, 256),
            Result = record.Result,
            MetadataJson = record.Metadata is { Count: > 0 } ? JsonSerializer.Serialize(record.Metadata) : null,
            CreatedAt = timeProvider.GetUtcNow(),
            ClientIp = Truncate(record.ClientIp ?? requestContext.ClientIp, 64)
        };
    }

    private static void Validate(SecurityAuditRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Action) || record.Action.Length > 128) throw new ArgumentException("Invalid audit action.", nameof(record));
        if (string.IsNullOrWhiteSpace(record.TargetType) || record.TargetType.Length > 64) throw new ArgumentException("Invalid audit target type.", nameof(record));
        if (record.Result is not (SecurityAuditResults.Succeeded or SecurityAuditResults.Failed or SecurityAuditResults.Denied or SecurityAuditResults.Requested))
            throw new ArgumentException("Invalid audit result.", nameof(record));

        if (record.Metadata is null) return;
        foreach (var (key, value) in record.Metadata)
        {
            if (!AllowedMetadataKeys.Contains(key)) throw new ArgumentException($"Audit metadata key '{key}' is not allowed.", nameof(record));
            if (value?.Length > 256) throw new ArgumentException($"Audit metadata value '{key}' is too long.", nameof(record));
        }
    }

    private static string? Truncate(string? value, int length) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
}
