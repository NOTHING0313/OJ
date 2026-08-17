using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Common;

namespace OnlineJudge.Application.Sms.Services;

public interface ISmsVerificationService
{
    Task<Result<SmsSendResultDto>> SendCodeAsync(string scene, string phoneNumber, CancellationToken cancellationToken = default);

    Task<Result> VerifyCodeAsync(string scene, string phoneNumber, string code, CancellationToken cancellationToken = default);
}
