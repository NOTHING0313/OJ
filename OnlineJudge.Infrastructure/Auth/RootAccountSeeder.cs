using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        var passwordPolicy = scope.ServiceProvider.GetRequiredService<PasswordPolicy>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

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
            .AnyAsync(user => user.Role == UserRole.Root, cancellationToken);

        if (rootExists)
        {
            return;
        }

        var userName = GetRequiredConfiguration(configuration, "RootAccount:UserName");
        var email = GetRequiredConfiguration(configuration, "RootAccount:Email").ToLowerInvariant();
        var password = GetRequiredConfiguration(configuration, "RootAccount:Password");

        var passwordError = passwordPolicy.Validate(password, userName, email);
        if (passwordError is not null)
        {
            throw new InvalidOperationException($"RootAccount:Password is invalid. {passwordError}");
        }

        var accountConflict = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.UserName == userName || user.Email.ToLower() == email, cancellationToken);

        if (accountConflict)
        {
            throw new InvalidOperationException("Configured root account conflicts with an existing user.");
        }

        var now = DateTimeOffset.UtcNow;

        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = UserRole.Root,
            IsBlacklisted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetRequiredConfiguration(IConfiguration configuration, string key)
    {
        var value = configuration[key]?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is not configured.");
        }

        return value;
    }
}
