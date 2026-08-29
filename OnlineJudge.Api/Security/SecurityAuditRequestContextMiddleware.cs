using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Security;

public sealed class SecurityAuditRequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SecurityAuditRequestContext auditContext)
    {
        auditContext.ClientIp = context.Connection.RemoteIpAddress?.ToString();
        await next(context);
    }
}
