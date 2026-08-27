using Microsoft.AspNetCore.Authorization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Api.Authorization;

internal sealed class CurrentRoleRequirement(params UserRole[] allowedRoles) : IAuthorizationRequirement
{
    private readonly HashSet<UserRole> _allowedRoles = allowedRoles.ToHashSet();

    public bool Allows(UserRole role)
    {
        return _allowedRoles.Contains(role);
    }
}
