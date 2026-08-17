using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.JudgeWorker;

public class BackgroundCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;

    public string? UserName => null;

    public UserRole? Role => null;
}
