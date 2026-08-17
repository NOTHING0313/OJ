using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Email.Services;

namespace OnlineJudge.Infrastructure.Email;

public class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendVerificationCodeAsync(string toEmail, string code, string scene, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Email:Smtp");
        var host = section["Host"];
        var userName = section["UserName"];
        var password = section["Password"];
        var fromName = section["FromName"] ?? "Online Judge";
        var enableSsl = !bool.TryParse(section["EnableSsl"], out var configuredEnableSsl) || configuredEnableSsl;
        var port = int.TryParse(section["Port"], out var configuredPort) ? configuredPort : 587;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("SMTP email sender is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(userName, fromName),
            Subject = "Online Judge 验证码",
            Body = $"你的 {scene} 验证码是：{code}。验证码 5 分钟内有效，请勿泄露给他人。",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail));

        using var smtpClient = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            Credentials = new NetworkCredential(userName, password)
        };

        cancellationToken.ThrowIfCancellationRequested();
        await smtpClient.SendMailAsync(message);
    }
}
