namespace OnlineJudge.Api.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self'; worker-src 'self' blob:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public async Task InvokeAsync(HttpContext context)
    {
        Apply(context.Response.Headers);
        await next(context);
    }

    public static void Apply(IHeaderDictionary headers)
    {
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["X-Frame-Options"] = "DENY";
        headers.Remove("Strict-Transport-Security");
    }
}
