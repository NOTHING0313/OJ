namespace OnlineJudge.Api.Authentication;

public static class BrowserSessionConstants
{
    public const string SessionCookieName = "__Host-OnlineJudge.Session";
    public const string AntiforgeryCookieName = "__Host-OnlineJudge.Antiforgery";
    public const string CsrfCookieName = "__Host-OnlineJudge.Csrf";
    public const string CsrfHeaderName = "X-CSRF-TOKEN";
    internal const string CookieAuthenticationItem = "OnlineJudge.Auth.Cookie";
}
