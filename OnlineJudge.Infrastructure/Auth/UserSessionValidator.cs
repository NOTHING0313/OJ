using Microsoft.EntityFrameworkCore;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Auth;

public enum UserSessionValidationStatus
{
    Valid,
    Invalid,
    Replaced
}

public sealed record UserSessionValidationResult(UserSessionValidationStatus Status, UserRole? Role = null);

public sealed class UserSessionValidator(OnlineJudgeDbContext dbContext)
{
    public async Task<UserSessionValidationResult> ValidateAsync(Guid userId, Guid? sessionId, CancellationToken cancellationToken = default)
    {
        if (sessionId is null)
        {
            return new UserSessionValidationResult(UserSessionValidationStatus.Invalid);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Role,
                user.IsBlacklisted,
                user.IsDeleted,
                user.ActiveSessionId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || user.IsDeleted || user.IsBlacklisted || user.ActiveSessionId is null)
        {
            return new UserSessionValidationResult(UserSessionValidationStatus.Invalid);
        }

        return user.ActiveSessionId == sessionId
            ? new UserSessionValidationResult(UserSessionValidationStatus.Valid, user.Role)
            : new UserSessionValidationResult(UserSessionValidationStatus.Replaced);
    }
}
