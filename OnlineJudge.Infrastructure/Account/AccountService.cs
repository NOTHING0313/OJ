using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Account.Dtos;
using OnlineJudge.Application.Account.Requests;
using OnlineJudge.Application.Account.Services;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Requests;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Application.Sms.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Auth;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Infrastructure.Account;

public partial class AccountService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    ISmsVerificationService smsVerificationService,
    IEmailVerificationService emailVerificationService,
    PasswordHasher passwordHasher,
    ISecurityAuditWriter? auditWriter = null,
    PasswordPolicy? passwordPolicy = null) : IAccountService
{
    private const string BindPhoneScene = "BindPhone";
    private const string PasswordResetScene = "PasswordReset";
    private const string EmailPasswordResetScene = "password-reset";
    private const string AccountDeleteScene = "account-delete";
    private const string UploadImagePrefix = "/uploads/images/";
    private const string EmailPasswordResetGenericSuccess = "如果该邮箱存在，验证码将会发送。";
    private const string EmailPasswordResetGenericFailure = "验证码无效或已过期。";
    private const string PasswordResetGenericSuccess = "如果该手机号存在，验证码将会发送。";
    private const string PasswordResetGenericFailure = "验证码无效或已过期。";
    private readonly PasswordPolicy passwordPolicy = passwordPolicy ?? new PasswordPolicy();

    public async Task<Result<AccountUserDto>> GetMeAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        return Result<AccountUserDto>.Success(ToDto(userResult.Value));
    }

    public async Task<Result<AccountUserDto>> UpdateAvatarAsync(UpdateAvatarRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var avatarUrl = request.AvatarUrl.Trim();
        if (!IsAllowedUploadedImageUrl(avatarUrl))
        {
            return Result<AccountUserDto>.Failure("Avatar URL must point to uploaded images.");
        }

        var user = await dbContext.Users.FirstAsync(user => user.Id == userResult.Value.Id, cancellationToken);
        user.AvatarUrl = avatarUrl;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountUserDto>.Success(ToDto(user));
    }

    public async Task<Result<AccountUserDto>> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var userName = request.UserName.Trim();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Result<AccountUserDto>.Failure("UserName is required.");
        }

        var exists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id != userResult.Value.Id && user.UserName == userName, cancellationToken);

        if (exists)
        {
            return Result<AccountUserDto>.Failure("UserName already exists.");
        }

        var user = await dbContext.Users.FirstAsync(user => user.Id == userResult.Value.Id, cancellationToken);
        user.UserName = userName;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountUserDto>.Success(ToDto(user));
    }

    public async Task<Result<AccountUserDto>> UpdateLeaderboardAnonymityAsync(UpdateLeaderboardAnonymityRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != OnlineJudge.Domain.Enums.UserRole.Answerer)
        {
            return Result<AccountUserDto>.Failure("Forbidden.");
        }

        var user = await dbContext.Users.FirstAsync(user => user.Id == userResult.Value.Id, cancellationToken);
        user.IsLeaderboardAnonymous = request.IsLeaderboardAnonymous;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountUserDto>.Success(ToDto(user));
    }

    public async Task<Result<UserAppearanceDto>> GetAppearanceAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<UserAppearanceDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var setting = await dbContext.UserAppearanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.UserId == userResult.Value.Id, cancellationToken);

        return Result<UserAppearanceDto>.Success(setting is null ? CreateDefaultAppearance() : ToAppearanceDto(setting));
    }

    public async Task<Result<UserAppearanceDto>> UpdateAppearanceAsync(UpdateUserAppearanceRequest request, string? requestHost = null, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<UserAppearanceDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var validationError = ValidateAppearanceRequest(request);
        if (validationError is not null)
        {
            return Result<UserAppearanceDto>.Failure(validationError);
        }

        if (!TryNormalizeUploadedImagePath(request.BackgroundImageUrl, requestHost, out var normalizedImageUrl))
        {
            return Result<UserAppearanceDto>.Failure("Background image URL must point to uploaded images.");
        }

        var setting = await dbContext.UserAppearanceSettings
            .FirstOrDefaultAsync(setting => setting.UserId == userResult.Value.Id, cancellationToken);

        if (setting is null)
        {
            setting = new UserAppearanceSetting
            {
                Id = Guid.NewGuid(),
                UserId = userResult.Value.Id
            };
            dbContext.UserAppearanceSettings.Add(setting);
        }

        setting.BackgroundImageUrl = normalizedImageUrl;
        setting.BackgroundEnabled = !string.IsNullOrWhiteSpace(normalizedImageUrl) && request.BackgroundEnabled;
        setting.PositionX = request.PositionX;
        setting.PositionY = request.PositionY;
        setting.Scale = request.Scale;
        setting.OverlayOpacity = request.OverlayOpacity;
        setting.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<UserAppearanceDto>.Success(ToAppearanceDto(setting));
    }

    public async Task<Result<SmsSendResultDto>> SendBindPhoneCodeAsync(SendPhoneCodeRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<SmsSendResultDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var phoneNumberResult = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumberResult.IsFailure || phoneNumberResult.Value is null)
        {
            return Result<SmsSendResultDto>.Failure(phoneNumberResult.ErrorMessage ?? "Invalid phone number.");
        }

        if (await IsPhoneNumberUsedByOtherUserAsync(phoneNumberResult.Value, userResult.Value.Id, cancellationToken))
        {
            return Result<SmsSendResultDto>.Failure("Phone number is already bound to another account.");
        }

        return await smsVerificationService.SendCodeAsync(BindPhoneScene, phoneNumberResult.Value, cancellationToken);
    }

    public async Task<Result<AccountUserDto>> VerifyAndBindPhoneAsync(VerifyPhoneRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var phoneNumberResult = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumberResult.IsFailure || phoneNumberResult.Value is null)
        {
            return Result<AccountUserDto>.Failure(phoneNumberResult.ErrorMessage ?? "Invalid phone number.");
        }

        if (await IsPhoneNumberUsedByOtherUserAsync(phoneNumberResult.Value, userResult.Value.Id, cancellationToken))
        {
            return Result<AccountUserDto>.Failure("Phone number is already bound to another account.");
        }

        var verifyResult = await smsVerificationService.VerifyCodeAsync(BindPhoneScene, phoneNumberResult.Value, request.Code.Trim(), cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result<AccountUserDto>.Failure(verifyResult.ErrorMessage ?? "Invalid or expired verification code.");
        }

        var user = await dbContext.Users.FirstAsync(user => user.Id == userResult.Value.Id, cancellationToken);
        user.PhoneNumber = phoneNumberResult.Value;
        user.PhoneNumberConfirmed = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AccountUserDto>.Success(ToDto(user));
    }

    public async Task<Result<SmsSendResultDto>> SendPasswordResetCodeAsync(SendPasswordResetCodeRequest request, CancellationToken cancellationToken = default)
    {
        var phoneNumberResult = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumberResult.IsFailure || phoneNumberResult.Value is null)
        {
            return Result<SmsSendResultDto>.Failure(phoneNumberResult.ErrorMessage ?? "Invalid phone number.");
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.PhoneNumber == phoneNumberResult.Value && user.PhoneNumberConfirmed && !user.IsBlacklisted && !user.IsDeleted, cancellationToken);

        if (!userExists)
        {
            return Result<SmsSendResultDto>.Success(new SmsSendResultDto { Message = PasswordResetGenericSuccess });
        }

        var sendResult = await smsVerificationService.SendCodeAsync(PasswordResetScene, phoneNumberResult.Value, cancellationToken);
        if (sendResult.IsFailure || sendResult.Value is null)
        {
            return sendResult;
        }

        sendResult.Value.Message = PasswordResetGenericSuccess;
        return sendResult;
    }

    public async Task<Result> ConfirmPasswordResetAsync(ConfirmPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var phoneNumberResult = NormalizePhoneNumber(request.PhoneNumber);
        if (phoneNumberResult.IsFailure || phoneNumberResult.Value is null)
        {
            return Result.Failure(PasswordResetGenericFailure);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumberResult.Value && user.PhoneNumberConfirmed && !user.IsBlacklisted && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure(PasswordResetGenericFailure);
        }

        var verifyResult = await smsVerificationService.VerifyCodeAsync(PasswordResetScene, phoneNumberResult.Value, request.Code.Trim(), cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result.Failure(PasswordResetGenericFailure);
        }

        var passwordError = passwordPolicy.Validate(request.NewPassword, user.UserName, user.Email);
        if (passwordError is not null)
        {
            return Result.Failure(passwordError);
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.ActiveSessionId = null;
        user.ActiveSessionIssuedAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(
            SecurityAuditActions.UserPasswordReset,
            "User",
            user.Id.ToString(),
            ActorUserId: user.Id,
            ActorNameSnapshot: user.UserName));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<EmailSendResultDto>> SendEmailPasswordResetCodeAsync(SendEmailPasswordResetCodeRequest request, CancellationToken cancellationToken = default)
    {
        var emailResult = NormalizeEmail(request.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            return Result<EmailSendResultDto>.Failure("Invalid email.");
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email.ToLower() == emailResult.Value && !user.IsBlacklisted && !user.IsDeleted, cancellationToken);

        if (!userExists)
        {
            return Result<EmailSendResultDto>.Success(new EmailSendResultDto { Message = EmailPasswordResetGenericSuccess });
        }

        var sendResult = await emailVerificationService.SendCodeAsync(EmailPasswordResetScene, emailResult.Value, cancellationToken);
        if (sendResult.IsFailure || sendResult.Value is null)
        {
            return sendResult;
        }

        sendResult.Value.Message = EmailPasswordResetGenericSuccess;
        return sendResult;
    }

    public async Task<Result> ConfirmEmailPasswordResetAsync(ConfirmEmailPasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var emailResult = NormalizeEmail(request.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            return Result.Failure(EmailPasswordResetGenericFailure);
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email.ToLower() == emailResult.Value && !user.IsBlacklisted && !user.IsDeleted, cancellationToken);

        if (user is null)
        {
            return Result.Failure(EmailPasswordResetGenericFailure);
        }

        var verifyResult = await emailVerificationService.VerifyCodeAsync(EmailPasswordResetScene, emailResult.Value, request.Code.Trim(), cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result.Failure(EmailPasswordResetGenericFailure);
        }

        var passwordError = passwordPolicy.Validate(request.NewPassword, user.UserName, user.Email);
        if (passwordError is not null)
        {
            return Result.Failure(passwordError);
        }

        user.PasswordHash = passwordHasher.HashPassword(request.NewPassword);
        user.ActiveSessionId = null;
        user.ActiveSessionIssuedAt = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(
            SecurityAuditActions.UserPasswordReset,
            "User",
            user.Id.ToString(),
            ActorUserId: user.Id,
            ActorNameSnapshot: user.UserName));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<EmailSendResultDto>> SendAccountDeleteCodeAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<EmailSendResultDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var emailResult = NormalizeEmail(userResult.Value.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            return Result<EmailSendResultDto>.Failure("Current account does not have a valid email.");
        }

        var sendResult = await emailVerificationService.SendCodeAsync(AccountDeleteScene, emailResult.Value, cancellationToken);
        if (sendResult.IsFailure || sendResult.Value is null)
        {
            return sendResult;
        }

        sendResult.Value.Message = "注销验证码已发送。";
        return sendResult;
    }

    public async Task<Result> ConfirmAccountDeleteAsync(ConfirmAccountDeleteRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || !passwordHasher.VerifyPassword(request.Password, userResult.Value.PasswordHash))
        {
            return Result.Failure("Invalid current password.");
        }

        var emailResult = NormalizeEmail(userResult.Value.Email);
        if (emailResult.IsFailure || emailResult.Value is null)
        {
            return Result.Failure("Current account does not have a valid email.");
        }

        var verifyResult = await emailVerificationService.VerifyCodeAsync(AccountDeleteScene, emailResult.Value, request.Code.Trim(), cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result.Failure("Invalid or expired verification code.");
        }

        var user = await dbContext.Users.FirstAsync(user => user.Id == userResult.Value.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var shortId = user.Id.ToString("N")[..8];

        user.IsDeleted = true;
        user.DeletedAt = now;
        user.UserName = $"deleted_{shortId}";
        user.Email = $"deleted_{user.Id:N}@deleted.local";
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.AvatarUrl = null;
        user.PasswordHash = passwordHasher.HashPassword(Guid.NewGuid().ToString("N"));
        user.ActiveSessionId = null;
        user.ActiveSessionIssuedAt = null;
        user.UpdatedAt = now;

        auditWriter?.Stage(new SecurityAuditRecord(
            SecurityAuditActions.UserDeleted,
            "User",
            user.Id.ToString(),
            ActorUserId: user.Id,
            ActorNameSnapshot: userResult.Value.UserName));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<User>> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<User>.Failure("Account is blacklisted.");
        }

        if (user.IsDeleted)
        {
            return Result<User>.Failure("Account has been deleted.");
        }

        return Result<User>.Success(user);
    }

    private async Task<bool> IsPhoneNumberUsedByOtherUserAsync(string phoneNumber, Guid currentUserId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id != currentUserId && user.PhoneNumber == phoneNumber && !user.IsDeleted, cancellationToken);
    }

    private static Result<string> NormalizePhoneNumber(string phoneNumber)
    {
        var normalized = phoneNumber.Trim();
        return PhoneNumberRegex().IsMatch(normalized)
            ? Result<string>.Success(normalized)
            : Result<string>.Failure("Invalid phone number.");
    }

    private static Result<string> NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result<string>.Failure("Invalid email.");
        }

        try
        {
            var address = new MailAddress(normalized);
            return string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
                ? Result<string>.Success(normalized)
                : Result<string>.Failure("Invalid email.");
        }
        catch (FormatException)
        {
            return Result<string>.Failure("Invalid email.");
        }
    }

    private static bool IsAllowedUploadedImageUrl(string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return false;
        }

        if (avatarUrl.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase) && !avatarUrl.Contains("..", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(avatarUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && uri.AbsolutePath.StartsWith("/uploads/images/", StringComparison.OrdinalIgnoreCase)
            && uri.Host is "localhost" or "127.0.0.1";
    }

    private static string? ValidateAppearanceRequest(UpdateUserAppearanceRequest request)
    {
        if (request.PositionX is < 0 or > 100)
        {
            return "PositionX must be between 0 and 100.";
        }

        if (request.PositionY is < 0 or > 100)
        {
            return "PositionY must be between 0 and 100.";
        }

        if (request.Scale is < 0.5m or > 2.5m)
        {
            return "Scale must be between 0.5 and 2.5.";
        }

        if (request.OverlayOpacity is < 0 or > 1)
        {
            return "OverlayOpacity must be between 0 and 1.";
        }

        return null;
    }

    private static bool TryNormalizeUploadedImagePath(string? url, string? requestHost, out string? normalizedPath)
    {
        normalizedPath = null;
        var trimmed = url?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        if (IsSafeUploadPath(trimmed))
        {
            normalizedPath = trimmed;
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !IsAllowedUploadHost(uri.Host, requestHost)
            || !IsSafeUploadPath(uri.AbsolutePath))
        {
            return false;
        }

        normalizedPath = uri.AbsolutePath;
        return true;
    }

    private static bool IsAllowedUploadHost(string host, string? requestHost)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(requestHost) && host.Equals(requestHost, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSafeUploadPath(string path)
    {
        if (!path.StartsWith(UploadImagePrefix, StringComparison.OrdinalIgnoreCase)
            || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var decodedPath = Uri.UnescapeDataString(path);
        return decodedPath.StartsWith(UploadImagePrefix, StringComparison.OrdinalIgnoreCase)
            && !decodedPath.Contains("..", StringComparison.Ordinal)
            && !decodedPath.Contains('\\', StringComparison.Ordinal);
    }

    private static UserAppearanceDto CreateDefaultAppearance()
    {
        return new UserAppearanceDto
        {
            BackgroundImageUrl = null,
            BackgroundEnabled = false,
            PositionX = 50m,
            PositionY = 50m,
            Scale = 1m,
            OverlayOpacity = 0.65m
        };
    }

    private static UserAppearanceDto ToAppearanceDto(UserAppearanceSetting setting)
    {
        return new UserAppearanceDto
        {
            BackgroundImageUrl = setting.BackgroundImageUrl,
            BackgroundEnabled = setting.BackgroundEnabled,
            PositionX = setting.PositionX,
            PositionY = setting.PositionY,
            Scale = setting.Scale,
            OverlayOpacity = setting.OverlayOpacity
        };
    }

    private static AccountUserDto ToDto(User user)
    {
        return new AccountUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsBlacklisted = user.IsBlacklisted,
            IsLeaderboardAnonymous = user.IsLeaderboardAnonymous,
            PhoneNumberMasked = MaskPhoneNumber(user.PhoneNumber),
            PhoneNumberConfirmed = user.PhoneNumberConfirmed
        };
    }

    private static string? MaskPhoneNumber(string? phoneNumber)
    {
        return string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 7
            ? phoneNumber
            : $"{phoneNumber[..3]}****{phoneNumber[^4..]}";
    }

    [GeneratedRegex("^1[3-9]\\d{9}$")]
    private static partial Regex PhoneNumberRegex();
}
