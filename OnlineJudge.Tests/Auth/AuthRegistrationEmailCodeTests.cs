using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Auth;

public class AuthRegistrationEmailCodeTests
{
    [Fact]
    public async Task SendRegisterEmailCode_ForAvailableEmail_SendsCode()
    {
        await using var dbContext = CreateDbContext();
        var emailService = new FakeEmailVerificationService();
        var authService = CreateAuthService(dbContext, emailService);

        var result = await authService.SendRegisterEmailCodeAsync(new SendRegisterEmailCodeRequest
        {
            Email = "new-user@example.test"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("验证码已发送。", result.Value!.Message);
        Assert.Equal(1, emailService.SendCount);
        Assert.Equal("register", emailService.LastScene);
    }

    [Fact]
    public async Task SendRegisterEmailCode_ForRegisteredEmail_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        await RegisterAsync(dbContext, "exists", "exists@example.test");
        var emailService = new FakeEmailVerificationService();
        var authService = CreateAuthService(dbContext, emailService);

        var result = await authService.SendRegisterEmailCodeAsync(new SendRegisterEmailCodeRequest
        {
            Email = "EXISTS@example.test"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("邮箱已被注册", result.ErrorMessage);
        Assert.Equal(0, emailService.SendCount);
    }

    [Fact]
    public async Task Register_WithWrongEmailCode_DoesNotCreateUser()
    {
        await using var dbContext = CreateDbContext();
        var authService = CreateAuthService(dbContext, new FakeEmailVerificationService());

        var result = await authService.RegisterAsync(new RegisterRequest
        {
            UserName = "answerer",
            Email = "answerer@example.test",
            Password = "password",
            EmailCode = "000000"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Invalid or expired email verification code.", result.ErrorMessage);
        Assert.Equal(0, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Register_WithCorrectEmailCode_CreatesUserAndDeletesCode()
    {
        await using var dbContext = CreateDbContext();
        var emailService = new FakeEmailVerificationService();
        var authService = CreateAuthService(dbContext, emailService);

        var result = await authService.RegisterAsync(new RegisterRequest
        {
            UserName = "answerer",
            Email = "Answerer@Example.Test",
            Password = "password",
            EmailCode = "123456"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("answerer@example.test", result.Value!.Email);
        Assert.True(emailService.WasVerifiedAndDeleted);
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Register_ReturnDto_DoesNotExposePasswordHash()
    {
        await using var dbContext = CreateDbContext();
        var authService = CreateAuthService(dbContext, new FakeEmailVerificationService());

        var result = await authService.RegisterAsync(new RegisterRequest
        {
            UserName = "answerer",
            Email = "answerer@example.test",
            Password = "password",
            EmailCode = "123456"
        });

        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static AuthService CreateAuthService(OnlineJudgeDbContext dbContext, IEmailVerificationService emailService)
    {
        return new AuthService(dbContext, new PasswordHasher(), new JwtTokenGenerator(CreateConfiguration()), emailService);
    }

    private static async Task RegisterAsync(OnlineJudgeDbContext dbContext, string userName, string email)
    {
        dbContext.Users.Add(new()
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static IConfiguration CreateConfiguration()
    {
        return new TestConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "OnlineJudge.Tests",
            ["Jwt:Audience"] = "OnlineJudge.Tests",
            ["Jwt:Secret"] = "OnlineJudgeTestsJwtSecretForLocalOnly1234567890",
            ["Jwt:ExpireMinutes"] = "120"
        });
    }

    private sealed class FakeEmailVerificationService : IEmailVerificationService
    {
        public int SendCount { get; private set; }

        public string? LastScene { get; private set; }

        public bool WasVerifiedAndDeleted { get; private set; }

        public Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default)
        {
            SendCount++;
            LastScene = scene;
            return Task.FromResult(Result<EmailSendResultDto>.Success(new EmailSendResultDto
            {
                Message = "验证码已发送。",
                DebugCode = "123456"
            }));
        }

        public Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default)
        {
            LastScene = scene;
            if (code != "123456")
            {
                return Task.FromResult(Result.Failure("Invalid or expired verification code."));
            }

            WasVerifiedAndDeleted = true;
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class TestConfiguration(Dictionary<string, string?> values) : IConfiguration
    {
        public string? this[string key]
        {
            get => values.TryGetValue(key, out var value) ? value : null;
            set => values[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return [];
        }

        public IChangeToken GetReloadToken()
        {
            return new StaticChangeToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            return new TestConfigurationSection(key, this[key]);
        }
    }

    private sealed class TestConfigurationSection(string key, string? value) : IConfigurationSection
    {
        public string? this[string key]
        {
            get => null;
            set { }
        }

        public string Key => key;

        public string Path => key;

        public string? Value { get; set; } = value;

        public IEnumerable<IConfigurationSection> GetChildren()
        {
            return [];
        }

        public IChangeToken GetReloadToken()
        {
            return new StaticChangeToken();
        }

        public IConfigurationSection GetSection(string key)
        {
            return new TestConfigurationSection(key, null);
        }
    }

    private sealed class StaticChangeToken : IChangeToken
    {
        public bool HasChanged => false;

        public bool ActiveChangeCallbacks => false;

        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            return new NoopDisposable();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
