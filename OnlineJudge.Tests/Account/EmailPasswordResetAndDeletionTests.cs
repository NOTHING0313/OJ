using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Requests;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Application.Sms.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Account;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Account;

public class EmailPasswordResetAndDeletionTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendEmailPasswordResetCode_ForMissingEmail_ReturnsGenericSuccessWithoutSending()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", "answerer@example.test");
        var emailService = new FakeEmailVerificationService();
        var service = CreateAccountService(dbContext, userId, emailService);

        var result = await service.SendEmailPasswordResetCodeAsync(new SendEmailPasswordResetCodeRequest
        {
            Email = "missing@example.test"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("如果该邮箱存在，验证码将会发送。", result.Value!.Message);
        Assert.Equal(0, emailService.SendCount);
    }

    [Fact]
    public async Task ConfirmEmailPasswordReset_WithCorrectCode_UpdatesPasswordHash()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        var userId = SeedUser(dbContext, "answerer", "answerer@example.test", hasher.HashPassword("old-password"));
        var service = CreateAccountService(dbContext, userId, new FakeEmailVerificationService(), hasher);

        var result = await service.ConfirmEmailPasswordResetAsync(new ConfirmEmailPasswordResetRequest
        {
            Email = "answerer@example.test",
            Code = "123456",
            NewPassword = "new-password"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsSuccess);
        Assert.False(hasher.VerifyPassword("old-password", user!.PasswordHash));
        Assert.True(hasher.VerifyPassword("new-password", user.PasswordHash));
    }

    [Fact]
    public async Task ConfirmEmailPasswordReset_WithWrongCode_DoesNotUpdatePassword()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        var userId = SeedUser(dbContext, "answerer", "answerer@example.test", hasher.HashPassword("old-password"));
        var service = CreateAccountService(dbContext, userId, new FakeEmailVerificationService(), hasher);

        var result = await service.ConfirmEmailPasswordResetAsync(new ConfirmEmailPasswordResetRequest
        {
            Email = "answerer@example.test",
            Code = "000000",
            NewPassword = "new-password"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsFailure);
        Assert.Equal("验证码无效或已过期。", result.ErrorMessage);
        Assert.True(hasher.VerifyPassword("old-password", user!.PasswordHash));
    }

    [Fact]
    public async Task ConfirmAccountDelete_WithCorrectPasswordAndCode_AnonymizesUserAndKeepsSubmissions()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        var userId = SeedUser(dbContext, "answerer", "answerer@example.test", hasher.HashPassword("old-password"), "13800138000");
        dbContext.Submissions.Add(new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = Guid.NewGuid(),
            UserId = userId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "code",
            Status = JudgeStatus.Accepted,
            CreatedAt = BaseTime
        });
        await dbContext.SaveChangesAsync();

        var service = CreateAccountService(dbContext, userId, new FakeEmailVerificationService(), hasher);

        var result = await service.ConfirmAccountDeleteAsync(new ConfirmAccountDeleteRequest
        {
            Code = "123456",
            Password = "old-password"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsSuccess);
        Assert.True(user!.IsDeleted);
        Assert.NotNull(user.DeletedAt);
        Assert.StartsWith("deleted_", user.UserName);
        Assert.Equal($"deleted_{userId:N}@deleted.local", user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.Null(user.AvatarUrl);
        Assert.False(hasher.VerifyPassword("old-password", user.PasswordHash));
        Assert.Equal(1, await dbContext.Submissions.CountAsync(submission => submission.UserId == userId));
    }

    [Fact]
    public async Task ConfirmAccountDelete_WithWrongPassword_ReturnsFailure()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        var userId = SeedUser(dbContext, "answerer", "answerer@example.test", hasher.HashPassword("old-password"));
        var service = CreateAccountService(dbContext, userId, new FakeEmailVerificationService(), hasher);

        var result = await service.ConfirmAccountDeleteAsync(new ConfirmAccountDeleteRequest
        {
            Code = "123456",
            Password = "bad-password"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsFailure);
        Assert.False(user!.IsDeleted);
    }

    [Fact]
    public async Task Login_ForDeletedUser_ReturnsDeletedFailure()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        SeedUser(dbContext, "answerer", "answerer@example.test", hasher.HashPassword("old-password"), isDeleted: true);
        var authService = new AuthService(dbContext, hasher, new JwtTokenGenerator(CreateConfiguration()), new FakeEmailVerificationService());

        var result = await authService.LoginAsync(new()
        {
            Account = "answerer@example.test",
            Password = "old-password"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Account has been deleted.", result.ErrorMessage);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static AccountService CreateAccountService(OnlineJudgeDbContext dbContext, Guid currentUserId, IEmailVerificationService emailService, PasswordHasher? passwordHasher = null)
    {
        return new AccountService(dbContext, new TestCurrentUser(currentUserId), new FakeSmsVerificationService(), emailService, passwordHasher ?? new PasswordHasher());
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

    private static Guid SeedUser(
        OnlineJudgeDbContext dbContext,
        string userName,
        string email,
        string? passwordHash = null,
        string? phoneNumber = null,
        bool isDeleted = false)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = userName,
            Email = email,
            PasswordHash = passwordHash ?? "hashed-password",
            AvatarUrl = "http://localhost:5101/uploads/images/avatar.png",
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = phoneNumber is not null,
            Role = UserRole.Answerer,
            IsBlacklisted = false,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? BaseTime : null,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        dbContext.SaveChanges();
        return userId;
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;

        public string? UserName => "test-user";

        public UserRole? Role => UserRole.Answerer;
    }

    private sealed class FakeSmsVerificationService : ISmsVerificationService
    {
        public Task<Result<SmsSendResultDto>> SendCodeAsync(string scene, string phoneNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<SmsSendResultDto>.Success(new SmsSendResultDto
            {
                Message = "验证码已发送。",
                DebugCode = "123456"
            }));
        }

        public Task<Result> VerifyCodeAsync(string scene, string phoneNumber, string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(code == "123456" ? Result.Success() : Result.Failure("Invalid or expired verification code."));
        }
    }

    private sealed class FakeEmailVerificationService : IEmailVerificationService
    {
        public int SendCount { get; private set; }

        public Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.FromResult(Result<EmailSendResultDto>.Success(new EmailSendResultDto
            {
                Message = "验证码已发送。",
                DebugCode = "123456"
            }));
        }

        public Task<Result> VerifyCodeAsync(string scene, string email, string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(code == "123456" ? Result.Success() : Result.Failure("Invalid or expired verification code."));
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
