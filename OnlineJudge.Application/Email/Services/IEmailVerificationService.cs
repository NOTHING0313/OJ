using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;

namespace OnlineJudge.Application.Email.Services;

public interface IEmailVerificationService
{
    Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default);

    Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default);
}
