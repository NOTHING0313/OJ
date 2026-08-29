using OnlineJudge.Application.Common;

namespace OnlineJudge.Application.Auth.Responses;

public enum LoginFailureKind
{
    None,
    InvalidPassword,
    Other
}

public sealed record LoginAttemptResult(Result<LoginResponse> Result, LoginFailureKind FailureKind);
