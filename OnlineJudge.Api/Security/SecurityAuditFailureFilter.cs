using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Api.Security;

public sealed class SecurityAuditFailureFilter(IServiceScopeFactory scopeFactory, ICurrentUser currentUser, ILogger<SecurityAuditFailureFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata.OfType<SecurityAuditAttribute>().SingleOrDefault();
        if (attribute is null)
        {
            await next();
            return;
        }

        ActionExecutedContext? executed = null;
        try
        {
            executed = await next();
        }
        catch
        {
            await TryWriteFailureAsync(context, attribute, SecurityAuditResults.Failed);
            throw;
        }

        var statusCode = GetStatusCode(executed.Result);
        if (statusCode >= StatusCodes.Status400BadRequest)
        {
            var result = statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                ? SecurityAuditResults.Denied
                : SecurityAuditResults.Failed;
            await TryWriteFailureAsync(context, attribute, result);
        }
    }

    private async Task TryWriteFailureAsync(ActionExecutingContext context, SecurityAuditAttribute attribute, string result)
    {
        try
        {
            var targetId = attribute.TargetRouteKey is not null && context.RouteData.Values.TryGetValue(attribute.TargetRouteKey, out var value)
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
                : null;
            await using var scope = scopeFactory.CreateAsyncScope();
            var writer = scope.ServiceProvider.GetRequiredService<ISecurityAuditWriter>();
            await writer.WriteAsync(new SecurityAuditRecord(
                attribute.Action,
                attribute.TargetType,
                targetId,
                result,
                ActorUserId: currentUser.UserId,
                ActorNameSnapshot: currentUser.UserName,
                ClientIp: context.HttpContext.Connection.RemoteIpAddress?.ToString()), context.HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to persist failed security audit. Action={Action}", attribute.Action);
        }
    }

    private static int GetStatusCode(IActionResult? result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _ => StatusCodes.Status200OK
    };
}
