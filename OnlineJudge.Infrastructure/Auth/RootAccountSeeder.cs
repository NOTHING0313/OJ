using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Auth;

public static class RootAccountSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OnlineJudgeDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        await dbContext.Users
            .Where(user => user.Role == (UserRole)0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.Role, UserRole.Answerer), cancellationToken);

        await dbContext.Users
            .Where(user => user.UpdatedAt == default)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.UpdatedAt, user => user.CreatedAt), cancellationToken);

        var rootExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UserName == "UnrealStudio", cancellationToken);

        if (rootExists)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = "UnrealStudio",
            Email = "unrealstudio@example.com",
            PasswordHash = passwordHasher.HashPassword("UnrealStudio"),
            Role = UserRole.Root,
            IsBlacklisted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
