using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Api.Authorization;

internal sealed class CurrentRoleAuthorizationHandler : AuthorizationHandler<CurrentRoleRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentRoleRequirement requirement)
    {
        var roleValue = context.User.FindFirstValue(AuthSessionConstants.AuthoritativeRoleClaim);
        if (Enum.TryParse<UserRole>(roleValue, out var role) && requirement.Allows(role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
