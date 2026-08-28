using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardIdentityService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string AliasAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<LeaderboardViewer> GetViewerAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return new LeaderboardViewer(null, null, false);
        }

        var user = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBlacklisted)
            .Select(user => new { user.Id, user.Role })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? new LeaderboardViewer(null, null, false)
            : new LeaderboardViewer(user.Id, user.Role, user.Role is UserRole.ProblemSetter or UserRole.Root);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> EnsureCurrentSeasonAliasesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var seasonId = await dbContext.LeaderboardSeasons.AsNoTracking()
            .Where(season => season.IsCurrent)
            .Select(season => (Guid?)season.Id)
            .SingleOrDefaultAsync(cancellationToken);

        return seasonId.HasValue
            ? await EnsureAliasesAsync(seasonId.Value, userIds, cancellationToken)
            : new Dictionary<Guid, string>();
    }

    public async Task<IReadOnlyDictionary<Guid, string>> EnsureAliasesAsync(
        Guid seasonId,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var aliases = await dbContext.LeaderboardSeasonAliases.AsNoTracking()
                .Where(alias => alias.SeasonId == seasonId && ids.Contains(alias.UserId))
                .ToDictionaryAsync(alias => alias.UserId, alias => alias.Alias, cancellationToken);
            if (aliases.Count == ids.Count) return aliases;

            var usedAliases = await dbContext.LeaderboardSeasonAliases.AsNoTracking()
                .Where(alias => alias.SeasonId == seasonId)
                .Select(alias => alias.Alias)
                .ToHashSetAsync(cancellationToken);
            var additions = ids.Where(id => !aliases.ContainsKey(id)).Select(userId =>
            {
                var value = GenerateUniqueAlias(usedAliases);
                usedAliases.Add(value);
                aliases[userId] = value;
                return new LeaderboardSeasonAlias
                {
                    Id = Guid.NewGuid(),
                    SeasonId = seasonId,
                    UserId = userId,
                    Alias = value,
                    CreatedAt = timeProvider.GetUtcNow()
                };
            }).ToList();

            dbContext.LeaderboardSeasonAliases.AddRange(additions);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return aliases;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                foreach (var addition in additions) dbContext.Entry(addition).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Could not persist unique leaderboard aliases after concurrent allocation.");
    }

    public static LeaderboardDisplayIdentity Project(
        LeaderboardIdentityUser user,
        LeaderboardViewer viewer,
        IReadOnlyDictionary<Guid, string> aliases)
    {
        var alias = aliases.GetValueOrDefault(user.Id) ?? "NODE-HIDDEN";
        if (viewer.CanAudit || !user.IsAnonymous)
        {
            return new LeaderboardDisplayIdentity(
                user.Id,
                user.UserName,
                user.UserName,
                alias,
                user.IsAnonymous,
                user.AvatarUrl);
        }

        return new LeaderboardDisplayIdentity(null, null, alias, alias, true, null);
    }

    private static string GenerateUniqueAlias(ISet<string> usedAliases)
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var suffix = new char[6];
            for (var index = 0; index < suffix.Length; index++)
            {
                suffix[index] = AliasAlphabet[RandomNumberGenerator.GetInt32(AliasAlphabet.Length)];
            }

            var alias = $"NODE-{new string(suffix)}";
            if (!usedAliases.Contains(alias)) return alias;
        }

        throw new InvalidOperationException("Could not allocate a unique leaderboard alias.");
    }
}

public sealed record LeaderboardIdentityUser(Guid Id, string UserName, string? AvatarUrl, bool IsAnonymous);

public sealed record LeaderboardViewer(Guid? UserId, UserRole? Role, bool CanAudit);

public sealed record LeaderboardDisplayIdentity(
    Guid? UserId,
    string? UserName,
    string DisplayName,
    string Alias,
    bool IsAnonymous,
    string? AvatarUrl);
