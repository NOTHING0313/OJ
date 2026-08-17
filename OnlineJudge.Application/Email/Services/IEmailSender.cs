namespace OnlineJudge.Application.Email.Services;

public interface IEmailSender
{
    Task SendVerificationCodeAsync(string toEmail, string code, string scene, CancellationToken cancellationToken = default);
}
