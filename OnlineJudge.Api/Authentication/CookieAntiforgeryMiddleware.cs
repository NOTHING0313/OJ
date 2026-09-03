using Microsoft.AspNetCore.Antiforgery;

namespace OnlineJudge.Api.Authentication;

public sealed class CookieAntiforgeryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (RequiresValidation(context))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    errorCode = "CSRF_VALIDATION_FAILED",
                    message = "CSRF token is invalid or missing."
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && context.Items.ContainsKey(BrowserSessionConstants.CookieAuthenticationItem)
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method)
        && !HttpMethods.IsTrace(context.Request.Method);
}
