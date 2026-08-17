namespace OnlineJudge.Application.Sms.Services;

public interface ISmsSender
{
    Task SendVerificationCodeAsync(string phoneNumber, string code, string scene, CancellationToken cancellationToken = default);
}
