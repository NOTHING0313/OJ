using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Api.Authorization;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Authorization;

public class CurrentRoleAuthorizationTests
{
    [Theory]
    [InlineData("RequireProblemSetter", UserRole.ProblemSetter, UserRole.ProblemSetter, false, false, true)]
    [InlineData("RequireProblemSetter", UserRole.Root, UserRole.Root, false, false, true)]
    [InlineData("RequireProblemSetter", UserRole.Answerer, UserRole.Answerer, false, false, false)]
    [InlineData("RequireProblemSetter", UserRole.Answerer, UserRole.ProblemSetter, false, false, true)]
    [InlineData("RequireProblemSetter", UserRole.ProblemSetter, UserRole.Answerer, false, false, false)]
    [InlineData("RequireProblemSetter", UserRole.ProblemSetter, UserRole.ProblemSetter, true, false, false)]
    [InlineData("RequireRoot", UserRole.Root, UserRole.Root, false, true, false)]
    [InlineData("RequireRoot", UserRole.Root, UserRole.Root, false, false, true)]
    [InlineData("RequireRoot", UserRole.Root, UserRole.ProblemSetter, false, false, false)]
    public async Task Policy_UsesCurrentDatabaseRole(
        string policyName,
        UserRole jwtRole,
        UserRole databaseRole,
        bool isBlacklisted,
        bool isDeleted,
        bool expectedAuthorized)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<OnlineJudgeDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddCurrentRoleAuthorization();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var userId = Guid.NewGuid();
        var dbContext = scope.ServiceProvider.GetRequiredService<OnlineJudgeDbContext>();
        dbContext.Users.Add(CreateUser(userId, databaseRole, isBlacklisted, isDeleted));
        await dbContext.SaveChangesAsync();

        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var result = await authorizationService.AuthorizeAsync(CreatePrincipal(userId, jwtRole), null, policyName);

        Assert.Equal(expectedAuthorized, result.Succeeded);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, UserRole jwtRole)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, jwtRole.ToString())
        ], "TestAuthentication");

        return new ClaimsPrincipal(identity);
    }

    private static User CreateUser(Guid userId, UserRole role, bool isBlacklisted, bool isDeleted)
    {
        return new User
        {
            Id = userId,
            UserName = $"user-{userId:N}",
            Email = $"{userId:N}@example.test",
            PasswordHash = "hash",
            Role = role,
            IsBlacklisted = isBlacklisted,
            IsDeleted = isDeleted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
