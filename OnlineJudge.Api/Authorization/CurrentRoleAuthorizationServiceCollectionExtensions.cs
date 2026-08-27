using Microsoft.AspNetCore.Authorization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Api.Authorization;

public static class CurrentRoleAuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddCurrentRoleAuthorization(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, CurrentRoleAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireProblemSetter", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new CurrentRoleRequirement(UserRole.ProblemSetter, UserRole.Root));
            });
            options.AddPolicy("RequireRoot", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new CurrentRoleRequirement(UserRole.Root));
            });
        });

        return services;
    }
}
