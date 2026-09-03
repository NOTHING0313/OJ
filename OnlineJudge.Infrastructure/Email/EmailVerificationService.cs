using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Email;

public class EmailVerificationService(
    IConnectionMultiplexer redis,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private const int CodeLength = 6;
    private const int MaxDailySendCount = 10;
    private const int MaxAttemptCount = 5;
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CooldownTtl = TimeSpan.FromSeconds(60);

    public async Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var database = redis.GetDatabase();
        var codeKey = GetCodeKey(scene, normalizedEmail);
        var cooldownKey = GetCooldownKey(scene, normalizedEmail);
        var dailyKey = GetDailyKey(scene, normalizedEmail);

        if (await database.KeyExistsAsync(cooldownKey))
        {
            return Result<EmailSendResultDto>.Failure("Please wait before requesting another verification code.");
        }

        var dailyCount = await database.StringIncrementAsync(dailyKey);
        if (dailyCount == 1)
        {
            await database.KeyExpireAsync(dailyKey, GetDailyTtl());
        }

        if (dailyCount > MaxDailySendCount)
        {
            return Result<EmailSendResultDto>.Failure("Daily email verification limit exceeded.");
        }

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
        var record = new EmailCodeRecord
        {
            CodeHash = HashCode(scene, normalizedEmail, code),
            AttemptCount = 0
        };

        await database.StringSetAsync(codeKey, JsonSerializer.Serialize(record), CodeTtl);
        await database.StringSetAsync(cooldownKey, "1", CooldownTtl);

        try
        {
            await emailSender.SendVerificationCodeAsync(normalizedEmail, code, scene, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email verification code for scene {Scene}.", scene);
            await database.KeyDeleteAsync(codeKey);
            await database.KeyDeleteAsync(cooldownKey);
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
        var codeKey = GetCodeKey(scene, normalizedEmail);
        var raw = await database.StringGetAsync(codeKey);

        if (raw.IsNullOrEmpty)
        {
            return Result.Failure("Invalid or expired verification code.");
        }

        EmailCodeRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<EmailCodeRecord>(raw.ToString());
        }
        catch (JsonException)
        {
            await database.KeyDeleteAsync(codeKey);
            return Result.Failure("Invalid or expired verification code.");
        }

        if (record is null || record.AttemptCount >= MaxAttemptCount)
        {
            await database.KeyDeleteAsync(codeKey);
            return Result.Failure("Invalid or expired verification code.");
        }

        var actualHash = HashCode(scene, normalizedEmail, code);
        if (!FixedTimeEquals(record.CodeHash, actualHash))
        {
            record.AttemptCount++;
            if (record.AttemptCount >= MaxAttemptCount)
            {
                await database.KeyDeleteAsync(codeKey);
            }
            else
            {
                var ttl = await database.KeyTimeToLiveAsync(codeKey) ?? CodeTtl;
                await database.StringSetAsync(codeKey, JsonSerializer.Serialize(record), ttl);
            }

            return Result.Failure("Invalid or expired verification code.");
        }

        await database.KeyDeleteAsync(codeKey);
        return Result.Success();
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

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string GetCodeKey(string scene, string email)
    {
        return $"email:code:{scene}:{email}";
    }

    private static string GetCooldownKey(string scene, string email)
    {
        return $"email:cooldown:{scene}:{email}";
    }

    private static string GetDailyKey(string scene, string email)
    {
        return $"email:daily:{scene}:{email}:{DateTimeOffset.UtcNow:yyyyMMdd}";
    }

    private static TimeSpan GetDailyTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var tomorrow = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        return tomorrow - now;
    }

    private sealed class EmailCodeRecord
    {
        public string CodeHash { get; set; } = string.Empty;

        public int AttemptCount { get; set; }
    }
}
