using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Api.Authorization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Tests.Authorization;

public class CurrentRoleAuthorizationTests
{
    [Theory]
    [InlineData("RequireProblemSetter", UserRole.ProblemSetter, UserRole.ProblemSetter, true)]
    [InlineData("RequireProblemSetter", UserRole.Root, UserRole.Root, true)]
    [InlineData("RequireProblemSetter", UserRole.Answerer, UserRole.Answerer, false)]
    [InlineData("RequireProblemSetter", UserRole.Answerer, UserRole.ProblemSetter, true)]
    [InlineData("RequireProblemSetter", UserRole.ProblemSetter, UserRole.Answerer, false)]
    [InlineData("RequireRoot", UserRole.Root, UserRole.Root, true)]
    [InlineData("RequireRoot", UserRole.Root, UserRole.ProblemSetter, false)]
    public async Task Policy_UsesRoleMarkedAuthoritativeBySessionValidation(
        string policyName,
        UserRole jwtRole,
        UserRole authoritativeRole,
        bool expectedAuthorized)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCurrentRoleAuthorization();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var result = await authorizationService.AuthorizeAsync(CreatePrincipal(jwtRole, authoritativeRole), null, policyName);

        Assert.Equal(expectedAuthorized, result.Succeeded);
    }

    private static ClaimsPrincipal CreatePrincipal(UserRole jwtRole, UserRole authoritativeRole)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, jwtRole.ToString()),
            new Claim(AuthSessionConstants.AuthoritativeRoleClaim, authoritativeRole.ToString())
        ], "TestAuthentication");

        return new ClaimsPrincipal(identity);
    }
}
