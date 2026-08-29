using OnlineJudge.Application.Auth.Dtos;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Auth.Responses;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;

namespace OnlineJudge.Application.Auth.Services;

public interface IAuthService
{
    Task<Result<AuthUserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<Result<EmailSendResultDto>> SendRegisterEmailCodeAsync(SendRegisterEmailCodeRequest request, CancellationToken cancellationToken = default);

    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<LoginAttemptResult> LoginWithOutcomeAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    Task<Result<AuthUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
