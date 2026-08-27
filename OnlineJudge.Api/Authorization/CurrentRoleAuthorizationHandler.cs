using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Api.Authorization;

internal sealed class CurrentRoleAuthorizationHandler(OnlineJudgeDbContext dbContext) : AuthorizationHandler<CurrentRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CurrentRoleRequirement requirement)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBlacklisted)
            .Select(user => new { user.Role })
            .FirstOrDefaultAsync();

        if (user is not null && requirement.Allows(user.Role))
        {
            context.Succeed(requirement);
        }
    }
}
