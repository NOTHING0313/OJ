using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Common.CurrentUser;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    string? UserName { get; }

    UserRole? Role { get; }
}
