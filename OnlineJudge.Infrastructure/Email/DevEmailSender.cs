using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Email.Services;

namespace OnlineJudge.Infrastructure.Email;

public class DevEmailSender(ILogger<DevEmailSender> logger, IConfiguration configuration) : IEmailSender
{
    public Task SendVerificationCodeAsync(string toEmail, string code, string scene, CancellationToken cancellationToken = default)
    {
        if (IsDevelopment())
        {
            logger.LogInformation("Development email verification code for {Scene} / {Email}: {Code}", scene, toEmail, code);
        }
        else
        {
            throw new InvalidOperationException("The development email sender cannot be used outside Development.");
        }

        return Task.CompletedTask;
    }

    private bool IsDevelopment()
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
