using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Sms.Services;
using StackExchange.Redis;

namespace OnlineJudge.Infrastructure.Sms;

public class SmsVerificationService(
    IConnectionMultiplexer redis,
    ISmsSender smsSender,
    IConfiguration configuration) : ISmsVerificationService
{
    private const int CodeLength = 6;
    private const int MaxDailySendCount = 10;
    private const int MaxAttemptCount = 5;
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CooldownTtl = TimeSpan.FromSeconds(60);

    public async Task<Result<SmsSendResultDto>> SendCodeAsync(string scene, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var codeKey = GetCodeKey(scene, phoneNumber);
        var cooldownKey = GetCooldownKey(scene, phoneNumber);
        var dailyKey = GetDailyKey(scene, phoneNumber);

        if (await database.KeyExistsAsync(cooldownKey))
        {
            return Result<SmsSendResultDto>.Failure("Please wait before requesting another verification code.");
        }

        var dailyCount = await database.StringIncrementAsync(dailyKey);
        if (dailyCount == 1)
        {
            await database.KeyExpireAsync(dailyKey, GetDailyTtl());
        }

        if (dailyCount > MaxDailySendCount)
        {
            return Result<SmsSendResultDto>.Failure("Daily SMS verification limit exceeded.");
        }

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeLength)).ToString($"D{CodeLength}");
        var record = new SmsCodeRecord
        {
            CodeHash = HashCode(scene, phoneNumber, code),
            AttemptCount = 0
        };

        await database.StringSetAsync(codeKey, JsonSerializer.Serialize(record), CodeTtl);
        await database.StringSetAsync(cooldownKey, "1", CooldownTtl);
        await smsSender.SendVerificationCodeAsync(phoneNumber, code, scene, cancellationToken);

        return Result<SmsSendResultDto>.Success(new SmsSendResultDto
        {
            Message = "验证码已发送",
            DebugCode = IsDevelopment() ? code : null
        });
    }

    public async Task<Result> VerifyCodeAsync(string scene, string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        var database = redis.GetDatabase();
        var codeKey = GetCodeKey(scene, phoneNumber);
        var raw = await database.StringGetAsync(codeKey);

        if (raw.IsNullOrEmpty)
        {
            return Result.Failure("Invalid or expired verification code.");
        }

        SmsCodeRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<SmsCodeRecord>(raw.ToString());
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

        var actualHash = HashCode(scene, phoneNumber, code);
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

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static string GetCodeKey(string scene, string phoneNumber)
    {
        return $"sms:code:{scene}:{phoneNumber}";
    }

    private static string GetCooldownKey(string scene, string phoneNumber)
    {
        return $"sms:cooldown:{scene}:{phoneNumber}";
    }

    private static string GetDailyKey(string scene, string phoneNumber)
    {
        return $"sms:daily:{scene}:{phoneNumber}:{DateTimeOffset.UtcNow:yyyyMMdd}";
    }

    private static TimeSpan GetDailyTtl()
    {
        var now = DateTimeOffset.UtcNow;
        var tomorrow = new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
        return tomorrow - now;
    }

    private sealed class SmsCodeRecord
    {
        public string CodeHash { get; set; } = string.Empty;

        public int AttemptCount { get; set; }
    }
}
