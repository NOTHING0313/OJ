namespace OnlineJudge.Api.Security;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SecurityAuditAttribute(string action, string targetType, string? targetRouteKey = null) : Attribute
{
    public string Action { get; } = action;
    public string TargetType { get; } = targetType;
    public string? TargetRouteKey { get; } = targetRouteKey;
}
