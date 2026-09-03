using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Infrastructure.Verification;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Email;

public class EmailVerificationService(
    IConnectionMultiplexer redis,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private const string Channel = "email";
    private const int CodeLength = 6;
    private const int MaxDailySendCount = 10;
    private const int MaxAttemptCount = 5;
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CooldownTtl = TimeSpan.FromSeconds(60);

    public async Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var database = redis.GetDatabase();
        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
        var issuanceId = Guid.NewGuid().ToString("N");
        var issueStatus = await RedisVerificationCodeStore.TryIssueAsync(
            database,
            Channel,
            scene,
            normalizedEmail,
            issuanceId,
            HashCode(scene, normalizedEmail, code),
            MaxDailySendCount,
            GetDailyTtl(),
            CodeTtl,
            CooldownTtl);

        if (issueStatus == VerificationCodeIssueStatus.Cooldown)
        {
            return Result<EmailSendResultDto>.Failure("Please wait before requesting another verification code.");
        }

        if (issueStatus == VerificationCodeIssueStatus.DailyLimitExceeded)
        {
            return Result<EmailSendResultDto>.Failure("Daily email verification limit exceeded.");
        }

        try
        {
            await emailSender.SendVerificationCodeAsync(normalizedEmail, code, scene, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email verification code for scene {Scene}.", scene);
            await RedisVerificationCodeStore.CleanupIssuanceAsync(database, Channel, scene, normalizedEmail, issuanceId);
            return Result<EmailSendResultDto>.Failure("Email verification code could not be sent.");
        }

        return Result<EmailSendResultDto>.Success(new EmailSendResultDto
        {
            Message = "验证码已发送。",
            DebugCode = IsDevelopment() ? code : null
        });
    }

    public async Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var database = redis.GetDatabase();
        var actualHash = HashCode(scene, normalizedEmail, code);
        return await RedisVerificationCodeStore.TryConsumeAsync(database, Channel, scene, normalizedEmail, actualHash, MaxAttemptCount)
            ? Result.Success()
            : Result.Failure("Invalid or expired verification code.");
    }

    private string HashCode(string scene, string email, string code)
    {
        var secret = configuration["Email:CodeHashSecret"]
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Email:CodeHashSecret or Jwt:Secret is required.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = Encoding.UTF8.GetBytes($"{scene}:{email}:{code}");
        return Convert.ToBase64String(hmac.ComputeHash(bytes));
    }

    private bool IsDevelopment()
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static TimeSpan GetDailyTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var tomorrow = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        return tomorrow - now;
    }
}
