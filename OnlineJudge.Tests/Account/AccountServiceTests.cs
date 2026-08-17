using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Application.Sms.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Account;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Account;

public class AccountServiceTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UpdateAvatar_AllowsUploadedImageUrl_AndReturnsUpdatedUser()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.UpdateAvatarAsync(new UpdateAvatarRequest
        {
            AvatarUrl = "http://localhost:5101/uploads/images/avatar.png"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("http://localhost:5101/uploads/images/avatar.png", result.Value!.AvatarUrl);
        Assert.Equal(result.Value.AvatarUrl, (await dbContext.Users.FindAsync(userId))!.AvatarUrl);
    }

    [Fact]
    public async Task UpdateAvatar_RejectsExternalUrl()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.UpdateAvatarAsync(new UpdateAvatarRequest
        {
            AvatarUrl = "https://example.com/uploads/images/avatar.png"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Avatar URL must point to uploaded images.", result.ErrorMessage);
    }

    [Fact]
    public async Task User_CanReadDefaultAppearance()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.BackgroundImageUrl);
        Assert.False(result.Value.BackgroundEnabled);
        Assert.Equal(50m, result.Value.PositionX);
        Assert.Equal(50m, result.Value.PositionY);
        Assert.Equal(1m, result.Value.Scale);
        Assert.Equal(0.65m, result.Value.OverlayOpacity);
    }

    [Fact]
    public async Task User_CanUpdateOwnAppearance()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var updateResult = await service.UpdateAppearanceAsync(CreateAppearanceRequest("/uploads/images/wallpaper.png"));
        var getResult = await service.GetAppearanceAsync();

        Assert.True(updateResult.IsSuccess);
        Assert.True(getResult.IsSuccess);
        Assert.Equal("/uploads/images/wallpaper.png", getResult.Value!.BackgroundImageUrl);
        Assert.True(getResult.Value.BackgroundEnabled);
        Assert.Equal(42m, getResult.Value.PositionX);
        Assert.Equal(58m, getResult.Value.PositionY);
        Assert.Equal(1.25m, getResult.Value.Scale);
        Assert.Equal(0.7m, getResult.Value.OverlayOpacity);
        Assert.Equal(userId, (await dbContext.UserAppearanceSettings.SingleAsync()).UserId);
    }

    [Fact]
    public async Task UploadUrl_NormalizedToRelativePath()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.UpdateAppearanceAsync(CreateAppearanceRequest("http://localhost:5101/uploads/images/wallpaper.png"));

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/images/wallpaper.png", result.Value!.BackgroundImageUrl);
        Assert.Equal("/uploads/images/wallpaper.png", (await dbContext.UserAppearanceSettings.SingleAsync()).BackgroundImageUrl);
    }

    [Theory]
    [InlineData("https://example.com/a.png")]
    [InlineData("data:image/png;base64,aaa")]
    [InlineData("/uploads/images/../../appsettings.json")]
    [InlineData("/uploads/images\\test.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///uploads/images/a.png")]
    public async Task UnsafeWallpaperUrl_Rejected(string url)
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.UpdateAppearanceAsync(CreateAppearanceRequest(url));

        Assert.True(result.IsFailure);
        Assert.Equal("Background image URL must point to uploaded images.", result.ErrorMessage);
        Assert.Empty(dbContext.UserAppearanceSettings);
    }

    [Theory]
    [InlineData("positionXLow")]
    [InlineData("positionYHigh")]
    [InlineData("scaleHigh")]
    [InlineData("overlayHigh")]
    public async Task InvalidPositionOrScale_Rejected(string scenario)
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());
        var request = CreateAppearanceRequest("/uploads/images/wallpaper.png");

        switch (scenario)
        {
            case "positionXLow":
                request.PositionX = -1m;
                break;
            case "positionYHigh":
                request.PositionY = 101m;
                break;
            case "scaleHigh":
                request.Scale = 10m;
                break;
            case "overlayHigh":
                request.OverlayOpacity = 1.5m;
                break;
        }

        var result = await service.UpdateAppearanceAsync(request);

        Assert.True(result.IsFailure);
        Assert.Empty(dbContext.UserAppearanceSettings);
    }

    [Fact]
    public async Task EmptyUrl_ClearsWallpaper()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var initialResult = await service.UpdateAppearanceAsync(CreateAppearanceRequest("/uploads/images/wallpaper.png"));
        var clearRequest = CreateAppearanceRequest(string.Empty);
        clearRequest.BackgroundEnabled = true;
        var clearResult = await service.UpdateAppearanceAsync(clearRequest);

        Assert.True(initialResult.IsSuccess);
        Assert.True(clearResult.IsSuccess);
        Assert.Null(clearResult.Value!.BackgroundImageUrl);
        Assert.False(clearResult.Value.BackgroundEnabled);
        var setting = await dbContext.UserAppearanceSettings.SingleAsync();
        Assert.Null(setting.BackgroundImageUrl);
        Assert.False(setting.BackgroundEnabled);
    }

    [Fact]
    public async Task AccountUserDto_DoesNotExposePasswordOrToken()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.GetMeAsync();
        var json = JsonSerializer.Serialize(result.Value);

        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(AccountUserDto).GetProperty("PasswordHash"));
    }

    [Fact]
    public async Task SendBindPhoneCode_RejectsPhoneNumberUsedByAnotherUser()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        SeedUser(dbContext, "other", UserRole.Answerer, "13800138000");
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.SendBindPhoneCodeAsync(new SendPhoneCodeRequest
        {
            PhoneNumber = "13800138000"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Phone number is already bound to another account.", result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyAndBindPhone_UpdatesCurrentUserPhone()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService());

        var result = await service.VerifyAndBindPhoneAsync(new VerifyPhoneRequest
        {
            PhoneNumber = "13800138000",
            Code = "123456"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsSuccess);
        Assert.Equal("138****8000", result.Value!.PhoneNumberMasked);
        Assert.True(result.Value.PhoneNumberConfirmed);
        Assert.Equal("13800138000", user!.PhoneNumber);
        Assert.True(user.PhoneNumberConfirmed);
    }

    [Fact]
    public async Task ConfirmPasswordReset_UpdatesPasswordHash()
    {
        await using var dbContext = CreateDbContext();
        var hasher = new PasswordHasher();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer, "13800138000", hasher.HashPassword("old-password"));
        var service = CreateService(dbContext, userId, new FakeSmsVerificationService(), hasher);

        var result = await service.ConfirmPasswordResetAsync(new ConfirmPasswordResetRequest
        {
            PhoneNumber = "13800138000",
            Code = "123456",
            NewPassword = "new-password"
        });

        var user = await dbContext.Users.FindAsync(userId);
        Assert.True(result.IsSuccess);
        Assert.False(hasher.VerifyPassword("old-password", user!.PasswordHash));
        Assert.True(hasher.VerifyPassword("new-password", user.PasswordHash));
    }

    [Fact]
    public async Task SendPasswordResetCode_ForMissingPhone_ReturnsGenericSuccess()
    {
        await using var dbContext = CreateDbContext();
        var userId = SeedUser(dbContext, "answerer", UserRole.Answerer);
        var smsService = new FakeSmsVerificationService();
        var service = CreateService(dbContext, userId, smsService);

        var result = await service.SendPasswordResetCodeAsync(new SendPasswordResetCodeRequest
        {
            PhoneNumber = "13800138000"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("如果该手机号存在，验证码将会发送。", result.Value!.Message);
        Assert.Equal(0, smsService.SendCount);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static AccountService CreateService(OnlineJudgeDbContext dbContext, Guid currentUserId, ISmsVerificationService smsService, PasswordHasher? passwordHasher = null)
    {
        return new AccountService(dbContext, new TestCurrentUser(currentUserId), smsService, new FakeEmailVerificationService(), passwordHasher ?? new PasswordHasher());
    }

    private static UpdateUserAppearanceRequest CreateAppearanceRequest(string? backgroundImageUrl)
    {
        return new UpdateUserAppearanceRequest
        {
            BackgroundImageUrl = backgroundImageUrl,
            BackgroundEnabled = true,
            PositionX = 42m,
            PositionY = 58m,
            Scale = 1.25m,
            OverlayOpacity = 0.7m
        };
    }

    private static Guid SeedUser(OnlineJudgeDbContext dbContext, string userName, UserRole role, string? phoneNumber = null, string? passwordHash = null)
    {
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = userName,
            Email = $"{userName}@example.test",
            PasswordHash = passwordHash ?? "hashed-password",
            AvatarUrl = null,
            PhoneNumber = phoneNumber,
            PhoneNumberConfirmed = phoneNumber is not null,
            Role = role,
            IsBlacklisted = false,
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
        public int SendCount { get; private set; }

        public Task<Result<SmsSendResultDto>> SendCodeAsync(string scene, string phoneNumber, CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.FromResult(Result<SmsSendResultDto>.Success(new SmsSendResultDto
            {
                Message = "验证码已发送",
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
        public Task<Result<EmailSendResultDto>> SendCodeAsync(string scene, string email, CancellationToken cancellationToken = default)
        {
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
}
