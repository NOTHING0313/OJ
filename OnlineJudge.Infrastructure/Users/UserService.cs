using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Auth.Dtos;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Users.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Infrastructure.Users;

public class UserService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, ISecurityAuditWriter? auditWriter = null) : IUserService
{
    public async Task<Result<PagedResult<AuthUserDto>>> GetUsersAsync(string? keyword, UserRole? role, bool? isBlacklisted, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<PagedResult<AuthUserDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Root)
        {
            return Result<PagedResult<AuthUserDto>>.Failure("Forbidden.");
        }

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(user => EF.Functions.ILike(user.UserName, pattern) || EF.Functions.ILike(user.Email, pattern));
        }

        if (role is not null)
        {
            query = query.Where(user => user.Role == role.Value);
        }

        if (isBlacklisted is not null)
        {
            query = query.Where(user => user.IsBlacklisted == isBlacklisted);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(user => new AuthUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                IsBlacklisted = user.IsBlacklisted,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<PagedResult<AuthUserDto>>.Success(new PagedResult<AuthUserDto>
        {
            Items = users,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        });
    }

    public async Task<Result<IReadOnlyList<AuthUserDto>>> GetProblemSettersAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<AuthUserDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role is not (UserRole.ProblemSetter or UserRole.Root))
        {
            return Result<IReadOnlyList<AuthUserDto>>.Failure("Forbidden.");
        }

        var query = dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.ProblemSetter);

        if (userResult.Value.Role == UserRole.ProblemSetter)
        {
            query = query.Where(user => !user.IsBlacklisted);
        }

        var users = await query
            .OrderBy(user => user.UserName)
            .Select(user => new AuthUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                IsBlacklisted = user.IsBlacklisted,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AuthUserDto>>.Success(users);
    }

    public async Task<Result<AuthUserDto>> PromoteToProblemSetterAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AuthUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Root)
        {
            return Result<AuthUserDto>.Failure("Forbidden.");
        }

        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (targetUser is null)
        {
            return Result<AuthUserDto>.Failure("User not found.");
        }

        if (targetUser.Role != UserRole.Answerer)
        {
            return Result<AuthUserDto>.Failure("Only Answerer can be promoted to ProblemSetter.");
        }

        targetUser.Role = UserRole.ProblemSetter;
        targetUser.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.UserRoleChanged, "User", targetUser.Id.ToString(), Metadata: new Dictionary<string, string?>
        {
            ["oldRole"] = UserRole.Answerer.ToString(), ["newRole"] = UserRole.ProblemSetter.ToString()
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AuthUserDto>.Success(ToDto(targetUser));
    }

    public async Task<Result<AuthUserDto>> DemoteToAnswererAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<AuthUserDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Root)
        {
            return Result<AuthUserDto>.Failure("Forbidden.");
        }

        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (targetUser is null)
        {
            return Result<AuthUserDto>.Failure("User not found.");
        }

        if (targetUser.Role == UserRole.Root)
        {
            return Result<AuthUserDto>.Failure("Root cannot be demoted.");
        }

        if (targetUser.Role != UserRole.ProblemSetter)
        {
            return Result<AuthUserDto>.Failure("Only ProblemSetter can be demoted to Answerer.");
        }

        targetUser.Role = UserRole.Answerer;
        targetUser.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.UserRoleChanged, "User", targetUser.Id.ToString(), Metadata: new Dictionary<string, string?>
        {
            ["oldRole"] = UserRole.ProblemSetter.ToString(), ["newRole"] = UserRole.Answerer.ToString()
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AuthUserDto>.Success(ToDto(targetUser));
    }

    public async Task<Result> BlacklistAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (targetUser is null)
        {
            return Result.Failure("User not found.");
        }

        if (!CanBlacklist(userResult.Value, targetUser))
        {
            return Result.Failure("Forbidden.");
        }

        targetUser.IsBlacklisted = true;
        targetUser.ActiveSessionId = null;
        targetUser.ActiveSessionIssuedAt = null;
        targetUser.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.UserBlacklisted, "User", targetUser.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> UnblacklistAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Root)
        {
            return Result.Failure("Forbidden.");
        }

        var targetUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (targetUser is null)
        {
            return Result.Failure("User not found.");
        }

        targetUser.IsBlacklisted = false;
        targetUser.UpdatedAt = DateTimeOffset.UtcNow;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.UserUnblacklisted, "User", targetUser.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<Result<User>> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<User>.Failure("Account is blacklisted.");
        }

        return Result<User>.Success(user);
    }

    private static bool CanBlacklist(User currentUser, User targetUser)
    {
        return currentUser.Role switch
        {
            UserRole.Root => targetUser.Role is UserRole.Answerer or UserRole.ProblemSetter,
            UserRole.ProblemSetter => targetUser.Role == UserRole.Answerer,
            _ => false
        };
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
}
