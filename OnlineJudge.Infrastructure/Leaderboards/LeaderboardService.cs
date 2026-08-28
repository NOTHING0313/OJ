using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Leaderboards.Dtos;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Leaderboards;

public class LeaderboardService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    LeaderboardIdentityService identityService) : ILeaderboardService
{
    public LeaderboardService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser)
        : this(dbContext, currentUser, new LeaderboardIdentityService(dbContext, currentUser, TimeProvider.System))
    {
    }

    public async Task<Result<GlobalUserLeaderboardDto>> GetGlobalUserLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await identityService.GetViewerAsync(cancellationToken);
        var completionRows = await (
                from completion in dbContext.ChallengeTaskCompletions.AsNoTracking()
                join challenge in dbContext.Challenges.AsNoTracking() on completion.ChallengeId equals challenge.Id
                join user in dbContext.Users.AsNoTracking() on completion.UserId equals user.Id
                where challenge.IsPublished
                    && user.Role == UserRole.Answerer
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                select new
                {
                    completion.UserId,
                    user.UserName,
                    user.AvatarUrl,
                    user.IsLeaderboardAnonymous,
                    completion.ChallengeId,
                    completion.Score,
                    completion.IsCompleted,
                    completion.CompletedAt,
                    completion.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var entries = completionRows
            .GroupBy(row => new { row.UserId, row.UserName, row.AvatarUrl, row.IsLeaderboardAnonymous })
            .Select(group => new
            {
                group.Key.UserId,
                group.Key.UserName,
                group.Key.AvatarUrl,
                group.Key.IsLeaderboardAnonymous,
                CompletedChallengeCount = group.Where(row => row.IsCompleted).Select(row => row.ChallengeId).Distinct().Count(),
                CompletedTaskCount = group.Count(row => row.IsCompleted),
                TotalScore = group.Sum(row => row.Score),
                LastCompletedAt = group.Max(row => row.UpdatedAt)
            })
            .OrderByDescending(entry => entry.TotalScore)
            .ThenByDescending(entry => entry.CompletedTaskCount)
            .ThenByDescending(entry => entry.CompletedChallengeCount)
            .ThenBy(entry => entry.LastCompletedAt)
            .ThenBy(entry => entry.UserName)
            .ToList();

        var aliases = await identityService.EnsureCurrentSeasonAliasesAsync(entries.Select(entry => entry.UserId), cancellationToken);
        var projectedEntries = entries.Select((entry, index) =>
            {
                var identity = LeaderboardIdentityService.Project(
                    new LeaderboardIdentityUser(entry.UserId, entry.UserName, entry.AvatarUrl, entry.IsLeaderboardAnonymous), viewer, aliases);
                return new GlobalUserLeaderboardEntryDto
                {
                Rank = index + 1,
                UserId = identity.UserId,
                UserName = identity.DisplayName,
                Alias = identity.Alias,
                IsAnonymous = identity.IsAnonymous,
                AvatarUrl = identity.AvatarUrl,
                CompletedChallengeCount = entry.CompletedChallengeCount,
                CompletedTaskCount = entry.CompletedTaskCount,
                TotalScore = entry.TotalScore,
                LastCompletedAt = entry.LastCompletedAt,
                IsCurrentUser = viewer.UserId == entry.UserId
                };
            })
            .ToList();

        return Result<GlobalUserLeaderboardDto>.Success(new GlobalUserLeaderboardDto
        {
            Entries = projectedEntries
        });
    }

    public async Task<Result<RankHistoryDto>> GetGlobalUserRankHistoryAsync(int days = 10, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 2, 10);
        var viewer = await identityService.GetViewerAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var historyStart = todayStart.AddDays(-(days - 1));
        var historyEnd = todayStart.AddDays(1);

        var rows = await (
                from completion in dbContext.ChallengeTaskCompletions.AsNoTracking()
                join challenge in dbContext.Challenges.AsNoTracking() on completion.ChallengeId equals challenge.Id
                join user in dbContext.Users.AsNoTracking() on completion.UserId equals user.Id
                where challenge.IsPublished
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                    && user.Role == UserRole.Answerer
                    && completion.UpdatedAt < historyEnd
                select new GlobalHistoryRow
                {
                    UserId = completion.UserId,
                    UserName = user.UserName,
                    AvatarUrl = user.AvatarUrl,
                    IsLeaderboardAnonymous = user.IsLeaderboardAnonymous,
                    ChallengeId = completion.ChallengeId,
                    Score = completion.Score,
                    IsCompleted = completion.IsCompleted,
                    CompletedAt = completion.CompletedAt,
                    UpdatedAt = completion.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var aliases = await identityService.EnsureCurrentSeasonAliasesAsync(rows.Select(row => row.UserId), cancellationToken);

        var history = new RankHistoryDto
        {
            Days = Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var dayStart = historyStart.AddDays(offset);
                    var cutoff = offset == days - 1 ? now.AddTicks(1) : dayStart.AddDays(1);
                    var entries = rows
                        .Where(row => row.UpdatedAt < cutoff)
                        .GroupBy(row => new { row.UserId, row.UserName, row.AvatarUrl, row.IsLeaderboardAnonymous })
                        .Select(group => new
                        {
                            group.Key.UserId,
                            group.Key.UserName,
                            group.Key.AvatarUrl,
                            group.Key.IsLeaderboardAnonymous,
                            CompletedChallengeCount = group
                                .Where(row => row.IsCompleted && row.CompletedAt < cutoff)
                                .Select(row => row.ChallengeId)
                                .Distinct()
                                .Count(),
                            CompletedTaskCount = group.Count(row => row.IsCompleted && row.CompletedAt < cutoff),
                            TotalScore = group.Sum(row => row.Score),
                            LastCompletedAt = group
                                .Where(row => row.IsCompleted && row.CompletedAt < cutoff)
                                .Select(row => (DateTimeOffset?)row.CompletedAt)
                                .Max()
                        })
                        .Where(entry => entry.CompletedTaskCount > 0 || entry.TotalScore > 0)
                        .OrderByDescending(entry => entry.TotalScore)
                        .ThenByDescending(entry => entry.CompletedTaskCount)
                        .ThenByDescending(entry => entry.CompletedChallengeCount)
                        .ThenBy(entry => entry.LastCompletedAt ?? DateTimeOffset.MaxValue)
                        .ThenBy(entry => entry.UserName)
                        .Select((entry, index) =>
                        {
                            var identity = LeaderboardIdentityService.Project(
                                new LeaderboardIdentityUser(entry.UserId, entry.UserName, entry.AvatarUrl, entry.IsLeaderboardAnonymous), viewer, aliases);
                            return new RankHistoryEntryDto
                            {
                            UserId = identity.UserId,
                            UserName = identity.DisplayName,
                            Alias = identity.Alias,
                            IsAnonymous = identity.IsAnonymous,
                            Rank = index + 1,
                            TotalScore = entry.TotalScore,
                            CompletedTaskCount = entry.CompletedTaskCount,
                            IsCurrentUser = viewer.UserId == entry.UserId
                            };
                        })
                        .ToList();

                    return new RankHistoryDayDto
                    {
                        Date = DateOnly.FromDateTime(dayStart.UtcDateTime),
                        Entries = entries
                    };
                })
                .ToList()
        };

        return Result<RankHistoryDto>.Success(history);
    }

    public async Task<Result<ChallengeLeaderboardIndexDto>> GetChallengeLeaderboardIndexAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await identityService.GetViewerAsync(cancellationToken);
        var challenges = await dbContext.Challenges
            .AsNoTracking()
            .Where(challenge => challenge.IsPublished)
            .OrderByDescending(challenge => challenge.StartAt)
            .Select(challenge => new
            {
                challenge.Id,
                challenge.Title,
                challenge.Description,
                challenge.StartAt,
                challenge.EndAt,
                challenge.IsPublished
            })
            .ToListAsync(cancellationToken);

        if (challenges.Count == 0)
        {
            return Result<ChallengeLeaderboardIndexDto>.Success(new ChallengeLeaderboardIndexDto());
        }

        var challengeIds = challenges.Select(challenge => challenge.Id).ToList();
        var taskCounts = await dbContext.ChallengeTasks
            .AsNoTracking()
            .Where(task => challengeIds.Contains(task.ChallengeId))
            .GroupBy(task => task.ChallengeId)
            .Select(group => new { ChallengeId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ChallengeId, item => item.Count, cancellationToken);

        var participantRows = await (
                from participant in dbContext.ChallengeParticipants.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on participant.UserId equals user.Id
                where challengeIds.Contains(participant.ChallengeId)
                    && user.Role == UserRole.Answerer
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                select new { participant.ChallengeId, participant.UserId })
            .ToListAsync(cancellationToken);

        var completionRows = await (
                from completion in dbContext.ChallengeTaskCompletions.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on completion.UserId equals user.Id
                where challengeIds.Contains(completion.ChallengeId)
                    && user.Role == UserRole.Answerer
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                select new
                {
                    completion.ChallengeId,
                    completion.UserId,
                    user.UserName,
                    user.AvatarUrl,
                    user.IsLeaderboardAnonymous,
                    completion.Score,
                    completion.IsCompleted,
                    completion.CompletedAt,
                    completion.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var aliases = await identityService.EnsureCurrentSeasonAliasesAsync(completionRows.Select(row => row.UserId), cancellationToken);

        var participantCountMap = participantRows
            .Concat(completionRows.Select(row => new { row.ChallengeId, row.UserId }))
            .GroupBy(row => row.ChallengeId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.UserId).Distinct().Count());

        var completedUserCountMap = completionRows
            .Where(row => row.IsCompleted)
            .GroupBy(row => row.ChallengeId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.UserId).Distinct().Count());

        var topEntriesMap = completionRows
            .GroupBy(row => row.ChallengeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(row => new { row.UserId, row.UserName, row.AvatarUrl, row.IsLeaderboardAnonymous })
                    .Select(userGroup => new
                    {
                        userGroup.Key.UserId,
                        userGroup.Key.UserName,
                        userGroup.Key.AvatarUrl,
                        userGroup.Key.IsLeaderboardAnonymous,
                        CompletedTaskCount = userGroup.Count(row => row.IsCompleted),
                        TotalScore = userGroup.Sum(row => row.Score),
                        LastCompletedAt = userGroup.Max(row => row.UpdatedAt)
                    })
                    .OrderByDescending(entry => entry.TotalScore)
                    .ThenByDescending(entry => entry.CompletedTaskCount)
                    .ThenBy(entry => entry.LastCompletedAt)
                    .ThenBy(entry => entry.UserName)
                    .Take(3)
                    .Select((entry, index) =>
                    {
                        var identity = LeaderboardIdentityService.Project(
                            new LeaderboardIdentityUser(entry.UserId, entry.UserName, entry.AvatarUrl, entry.IsLeaderboardAnonymous), viewer, aliases);
                        return new ChallengeLeaderboardTopEntryDto
                        {
                        Rank = index + 1,
                        UserId = identity.UserId,
                        UserName = identity.DisplayName,
                        Alias = identity.Alias,
                        IsAnonymous = identity.IsAnonymous,
                        AvatarUrl = identity.AvatarUrl,
                        CompletedTaskCount = entry.CompletedTaskCount,
                        TotalScore = entry.TotalScore,
                        LastCompletedAt = entry.LastCompletedAt
                        };
                    })
                    .ToList());

        var summaries = challenges
            .Select(challenge => new ChallengeLeaderboardSummaryDto
            {
                ChallengeId = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                StartAt = challenge.StartAt,
                EndAt = challenge.EndAt,
                IsPublished = challenge.IsPublished,
                TotalTaskCount = taskCounts.GetValueOrDefault(challenge.Id),
                ParticipantCount = participantCountMap.GetValueOrDefault(challenge.Id),
                CompletedUserCount = completedUserCountMap.GetValueOrDefault(challenge.Id),
                TopEntries = topEntriesMap.GetValueOrDefault(challenge.Id) ?? []
            })
            .ToList();

        return Result<ChallengeLeaderboardIndexDto>.Success(new ChallengeLeaderboardIndexDto
        {
            Challenges = summaries
        });
    }

    private sealed class GlobalHistoryRow
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public bool IsLeaderboardAnonymous { get; set; }

        public Guid ChallengeId { get; set; }

        public int Score { get; set; }

        public bool IsCompleted { get; set; }

        public DateTimeOffset CompletedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    private async Task<Guid?> GetCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return null;
        }

        var isActiveUser = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && !user.IsBlacklisted, cancellationToken);

        return isActiveUser ? userId : null;
    }
}
