using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Sms.Services;
using OnlineJudge.Infrastructure.Verification;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Sms;

public class SmsVerificationService(
    IConnectionMultiplexer redis,
    ISmsSender smsSender,
    IConfiguration configuration,
    ILogger<SmsVerificationService>? logger = null) : ISmsVerificationService
{
    private const string Channel = "sms";
    private const int CodeLength = 6;
    private const int MaxDailySendCount = 10;
    private const int MaxAttemptCount = 5;
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CooldownTtl = TimeSpan.FromSeconds(60);

    public async Task<Result<SmsSendResultDto>> SendCodeAsync(string scene, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
        var issuanceId = Guid.NewGuid().ToString("N");
        var issueStatus = await RedisVerificationCodeStore.TryIssueAsync(
            database,
            Channel,
            scene,
            phoneNumber,
            issuanceId,
            HashCode(scene, phoneNumber, code),
            MaxDailySendCount,
            GetDailyTtl(),
            CodeTtl,
            CooldownTtl);

        if (issueStatus == VerificationCodeIssueStatus.Cooldown)
        {
            return Result<SmsSendResultDto>.Failure("Please wait before requesting another verification code.");
        }

        if (issueStatus == VerificationCodeIssueStatus.DailyLimitExceeded)
        {
            return Result<SmsSendResultDto>.Failure("Daily SMS verification limit exceeded.");
        }

        try
        {
            await smsSender.SendVerificationCodeAsync(phoneNumber, code, scene, cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to send SMS verification code for scene {Scene}.", scene);
            await RedisVerificationCodeStore.CleanupIssuanceAsync(database, Channel, scene, phoneNumber, issuanceId);
            return Result<SmsSendResultDto>.Failure("SMS verification code could not be sent.");
        }

        return Result<SmsSendResultDto>.Success(new SmsSendResultDto
        {
            Message = "验证码已发送",
            DebugCode = IsDevelopment() ? code : null
        });
    }

    public async Task<Result> VerifyCodeAsync(string scene, string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var actualHash = HashCode(scene, phoneNumber, code);
        return await RedisVerificationCodeStore.TryConsumeAsync(database, Channel, scene, phoneNumber, actualHash, MaxAttemptCount)
            ? Result.Success()
            : Result.Failure("Invalid or expired verification code.");
    }

    private string HashCode(string scene, string phoneNumber, string code)
    {
        var secret = configuration["Sms:CodeHashSecret"]
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Sms:CodeHashSecret or Jwt:Secret is required.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes($"{scene}:{phoneNumber}:{code}");
        return Convert.ToBase64String(hmac.ComputeHash(bytes));
    }

    private bool IsDevelopment()
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetDailyTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var tomorrow = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        return tomorrow - now;
    }
}
