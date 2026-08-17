using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using OnlineJudge.Application.Auth.Dtos;
using OnlineJudge.Application.Auth.Requests;
using OnlineJudge.Application.Auth.Responses;
using OnlineJudge.Application.Auth.Services;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Email.Dtos;
using OnlineJudge.Application.Email.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Auth;

public class AuthService(
    OnlineJudgeDbContext dbContext,
    PasswordHasher passwordHasher,
    JwtTokenGenerator jwtTokenGenerator,
    IEmailVerificationService emailVerificationService) : IAuthService
{
    private const string RegisterEmailScene = "register";

    public async Task<Result<AuthUserDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var userName = request.UserName.Trim();
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<AuthUserDto>.Failure("UserName, Email and Password are required.");
        }

        var userNameExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UserName == userName, cancellationToken);

        if (userNameExists)
        {
            return Result<AuthUserDto>.Failure("UserName already exists.");
        }

        var emailExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email.ToLower() == email, cancellationToken);

        if (emailExists)
        {
            return Result<AuthUserDto>.Failure("Email already exists.");
        }

        var verifyResult = await emailVerificationService.VerifyCodeAsync(RegisterEmailScene, email, request.EmailCode.Trim(), cancellationToken);
        if (verifyResult.IsFailure)
        {
            return Result<AuthUserDto>.Failure("Invalid or expired email verification code.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            AvatarUrl = request.AvatarUrl,
            Role = UserRole.Answerer,
            IsBlacklisted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AuthUserDto>.Success(ToDto(user));
    }

    public async Task<Result<EmailSendResultDto>> SendRegisterEmailCodeAsync(SendRegisterEmailCodeRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<EmailSendResultDto>.Failure("Invalid email.");
        }

        var emailExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email.ToLower() == email, cancellationToken);

        if (emailExists)
        {
            return Result<EmailSendResultDto>.Failure("邮箱已被注册");
        }

        var result = await emailVerificationService.SendCodeAsync(RegisterEmailScene, email, cancellationToken);
        if (result.IsSuccess && result.Value is not null)
        {
            result.Value.Message = "验证码已发送。";
        }

        return result;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var account = request.Account.Trim();

        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<LoginResponse>.Failure("Account and Password are required.");
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(user => user.UserName == account || user.Email == account, cancellationToken);

        if (user is null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Result<LoginResponse>.Failure("Invalid account or password.");
        }

        if (user.IsDeleted)
        {
            return Result<LoginResponse>.Failure("Account has been deleted.");
        }

        if (user.IsBlacklisted)
        {
            return Result<LoginResponse>.Failure("Account is blacklisted.");
        }

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccessToken = jwtTokenGenerator.Generate(user),
            User = ToDto(user)
        });
    }

    public async Task<Result<AuthUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<AuthUserDto>.Failure("User not found.");
        }

        if (user.IsBlacklisted)
        {
            return Result<AuthUserDto>.Failure("Account is blacklisted.");
        }

        if (user.IsDeleted)
        {
            return Result<AuthUserDto>.Failure("Account has been deleted.");
        }

        return Result<AuthUserDto>.Success(ToDto(user));
    }

    private static AuthUserDto ToDto(User user)
    {
        return new AuthUserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsBlacklisted = user.IsBlacklisted,
            CreatedAt = user.CreatedAt
        };
    }

    private static string NormalizeEmail(string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        try
        {
            var address = new MailAddress(normalized);
            return string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase) ? normalized : string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }
}
