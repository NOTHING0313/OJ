namespace OnlineJudge.Api.Authentication;

public static class AuthSessionConstants
{
    public const string SessionIdClaim = "sid";
    public const string AuthoritativeRoleClaim = "oj:authoritative_role";
    public const string SessionInvalid = "AUTH_SESSION_INVALID";
    public const string SessionReplaced = "AUTH_SESSION_REPLACED";
    public const string TokenExpired = "AUTH_TOKEN_EXPIRED";
    internal const string ErrorCodeItem = "OnlineJudge.Auth.ErrorCode";
}
