using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Api.Authentication;
using OnlineJudge.Api.Authorization;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Tests.Authorization;

public class SeasonResultAccessTests
{
    [Theory]
    [InlineData(typeof(LeaderboardSeasonsController), "GetCurrent")]
    [InlineData(typeof(LeaderboardSeasonsController), "GetCurrentProblem")]
    [InlineData(typeof(LeaderboardSeasonHistoryController), "GetHistory")]
    [InlineData(typeof(AdminLeaderboardSeasonsController), "GetCurrentLeaderboard")]
    [InlineData(typeof(AdminLeaderboardSeasonsController), "GetHistory")]
    [InlineData(typeof(AdminLeaderboardSeasonsController), "GetUserCurrent")]
    [InlineData(typeof(AdminLeaderboardSeasonsController), "GetUserHistory")]
    [InlineData(typeof(AdminLeaderboardSeasonsController), "GetArchive")]
    public async Task FullResults_RequireAuthoritativeRoot(Type controller, string action)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCurrentRoleAuthorization();
        await using var provider = services.BuildServiceProvider();
        foreach (var method in controller.GetMethods().Where(method => method.Name == action))
        {
            var metadata = controller.GetCustomAttributes().Concat(method.GetCustomAttributes()).ToArray();
            Assert.Empty(metadata.OfType<IAllowAnonymous>());
            var policy = await AuthorizationPolicy.CombineAsync(provider.GetRequiredService<IAuthorizationPolicyProvider>(), metadata.OfType<IAuthorizeData>());
            Assert.NotNull(policy);
            var evaluator = provider.GetRequiredService<IAuthorizationService>();
            Assert.False((await evaluator.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
            foreach (var role in Enum.GetValues<UserRole>())
            {
                // A stale Root claim cannot override the current authoritative role.
                var principal = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.Role, "Root"),
                    new Claim(AuthSessionConstants.AuthoritativeRoleClaim, role.ToString())
                ], "Test"));
                Assert.Equal(role == UserRole.Root, (await evaluator.AuthorizeAsync(principal, null, policy)).Succeeded);
            }
        }
    }

    [Theory]
    [InlineData("GetCurrentPersonal")]
    [InlineData("GetPersonalHistory")]
    public void SelfQueries_DoNotAcceptTargetIdentity(string action)
    {
        var method = typeof(LeaderboardSeasonHistoryController).GetMethod(action)!;
        Assert.NotNull(method.GetCustomAttribute<AuthorizeAttribute>());
        Assert.All(method.GetParameters(), parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }
}
