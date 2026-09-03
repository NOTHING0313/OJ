using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Sms.Services;

namespace OnlineJudge.Infrastructure.Sms;

public class DevSmsSender(ILogger<DevSmsSender> logger, IConfiguration configuration) : ISmsSender
{
    public Task SendVerificationCodeAsync(string phoneNumber, string code, string scene, CancellationToken cancellationToken = default)
    {
        if (IsDevelopment())
        {
            logger.LogInformation("Development SMS verification code for {Scene} / {PhoneNumber}: {Code}", scene, phoneNumber, code);
        }
        else
        {
            throw new InvalidOperationException("The development SMS sender cannot be used outside Development.");
        }

        return Task.CompletedTask;
    }

    private bool IsDevelopment()
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
