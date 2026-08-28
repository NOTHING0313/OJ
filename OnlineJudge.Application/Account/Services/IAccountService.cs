using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Requests;

namespace OnlineJudge.Application.Account.Services;

public interface IAccountService
{
    Task<Result<AccountUserDto>> GetMeAsync(CancellationToken cancellationToken = default);

    Task<Result<AccountUserDto>> UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken cancellationToken = default);

    Task<Result<AccountUserDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);

    Task<Result<AccountUserDto>> UpdateLeaderboardAnonymityAsync(UpdateLeaderboardAnonymityRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserAppearanceDto>> GetAppearanceAsync(CancellationToken cancellationToken = default);

    Task<Result<UserAppearanceDto>> UpdateAppearanceAsync(UpdateUserAppearanceRequest request, string? requestHost = null, CancellationToken cancellationToken = default);

    Task<Result<SmsSendResultDto>> SendBindPhoneCodeAsync(SendPhoneCodeRequest request, CancellationToken cancellationToken = default);

    Task<Result<AccountUserDto>> VerifyAndBindPhoneAsync(VerifyPhoneRequest request, CancellationToken cancellationToken = default);

    Task<Result<SmsSendResultDto>> SendPasswordResetCodeAsync(SendPasswordResetCodeRequest request, CancellationToken cancellationToken = default);

    Task<Result> ConfirmPasswordResetAsync(ConfirmPasswordResetRequest request, CancellationToken cancellationToken = default);

    Task<Result<EmailSendResultDto>> SendEmailPasswordResetCodeAsync(SendEmailPasswordResetCodeRequest request, CancellationToken cancellationToken = default);

    Task<Result> ConfirmEmailPasswordResetAsync(ConfirmEmailPasswordResetRequest request, CancellationToken cancellationToken = default);

    Task<Result<EmailSendResultDto>> SendAccountDeleteCodeAsync(CancellationToken cancellationToken = default);

    Task<Result> ConfirmAccountDeleteAsync(ConfirmAccountDeleteRequest request, CancellationToken cancellationToken = default);
}
