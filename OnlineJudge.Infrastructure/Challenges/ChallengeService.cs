using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Challenges.Services;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Leaderboards.Dtos;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using System.Text;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Leaderboards;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Infrastructure.Challenges;

public class ChallengeService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    ContentVisibilityPolicy visibilityPolicy,
    LeaderboardIdentityService identityService,
    IRuntimeStoragePathProvider storagePaths,
    ISecurityAuditWriter? auditWriter = null) : IChallengeService
{
    public ChallengeService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser)
        : this(dbContext, currentUser, new ContentVisibilityPolicy(TimeProvider.System), new LeaderboardIdentityService(dbContext, currentUser, TimeProvider.System), RuntimeStoragePathProvider.CreateDevelopmentDefault())
    {
    }

    public ChallengeService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, ContentVisibilityPolicy visibilityPolicy)
        : this(dbContext, currentUser, visibilityPolicy, new LeaderboardIdentityService(dbContext, currentUser, TimeProvider.System), RuntimeStoragePathProvider.CreateDevelopmentDefault())
    {
    }

    public ChallengeService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, ContentVisibilityPolicy visibilityPolicy, IRuntimeStoragePathProvider storagePaths)
        : this(dbContext, currentUser, visibilityPolicy, new LeaderboardIdentityService(dbContext, currentUser, TimeProvider.System), storagePaths)
    {
    }

    private const long MaxFileSubmissionSizeBytes = 50L * 1024 * 1024;
    private static readonly HashSet<string> AllowedZipContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/zip",
        "application/x-zip-compressed",
        "application/octet-stream"
    };

    public async Task<Result<IReadOnlyList<ChallengeListItemDto>>> GetChallengesAsync(CancellationToken cancellationToken = default)
    {
        var visibilityRole = await GetVisibilityRoleAsync(cancellationToken);
        var query = dbContext.Challenges
            .AsNoTracking()
            .AsQueryable();

        var challenges = await visibilityPolicy.ApplyChallengeVisibility(query, visibilityRole)
            .Include(challenge => challenge.Tasks)
            .Include(challenge => challenge.Participants)
            .Include(challenge => challenge.TeamParticipants)
            .OrderByDescending(challenge => challenge.CreatedAt)
            .ToListAsync(cancellationToken);

        var completions = await GetCurrentUserCompletionsAsync(
            challenges.Select(challenge => challenge.Id),
            cancellationToken);

        var items = challenges
            .Select(challenge => new ChallengeListItemDto
            {
                Id = challenge.Id,
                Title = challenge.Title,
                Description = challenge.Description,
                StartAt = challenge.StartAt,
                EndAt = challenge.EndAt,
                IsPublished = challenge.IsPublished,
                ParticipationMode = challenge.ParticipationMode,
                TeamCount = challenge.TeamParticipants.Count,
                ParticipantCount = challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly
                    ? challenge.TeamParticipants.Count
                    : challenge.Participants.Count,
                CreatedAt = challenge.CreatedAt,
                TotalTaskCount = challenge.Tasks.Count,
                CompletedTaskCount = CountCompletedTasks(challenge, completions),
                CanManage = CanManageChallengeForCurrentUser(challenge, visibilityRole)
            })
            .ToList();

        if (currentUser.IsAuthenticated && currentUser.UserId is { } currentUserId)
        {
            var teamProgress = await dbContext.ChallengeTeamRosterMembers.AsNoTracking()
                .Where(member => member.UserId == currentUserId && challenges.Select(challenge => challenge.Id).Contains(member.ChallengeId))
                .SelectMany(member => member.ChallengeTeamParticipant!.TaskCompletions)
                .Where(completion => completion.IsCompleted)
                .GroupBy(completion => completion.ChallengeId)
                .Select(group => new { ChallengeId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(row => row.ChallengeId, row => row.Count, cancellationToken);
            foreach (var item in items.Where(item => item.ParticipationMode == ChallengeParticipationMode.TeamOnly))
            {
                item.CompletedTaskCount = teamProgress.GetValueOrDefault(item.Id);
            }
        }

        return Result<IReadOnlyList<ChallengeListItemDto>>.Success(items);
    }

    public async Task<Result<ChallengeDetailDto>> GetChallengeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var visibilityRole = await GetVisibilityRoleAsync(cancellationToken);
        var query = dbContext.Challenges
            .AsNoTracking()
            .AsQueryable();

        var challenge = await visibilityPolicy.ApplyChallengeVisibility(query, visibilityRole)
            .Include(challenge => challenge.Tasks)
            .FirstOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeDetailDto>.Failure("Challenge not found.");
        }

        var completions = challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly
            ? new Dictionary<Guid, ChallengeTaskCompletion>()
            : await GetCurrentUserCompletionsAsync([challenge.Id], cancellationToken);
        var detail = ToDetailDto(challenge, completions, visibilityRole);
        detail.ParticipationModeLocked = await IsParticipationModeLockedAsync(challenge, cancellationToken);
        detail.PeerReviewConfigurationLocked = await IsPeerReviewConfigurationLockedAsync(challenge, cancellationToken);
        await PopulateTeamParticipationAsync(detail, cancellationToken);
        return Result<ChallengeDetailDto>.Success(detail);
    }

    public async Task<Result<ChallengeLeaderboardDto>> GetLeaderboardAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeLeaderboardDto>.Failure("Challenge not found.");
        }

        var userResult = await GetOptionalCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result<ChallengeLeaderboardDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var user = userResult.Value;
        if (!CanViewLeaderboard(user, challenge))
        {
            return Result<ChallengeLeaderboardDto>.Failure("Challenge not found.");
        }
        if (!await CanViewSelectedLeaderboardAsync(user, challengeId, cancellationToken))
        {
            return Result<ChallengeLeaderboardDto>.Failure("Challenge not found.");
        }

        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return await GetTeamLeaderboardAsync(challenge, cancellationToken);
        }

        var viewer = user is null
            ? new LeaderboardViewer(null, null, false)
            : new LeaderboardViewer(user.Id, user.Role, user.Role is UserRole.ProblemSetter or UserRole.Root);

        var totalTaskCount = await dbContext.ChallengeTasks
            .AsNoTracking()
            .CountAsync(task => task.ChallengeId == challengeId, cancellationToken);

        var groupedCompletions = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.ChallengeId == challengeId && (completion.Score > 0 || completion.IsCompleted))
            .GroupBy(completion => completion.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                CompletedTaskCount = group.Count(completion => completion.IsCompleted),
                TotalScore = group.Sum(completion => completion.Score),
                LastCompletedAt = group.Max(completion => completion.UpdatedAt)
            })
            .Join(
                dbContext.Users.AsNoTracking().Where(user => user.Role == UserRole.Answerer && !user.IsBlacklisted && !user.IsDeleted),
                completion => completion.UserId,
                user => user.Id,
                (completion, user) => new
                {
                    completion.UserId,
                    user.UserName,
                    user.AvatarUrl,
                    user.IsLeaderboardAnonymous,
                    completion.CompletedTaskCount,
                    completion.TotalScore,
                    completion.LastCompletedAt
                })
            .OrderByDescending(entry => entry.TotalScore)
            .ThenByDescending(entry => entry.CompletedTaskCount)
            .ThenBy(entry => entry.LastCompletedAt)
            .ThenBy(entry => entry.UserName)
            .ToListAsync(cancellationToken);

        var aliases = await identityService.EnsureCurrentSeasonAliasesAsync(groupedCompletions.Select(entry => entry.UserId), cancellationToken);

        var entries = groupedCompletions
            .Select((entry, index) =>
            {
                var identity = LeaderboardIdentityService.Project(
                    new LeaderboardIdentityUser(entry.UserId, entry.UserName, entry.AvatarUrl, entry.IsLeaderboardAnonymous), viewer, aliases);
                return new ChallengeLeaderboardEntryDto
                {
                Rank = index + 1,
                UserId = identity.UserId,
                UserName = identity.DisplayName,
                Alias = identity.Alias,
                IsAnonymous = identity.IsAnonymous,
                AvatarUrl = identity.AvatarUrl,
                CompletedTaskCount = entry.CompletedTaskCount,
                TotalScore = entry.TotalScore,
                LastCompletedAt = entry.LastCompletedAt,
                IsCurrentUser = user?.Id == entry.UserId
                };
            })
            .ToList();

        return Result<ChallengeLeaderboardDto>.Success(new ChallengeLeaderboardDto
        {
            ChallengeId = challenge.Id,
            ChallengeTitle = challenge.Title,
            TotalTaskCount = totalTaskCount,
            ParticipationMode = ChallengeParticipationMode.Individual,
            Entries = entries
        });
    }

    public async Task<Result<ChallengeLeaderboardProgressDto>> GetLeaderboardProgressAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeLeaderboardProgressDto>.Failure("Challenge not found.");
        }

        var userResult = await GetOptionalCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result<ChallengeLeaderboardProgressDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var current = userResult.Value;
        if (!CanViewLeaderboard(current, challenge))
        {
            return Result<ChallengeLeaderboardProgressDto>.Failure("Challenge not found.");
        }
        if (!await CanViewSelectedLeaderboardAsync(current, challengeId, cancellationToken))
        {
            return Result<ChallengeLeaderboardProgressDto>.Failure("Challenge not found.");
        }
        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return await GetTeamLeaderboardProgressAsync(challenge, cancellationToken);
        }

        var viewer = current is null
            ? new LeaderboardViewer(null, null, false)
            : new LeaderboardViewer(current.Id, current.Role, current.Role is UserRole.ProblemSetter or UserRole.Root);

        var tasks = await dbContext.ChallengeTasks
            .AsNoTracking()
            .Where(task => task.ChallengeId == challengeId)
            .OrderBy(task => task.BoardY)
            .ThenBy(task => task.BoardX)
            .ThenBy(task => task.CreatedAt)
            .Select(task => new ChallengeLeaderboardProgressTaskDto
            {
                TaskId = task.Id,
                Title = task.Title,
                Score = task.Score
            })
            .ToListAsync(cancellationToken);

        var participantRows = await (
                from participant in dbContext.ChallengeParticipants.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on participant.UserId equals user.Id
                where participant.ChallengeId == challengeId
                    && user.Role == UserRole.Answerer
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                select new ChallengeProgressUserRow
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    AvatarUrl = user.AvatarUrl,
                    IsLeaderboardAnonymous = user.IsLeaderboardAnonymous
                })
            .ToListAsync(cancellationToken);

        var completionRows = await (
                from completion in dbContext.ChallengeTaskCompletions.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on completion.UserId equals user.Id
                where completion.ChallengeId == challengeId
                    && user.Role == UserRole.Answerer
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                select new ChallengeProgressCompletionRow
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    AvatarUrl = user.AvatarUrl,
                    IsLeaderboardAnonymous = user.IsLeaderboardAnonymous,
                    TaskId = completion.ChallengeTaskId,
                    Score = completion.Score,
                    IsCompleted = completion.IsCompleted,
                    CompletedAt = completion.CompletedAt,
                    UpdatedAt = completion.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        var users = participantRows
            .Concat(completionRows.Select(row => new ChallengeProgressUserRow
            {
                UserId = row.UserId,
                UserName = row.UserName,
                AvatarUrl = row.AvatarUrl,
                IsLeaderboardAnonymous = row.IsLeaderboardAnonymous
            }))
            .GroupBy(row => row.UserId)
            .Select(group => group.First())
            .ToList();

        var aliases = await identityService.EnsureCurrentSeasonAliasesAsync(users.Select(user => user.UserId), cancellationToken);

        var completionMap = completionRows
            .GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rankedUsers = users
            .Select(user =>
            {
                var userCompletions = completionMap.GetValueOrDefault(user.UserId) ?? [];
                return new
                {
                    User = user,
                    Completions = userCompletions,
                    CompletedTaskCount = userCompletions.Count(row => row.IsCompleted),
                    TotalScore = userCompletions.Sum(row => row.Score),
                    LastCompletedAt = userCompletions.Count == 0 ? (DateTimeOffset?)null : userCompletions.Max(row => row.UpdatedAt)
                };
            })
            .OrderByDescending(entry => entry.TotalScore)
            .ThenByDescending(entry => entry.CompletedTaskCount)
            .ThenBy(entry => entry.LastCompletedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(entry => entry.User.UserName)
            .ToList();

        var rankedScoredUsers = rankedUsers.Where(entry => entry.TotalScore > 0 || entry.CompletedTaskCount > 0).ToList();
        var rankMap = rankedScoredUsers
            .Select((entry, index) => new { entry.User.UserId, Rank = index + 1 })
            .ToDictionary(entry => entry.UserId, entry => entry.Rank);

        var progressUsers = rankedUsers
            .Select(entry =>
            {
                var identity = LeaderboardIdentityService.Project(
                    new LeaderboardIdentityUser(entry.User.UserId, entry.User.UserName, entry.User.AvatarUrl, entry.User.IsLeaderboardAnonymous), viewer, aliases);
                return new ChallengeLeaderboardProgressUserDto
                {
                UserId = identity.UserId,
                UserName = identity.DisplayName,
                Alias = identity.Alias,
                IsAnonymous = identity.IsAnonymous,
                AvatarUrl = identity.AvatarUrl,
                Rank = rankMap.TryGetValue(entry.User.UserId, out var rank) ? rank : null,
                CompletedTaskCount = entry.CompletedTaskCount,
                TotalScore = entry.TotalScore,
                LastCompletedAt = entry.LastCompletedAt,
                IsCurrentUser = current?.Id == entry.User.UserId,
                CompletedTaskIds = entry.Completions.Where(row => row.IsCompleted).Select(row => row.TaskId).Distinct().ToList(),
                TaskScores = entry.Completions.GroupBy(row => row.TaskId).ToDictionary(group => group.Key, group => group.Max(row => row.Score))
                };
            })
            .ToList();

        return Result<ChallengeLeaderboardProgressDto>.Success(new ChallengeLeaderboardProgressDto
        {
            ChallengeId = challenge.Id,
            ChallengeTitle = challenge.Title,
            Tasks = tasks,
            Users = progressUsers
        });
    }

    public async Task<Result<RankHistoryDto>> GetLeaderboardHistoryAsync(Guid challengeId, int days = 10, CancellationToken cancellationToken = default)
    {
        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return Result<RankHistoryDto>.Failure("Challenge not found.");
        }

        var userResult = await GetOptionalCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure)
        {
            return Result<RankHistoryDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var current = userResult.Value;
        if (!CanViewLeaderboard(current, challenge))
        {
            return Result<RankHistoryDto>.Failure("Challenge not found.");
        }
        if (!await CanViewSelectedLeaderboardAsync(current, challengeId, cancellationToken))
        {
            return Result<RankHistoryDto>.Failure("Challenge not found.");
        }
        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return Result<RankHistoryDto>.Success(new RankHistoryDto());
        }

        var viewer = current is null
            ? new LeaderboardViewer(null, null, false)
            : new LeaderboardViewer(current.Id, current.Role, current.Role is UserRole.ProblemSetter or UserRole.Root);

        days = Math.Clamp(days, 2, 10);
        var now = visibilityPolicy.UtcNow;
        var todayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var historyStart = todayStart.AddDays(-(days - 1));
        var historyEnd = todayStart.AddDays(1);

        var rows = await (
                from completion in dbContext.ChallengeTaskCompletions.AsNoTracking()
                join user in dbContext.Users.AsNoTracking() on completion.UserId equals user.Id
                where completion.ChallengeId == challengeId
                    && !user.IsBlacklisted
                    && !user.IsDeleted
                    && user.Role == UserRole.Answerer
                    && completion.IsCompleted
                    && completion.CompletedAt < historyEnd
                select new ChallengeProgressCompletionRow
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    AvatarUrl = user.AvatarUrl,
                    IsLeaderboardAnonymous = user.IsLeaderboardAnonymous,
                    TaskId = completion.ChallengeTaskId,
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
                        .Where(row => row.CompletedAt < cutoff)
                        .GroupBy(row => new { row.UserId, row.UserName, row.AvatarUrl, row.IsLeaderboardAnonymous })
                        .Select(group => new
                        {
                            group.Key.UserId,
                            group.Key.UserName,
                            group.Key.AvatarUrl,
                            group.Key.IsLeaderboardAnonymous,
                            CompletedTaskCount = group.Count(),
                            TotalScore = group.Sum(row => row.Score),
                            LastCompletedAt = group.Max(row => row.CompletedAt)
                        })
                        .OrderByDescending(entry => entry.TotalScore)
                        .ThenByDescending(entry => entry.CompletedTaskCount)
                        .ThenBy(entry => entry.LastCompletedAt)
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
                            IsCurrentUser = current?.Id == entry.UserId
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

    public async Task<Result<ChallengeAdminSummaryDto>> GetAdminSummaryAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeAdminSummaryDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .Include(challenge => challenge.Tasks)
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeAdminSummaryDto>.Failure("Challenge not found.");
        }

        if (!CanManageChallenge(userResult.Value, challenge))
        {
            return Result<ChallengeAdminSummaryDto>.Failure("Forbidden.");
        }

        var tasks = challenge.Tasks
            .OrderBy(task => task.BoardY)
            .ThenBy(task => task.BoardX)
            .ToList();

        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return await GetTeamAdminSummaryAsync(challenge, tasks, cancellationToken);
        }

        var completions = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.ChallengeId == challengeId)
            .ToListAsync(cancellationToken);

        var participants = await dbContext.ChallengeParticipants
            .AsNoTracking()
            .Where(participant => participant.ChallengeId == challengeId)
            .ToListAsync(cancellationToken);

        var fileSubmissions = await dbContext.ChallengeTaskFileSubmissions
            .AsNoTracking()
            .Include(submission => submission.ReviewedByUser)
            .Where(submission => submission.ChallengeId == challengeId)
            .ToListAsync(cancellationToken);

        var participantIds = participants
            .Select(participant => participant.UserId)
            .Concat(completions.Select(completion => completion.UserId))
            .Concat(fileSubmissions.Select(submission => submission.UserId))
            .Distinct()
            .ToList();

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => participantIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        var completionMap = completions
            .GroupBy(completion => (completion.UserId, completion.ChallengeTaskId))
            .ToDictionary(group => group.Key, group => group.First());

        var fileSubmissionMap = fileSubmissions
            .GroupBy(submission => (submission.UserId, submission.ChallengeTaskId))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(submission => submission.UpdatedAt).First());

        var completedUserCountByTask = completions
            .Where(completion => completion.IsCompleted)
            .GroupBy(completion => completion.ChallengeTaskId)
            .ToDictionary(group => group.Key, group => group.Select(completion => completion.UserId).Distinct().Count());

        var taskProgress = tasks
            .Select(task => new ChallengeAdminTaskProgressDto
            {
                TaskId = task.Id,
                Title = task.Title,
                TaskType = (int)task.TaskType,
                Difficulty = (int)task.Difficulty,
                Score = task.Score,
                CompletedUserCount = completedUserCountByTask.GetValueOrDefault(task.Id)
            })
            .ToList();

        var userProgress = users
            .Select(user =>
            {
                var userCompletions = completions
                    .Where(completion => completion.UserId == user.Id)
                    .ToList();

                return new ChallengeAdminUserProgressDto
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    AvatarUrl = user.AvatarUrl,
                    CompletedTaskCount = userCompletions.Count(completion => completion.IsCompleted),
                    TotalScore = userCompletions.Sum(completion => completion.Score),
                    LastCompletedAt = userCompletions.Count == 0 ? null : userCompletions.Max(completion => completion.UpdatedAt),
                    TaskStatuses = tasks
                        .Select(task =>
                        {
                            completionMap.TryGetValue((user.Id, task.Id), out var completion);
                            fileSubmissionMap.TryGetValue((user.Id, task.Id), out var fileSubmission);

                            return ToAdminUserTaskStatusDto(task, completion, fileSubmission);
                        })
                        .ToList()
                };
            })
            .OrderByDescending(user => user.TotalScore)
            .ThenByDescending(user => user.CompletedTaskCount)
            .ThenBy(user => user.LastCompletedAt)
            .ThenBy(user => user.UserName)
            .ToList();

        return Result<ChallengeAdminSummaryDto>.Success(new ChallengeAdminSummaryDto
        {
            ChallengeId = challenge.Id,
            ChallengeTitle = challenge.Title,
            ParticipationMode = challenge.ParticipationMode,
            PeerReviewEnabled = challenge.PeerReviewEnabled,
            TotalTaskCount = tasks.Count,
            ParticipantCount = users.Count,
            TotalCompletionCount = completions.Count(completion => completion.IsCompleted),
            Users = userProgress,
            Tasks = taskProgress
        });
    }

    private async Task<Result<ChallengeAdminSummaryDto>> GetTeamAdminSummaryAsync(Challenge challenge, IReadOnlyList<ChallengeTask> tasks, CancellationToken cancellationToken)
    {
        var participants = await dbContext.ChallengeTeamParticipants.AsNoTracking()
            .Where(participant => participant.ChallengeId == challenge.Id)
            .Include(participant => participant.RosterMembers)
            .Include(participant => participant.TaskCompletions).ThenInclude(completion => completion.ContributorUser)
            .ToListAsync(cancellationToken);
        var teams = participants.Select(participant => new ChallengeAdminTeamProgressDto
        {
            TeamParticipantId = participant.Id, TeamId = participant.TeamId, TeamName = participant.TeamNameSnapshot,
            RegisteredByUserId = participant.RegisteredByUserId, RegisteredAt = participant.RegisteredAt,
            TotalScore = participant.TaskCompletions.Sum(completion => completion.Score),
            CompletedTaskCount = participant.TaskCompletions.Count(completion => completion.IsCompleted),
            Roster = participant.RosterMembers.OrderBy(member => member.UserNameSnapshot).Select(member => new ChallengeAdminTeamRosterMemberDto
            { UserId = member.UserId, UserName = member.UserNameSnapshot, Role = (int)member.TeamMemberRoleSnapshot }).ToList(),
            Tasks = tasks.Select(task =>
            {
                var completion = participant.TaskCompletions.FirstOrDefault(row => row.ChallengeTaskId == task.Id);
                return new ChallengeAdminTeamTaskStatusDto
                {
                    TaskId = task.Id, TaskTitle = task.Title, Score = completion?.Score ?? 0,
                    IsCompleted = completion?.IsCompleted == true, BestSubmissionId = completion?.BestSubmissionId,
                    ContributorUserId = completion?.ContributorUserId, ContributorUserName = completion?.ContributorUser?.UserName,
                    CompletedAt = completion?.IsCompleted == true ? completion.CompletedAt : null, UpdatedAt = completion?.UpdatedAt
                };
            }).ToList()
        }).OrderByDescending(team => team.TotalScore).ThenByDescending(team => team.CompletedTaskCount).ThenBy(team => team.TeamName).ToList();

        return Result<ChallengeAdminSummaryDto>.Success(new ChallengeAdminSummaryDto
        {
            ChallengeId = challenge.Id, ChallengeTitle = challenge.Title, ParticipationMode = ChallengeParticipationMode.TeamOnly,
            PeerReviewEnabled = challenge.PeerReviewEnabled,
            TotalTaskCount = tasks.Count, ParticipantCount = participants.Sum(participant => participant.RosterMembers.Count),
            TotalCompletionCount = participants.Sum(participant => participant.TaskCompletions.Count(completion => completion.IsCompleted)),
            Teams = teams,
            Tasks = tasks.Select(task => new ChallengeAdminTaskProgressDto
            {
                TaskId = task.Id, Title = task.Title, TaskType = (int)task.TaskType, Difficulty = (int)task.Difficulty,
                Score = task.Score, CompletedUserCount = participants.Count(participant => participant.TaskCompletions.Any(row => row.ChallengeTaskId == task.Id && row.IsCompleted))
            }).ToList()
        });
    }

    public async Task<Result<ChallengeCsvExportResult>> ExportAdminUsersCsvAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetAdminSummaryAsync(challengeId, cancellationToken);
        if (summaryResult.IsFailure || summaryResult.Value is null)
        {
            return Result<ChallengeCsvExportResult>.Failure(summaryResult.ErrorMessage ?? "Failed to export challenge users.");
        }

        return Result<ChallengeCsvExportResult>.Success(new ChallengeCsvExportResult
        {
            Content = BuildCsvBytes(BuildAdminUsersCsv(summaryResult.Value)),
            FileName = $"challenge-{challengeId}-users.csv"
        });
    }

    public async Task<Result<ChallengeCsvExportResult>> ExportAdminTasksCsvAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var summaryResult = await GetAdminSummaryAsync(challengeId, cancellationToken);
        if (summaryResult.IsFailure || summaryResult.Value is null)
        {
            return Result<ChallengeCsvExportResult>.Failure(summaryResult.ErrorMessage ?? "Failed to export challenge tasks.");
        }

        return Result<ChallengeCsvExportResult>.Success(new ChallengeCsvExportResult
        {
            Content = BuildCsvBytes(BuildAdminTasksCsv(summaryResult.Value)),
            FileName = $"challenge-{challengeId}-tasks.csv"
        });
    }

    public async Task<Result<ChallengeFileDownloadDto>> GetFileSubmissionDownloadAsync(Guid challengeId, Guid fileSubmissionId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeFileDownloadDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var fileSubmission = await dbContext.ChallengeTaskFileSubmissions
            .AsNoTracking()
            .Include(submission => submission.Challenge)
            .FirstOrDefaultAsync(
                submission => submission.Id == fileSubmissionId && submission.ChallengeId == challengeId,
                cancellationToken);

        if (fileSubmission is null || fileSubmission.Challenge is null)
        {
            return Result<ChallengeFileDownloadDto>.Failure("File submission not found.");
        }

        if (!CanDownloadChallengeFile(userResult.Value, fileSubmission))
        {
            return Result<ChallengeFileDownloadDto>.Failure("Forbidden.");
        }

        string fullPath;
        try
        {
            fullPath = storagePaths.ResolveChallengeFilePath(fileSubmission.StoredFileName);
        }
        catch (InvalidDataException)
        {
            return Result<ChallengeFileDownloadDto>.Failure("Forbidden.");
        }

        if (!File.Exists(fullPath))
        {
            return Result<ChallengeFileDownloadDto>.Failure("File not found.");
        }

        var downloadFileName = Path.GetFileName(fileSubmission.OriginalFileName);
        if (string.IsNullOrWhiteSpace(downloadFileName))
        {
            downloadFileName = "submission.zip";
        }

        return Result<ChallengeFileDownloadDto>.Success(new ChallengeFileDownloadDto
        {
            FilePath = fullPath,
            DownloadFileName = downloadFileName,
            ContentType = string.IsNullOrWhiteSpace(fileSubmission.ContentType)
                ? "application/zip"
                : fileSubmission.ContentType
        });
    }

    public async Task<Result<ChallengeTaskFileSubmissionDto?>> GetMyFileSubmissionAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeTaskFileSubmissionDto?>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var task = await dbContext.ChallengeTasks
            .AsNoTracking()
            .Include(task => task.Challenge)
            .FirstOrDefaultAsync(task => task.Id == taskId && task.ChallengeId == challengeId, cancellationToken);

        if (task is null || task.Challenge is null
            || !visibilityPolicy.CanViewChallenge(userResult.Value.Role, task.Challenge))
        {
            return Result<ChallengeTaskFileSubmissionDto?>.Failure("Challenge task not found.");
        }

        if (task.TaskType != ChallengeTaskType.FileUpload)
        {
            return Result<ChallengeTaskFileSubmissionDto?>.Failure("Challenge task is not a file upload task.");
        }
        if (task.Challenge?.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return Result<ChallengeTaskFileSubmissionDto?>.Failure("Team-only challenges support algorithm tasks only.");
        }

        var fileSubmission = await dbContext.ChallengeTaskFileSubmissions
            .AsNoTracking()
            .Include(submission => submission.ReviewedByUser)
            .FirstOrDefaultAsync(
                submission => submission.ChallengeId == challengeId
                    && submission.ChallengeTaskId == taskId
                    && submission.UserId == userResult.Value.Id,
                cancellationToken);

        return Result<ChallengeTaskFileSubmissionDto?>.Success(fileSubmission is null
            ? null
            : ToFileSubmissionDto(fileSubmission));
    }

    public async Task<Result> ReviewFileSubmissionAsync(Guid challengeId, Guid fileSubmissionId, ReviewChallengeFileSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var fileSubmission = await dbContext.ChallengeTaskFileSubmissions
            .Include(submission => submission.Challenge)
            .Include(submission => submission.ChallengeTask)
            .FirstOrDefaultAsync(
                submission => submission.Id == fileSubmissionId && submission.ChallengeId == challengeId,
                cancellationToken);

        if (fileSubmission is null || fileSubmission.Challenge is null || fileSubmission.ChallengeTask is null)
        {
            return Result.Failure("File submission not found.");
        }

        if (fileSubmission.ChallengeTask.TaskType != ChallengeTaskType.FileUpload)
        {
            return Result.Failure("Challenge task is not a file upload task.");
        }

        if (!CanManageChallenge(userResult.Value, fileSubmission.Challenge))
        {
            return Result.Failure("Forbidden.");
        }

        if (request.Score < 0 || request.Score > fileSubmission.ChallengeTask.Score)
        {
            return Result.Failure("Review score is out of range.");
        }

        if (request.Comment is { Length: > 2000 })
        {
            return Result.Failure("Review comment must not exceed 2000 characters.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        fileSubmission.ReviewScore = request.Score;
        fileSubmission.ReviewComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        fileSubmission.ReviewedByUserId = userResult.Value.Id;
        fileSubmission.ReviewedAt = now;
        fileSubmission.UpdatedAt = now;

        await ChallengeBestScoreStore.UpsertFileIndividualAsync(
            dbContext, challengeId, fileSubmission.ChallengeTaskId, fileSubmission.UserId,
            request.Score, true, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> JoinChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null || !challenge.IsPublished || !visibilityPolicy.CanViewChallenge(userResult.Value.Role, challenge))
        {
            return Result.Failure("Challenge not found.");
        }

        if (!visibilityPolicy.IsChallengeOpen(challenge))
        {
            return Result.Failure("Challenge is not open.");
        }

        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return Result.Failure("Team registration is required.");
        }

        await EnsureParticipantAsync(challengeId, userResult.Value.Id, visibilityPolicy.UtcNow, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public Task<Result<ChallengeTeamParticipationDto>> RegisterTeamAsync(Guid challengeId, CancellationToken cancellationToken = default)
        => RegisterTeamAsync(challengeId, new RegisterChallengeTeamRequest(), cancellationToken);

    public async Task<Result<ChallengeTeamParticipationDto>> RegisterTeamAsync(Guid challengeId, RegisterChallengeTeamRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeTeamParticipationDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);
        if (challenge is null || !visibilityPolicy.CanViewChallenge(userResult.Value.Role, challenge))
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Challenge not found.");
        }
        if (challenge.ParticipationMode != ChallengeParticipationMode.TeamOnly)
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Challenge uses individual participation.");
        }
        if (!visibilityPolicy.IsChallengeOpen(challenge))
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Challenge is not open.");
        }

        var activeMembership = await dbContext.TeamMembers.AsNoTracking()
            .FirstOrDefaultAsync(member => member.UserId == userResult.Value.Id && member.IsActive, cancellationToken);
        if (activeMembership is null)
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Active team membership is required.");
        }
        if (activeMembership.Role != TeamMemberRole.Owner)
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Forbidden.");
        }

        var team = await dbContext.Teams
            .Include(team => team.Members.Where(member => member.IsActive))
                .ThenInclude(member => member.User)
            .FirstOrDefaultAsync(team => team.Id == activeMembership.TeamId && !team.IsDeleted && team.OwnerUserId == userResult.Value.Id, cancellationToken);
        if (team is null || team.Members.All(member => member.UserId != userResult.Value.Id || member.Role != TeamMemberRole.Owner))
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Forbidden.");
        }

        TeamProject? selectedProject = null;
        if (challenge.PeerReviewEnabled)
        {
            if (!request.SelectedTeamProjectId.HasValue)
            {
                return Result<ChallengeTeamParticipationDto>.Failure("Team project is required for peer review.");
            }
            selectedProject = await dbContext.TeamProjects.AsNoTracking()
                .FirstOrDefaultAsync(project => project.Id == request.SelectedTeamProjectId.Value && project.TeamId == team.Id, cancellationToken);
            if (selectedProject is null)
            {
                return Result<ChallengeTeamParticipationDto>.Failure("Team project is invalid for this team.");
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (await dbContext.ChallengeTeamParticipants.AnyAsync(participant => participant.ChallengeId == challengeId && participant.TeamId == team.Id, cancellationToken))
        {
            return Result<ChallengeTeamParticipationDto>.Failure("Team is already registered.");
        }

        var memberIds = team.Members.Select(member => member.UserId).ToList();
        if (await dbContext.ChallengeTeamRosterMembers.AnyAsync(member => member.ChallengeId == challengeId && memberIds.Contains(member.UserId), cancellationToken))
        {
            return Result<ChallengeTeamParticipationDto>.Failure("A team member is already registered with another team.");
        }

        var now = visibilityPolicy.UtcNow;
        var participant = new ChallengeTeamParticipant
        {
            Id = Guid.NewGuid(), ChallengeId = challengeId, TeamId = team.Id, TeamNameSnapshot = team.Name,
            SelectedTeamProjectId = selectedProject?.Id, ProjectNameSnapshot = selectedProject?.Name,
            RepositoryUrlSnapshot = selectedProject?.RepositoryUrl,
            RegisteredByUserId = userResult.Value.Id, RegisteredAt = now
        };
        participant.RosterMembers = team.Members.Where(member => member.IsActive).Select(member => new ChallengeTeamRosterMember
        {
            Id = Guid.NewGuid(), ChallengeId = challengeId, ChallengeTeamParticipantId = participant.Id,
            TeamId = team.Id, UserId = member.UserId, UserNameSnapshot = member.User?.UserName ?? string.Empty,
            TeamMemberRoleSnapshot = member.Role, RegisteredAt = now
        }).ToList();
        dbContext.ChallengeTeamParticipants.Add(participant);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ChallengeTeamParticipationDto>.Failure("Team registration conflicts with an existing registration.");
        }

        return Result<ChallengeTeamParticipationDto>.Success(ToTeamParticipationDto(participant, userResult.Value.Id, false));
    }

    public async Task<Result<ChallengeDetailDto>> CreateChallengeAsync(CreateChallengeRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeDetailDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role is not (UserRole.ProblemSetter or UserRole.Root))
        {
            return Result<ChallengeDetailDto>.Failure("Forbidden.");
        }

        var validation = ValidateChallengeTime(request.StartAt, request.EndAt);
        if (validation.IsFailure)
        {
            return Result<ChallengeDetailDto>.Failure(validation.ErrorMessage ?? "Invalid challenge time.");
        }
        if (!Enum.IsDefined(request.ParticipationMode))
        {
            return Result<ChallengeDetailDto>.Failure("Invalid participation mode.");
        }
        var peerReviewValidation = ValidatePeerReviewConfiguration(request.ParticipationMode, request.PeerReviewEnabled, request.PeerReviewEndAt, request.EndAt);
        if (peerReviewValidation.IsFailure)
        {
            return Result<ChallengeDetailDto>.Failure(peerReviewValidation.ErrorMessage!);
        }

        LeaderboardSeason? linkedSeason = null;
        if (request.SeasonId.HasValue)
        {
            if (userResult.Value.Role != UserRole.Root) return Result<ChallengeDetailDto>.Failure("Forbidden.");
            linkedSeason = await dbContext.LeaderboardSeasons
                .FirstOrDefaultAsync(season => season.Id == request.SeasonId.Value && season.IsCurrent, cancellationToken);
            if (linkedSeason is null) return Result<ChallengeDetailDto>.Failure("Leaderboard season not found.");
            if (linkedSeason.Status != LeaderboardSeasonStatus.Scheduled
                || visibilityPolicy.UtcNow >= linkedSeason.StartAt
                || request.StartAt < linkedSeason.StartAt
                || request.EndAt > linkedSeason.FreezeAt)
            {
                return Result<ChallengeDetailDto>.Failure("Challenge must stay within the scheduled season time range.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            CreatedByUserId = userResult.Value.Id,
            IsPublished = request.IsPublished,
            ParticipationMode = request.ParticipationMode,
            PeerReviewEnabled = request.PeerReviewEnabled,
            PeerReviewEndAt = request.PeerReviewEnabled ? request.PeerReviewEndAt : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Challenges.Add(challenge);
        if (linkedSeason is not null)
        {
            dbContext.LeaderboardSeasonBoards.Add(new LeaderboardSeasonBoard
            {
                Id = Guid.NewGuid(),
                SeasonId = linkedSeason.Id,
                BoardType = LeaderboardSeasonBoardType.Challenge,
                ChallengeId = challenge.Id,
                CreatedAt = now
            });
        }
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ChallengeCreated, "Challenge", challenge.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ChallengeDetailDto>.Success(ToDetailDto(challenge, new Dictionary<Guid, ChallengeTaskCompletion>(), userResult.Value.Role));
    }

    public async Task<Result<ChallengeDetailDto>> UpdateChallengeAsync(Guid id, UpdateChallengeRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeDetailDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .Include(challenge => challenge.Tasks)
            .FirstOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeDetailDto>.Failure("Challenge not found.");
        }

        if (!CanManageChallenge(userResult.Value, challenge))
        {
            return Result<ChallengeDetailDto>.Failure("Forbidden.");
        }

        if (!CanModifyAfterEnd(userResult.Value, challenge))
        {
            return Result<ChallengeDetailDto>.Failure("Challenge has ended.");
        }

        var validation = ValidateChallengeTime(request.StartAt, request.EndAt);
        if (validation.IsFailure)
        {
            return Result<ChallengeDetailDto>.Failure(validation.ErrorMessage ?? "Invalid challenge time.");
        }

        if (!Enum.IsDefined(request.ParticipationMode))
        {
            return Result<ChallengeDetailDto>.Failure("Invalid participation mode.");
        }
        var peerReviewValidation = ValidatePeerReviewConfiguration(request.ParticipationMode, request.PeerReviewEnabled, request.PeerReviewEndAt, request.EndAt);
        if (peerReviewValidation.IsFailure)
        {
            return Result<ChallengeDetailDto>.Failure(peerReviewValidation.ErrorMessage!);
        }
        var linkedSeasonRanges = await dbContext.LeaderboardSeasonBoards.AsNoTracking()
            .Where(board => board.ChallengeId == id && board.BoardType == LeaderboardSeasonBoardType.Challenge)
            .Select(board => new { board.Season!.StartAt, board.Season.FreezeAt })
            .ToListAsync(cancellationToken);
        if (linkedSeasonRanges.Any(season => request.StartAt < season.StartAt || request.EndAt > season.FreezeAt))
        {
            return Result<ChallengeDetailDto>.Failure("Challenge leaderboard must stay within the season time range.");
        }
        var peerReviewChanged = challenge.PeerReviewEnabled != request.PeerReviewEnabled
            || challenge.PeerReviewEndAt != (request.PeerReviewEnabled ? request.PeerReviewEndAt : null);
        if (peerReviewChanged && await IsPeerReviewConfigurationLockedAsync(challenge, cancellationToken))
        {
            return Result<ChallengeDetailDto>.Failure("Peer review configuration is locked.");
        }
        if (challenge.ParticipationMode != request.ParticipationMode)
        {
            var modeLocked = challenge.IsPublished
                || visibilityPolicy.UtcNow >= challenge.StartAt
                || await dbContext.ChallengeParticipants.AnyAsync(participant => participant.ChallengeId == id, cancellationToken)
                || await dbContext.ChallengeTeamParticipants.AnyAsync(participant => participant.ChallengeId == id, cancellationToken)
                || await dbContext.Submissions.AnyAsync(submission => submission.ChallengeTask != null && submission.ChallengeTask.ChallengeId == id, cancellationToken);
            if (modeLocked)
            {
                return Result<ChallengeDetailDto>.Failure("Participation mode is locked.");
            }
            if (request.ParticipationMode == ChallengeParticipationMode.TeamOnly
                && challenge.Tasks.Any(task => task.TaskType == ChallengeTaskType.FileUpload))
            {
                return Result<ChallengeDetailDto>.Failure("Team-only challenges support algorithm tasks only.");
            }
        }

        challenge.Title = request.Title;
        challenge.Description = request.Description;
        challenge.StartAt = request.StartAt;
        challenge.EndAt = request.EndAt;
        challenge.IsPublished = request.IsPublished;
        challenge.ParticipationMode = request.ParticipationMode;
        challenge.PeerReviewEnabled = request.PeerReviewEnabled;
        challenge.PeerReviewEndAt = request.PeerReviewEnabled ? request.PeerReviewEndAt : null;
        challenge.UpdatedAt = DateTimeOffset.UtcNow;

        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ChallengeUpdated, "Challenge", challenge.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        var completions = await GetCurrentUserCompletionsAsync([challenge.Id], cancellationToken);

        return Result<ChallengeDetailDto>.Success(ToDetailDto(challenge, completions, userResult.Value.Role));
    }

    public async Task<Result> DeleteChallengeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .FirstOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);

        if (challenge is null)
        {
            return Result.Failure("Challenge not found.");
        }

        if (!CanManageChallenge(userResult.Value, challenge))
        {
            return Result.Failure("Forbidden.");
        }

        if (!CanModifyAfterEnd(userResult.Value, challenge))
        {
            return Result.Failure("Challenge has ended.");
        }

        dbContext.Challenges.Remove(challenge);
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ChallengeDeleted, "Challenge", challenge.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ChallengeTaskDto>> AddTaskAsync(Guid challengeId, CreateChallengeTaskRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeTaskDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var challenge = await dbContext.Challenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

        if (challenge is null)
        {
            return Result<ChallengeTaskDto>.Failure("Challenge not found.");
        }

        if (!CanManageChallenge(userResult.Value, challenge))
        {
            return Result<ChallengeTaskDto>.Failure("Forbidden.");
        }

        if (!CanModifyAfterEnd(userResult.Value, challenge))
        {
            return Result<ChallengeTaskDto>.Failure("Challenge has ended.");
        }

        var validation = await ValidateCreateTaskAsync(challengeId, request, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<ChallengeTaskDto>.Failure(validation.ErrorMessage ?? "Invalid challenge task.");
        }
        if (challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly && request.TaskType != ChallengeTaskType.Algorithm)
        {
            return Result<ChallengeTaskDto>.Failure("Team-only challenges support algorithm tasks only.");
        }

        var now = DateTimeOffset.UtcNow;
        var task = new ChallengeTask
        {
            Id = Guid.NewGuid(),
            ChallengeId = challengeId,
            Title = request.Title,
            Description = request.Description,
            TaskType = request.TaskType,
            Difficulty = request.Difficulty,
            BoardX = request.BoardX,
            BoardY = request.BoardY,
            AlgorithmProblemId = request.AlgorithmProblemId,
            Score = request.Score,
            IsPublished = request.IsPublished,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ChallengeTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ChallengeTaskDto>.Success(ToTaskDto(task, null));
    }

    public async Task<Result<ChallengeTaskDto>> UpdateTaskAsync(Guid challengeId, Guid taskId, UpdateChallengeTaskRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeTaskDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var task = await dbContext.ChallengeTasks
            .Include(task => task.Challenge)
            .FirstOrDefaultAsync(task => task.Id == taskId && task.ChallengeId == challengeId, cancellationToken);

        if (task is null || task.Challenge is null)
        {
            return Result<ChallengeTaskDto>.Failure("Challenge task not found.");
        }

        if (!CanManageChallenge(userResult.Value, task.Challenge!))
        {
            return Result<ChallengeTaskDto>.Failure("Forbidden.");
        }

        if (!CanModifyAfterEnd(userResult.Value, task.Challenge))
        {
            return Result<ChallengeTaskDto>.Failure("Challenge has ended.");
        }

        var validation = await ValidateUpdateTaskAsync(task, request, cancellationToken);
        if (validation.IsFailure)
        {
            return Result<ChallengeTaskDto>.Failure(validation.ErrorMessage ?? "Invalid challenge task.");
        }

        task.Title = request.Title;
        task.Description = request.Description;
        task.Difficulty = request.Difficulty;
        task.BoardX = request.BoardX;
        task.BoardY = request.BoardY;
        task.AlgorithmProblemId = request.AlgorithmProblemId;
        task.Score = request.Score;
        task.IsPublished = request.IsPublished;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var completion = await GetCurrentUserCompletionAsync(task.Id, cancellationToken);

        return Result<ChallengeTaskDto>.Success(ToTaskDto(task, completion));
    }

    public async Task<Result> DeleteTaskAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var task = await dbContext.ChallengeTasks
            .Include(task => task.Challenge)
            .FirstOrDefaultAsync(task => task.Id == taskId && task.ChallengeId == challengeId, cancellationToken);

        if (task is null || task.Challenge is null)
        {
            return Result.Failure("Challenge task not found.");
        }

        if (!CanManageChallenge(userResult.Value, task.Challenge))
        {
            return Result.Failure("Forbidden.");
        }

        if (!CanModifyAfterEnd(userResult.Value, task.Challenge))
        {
            return Result.Failure("Challenge has ended.");
        }

        dbContext.ChallengeTasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ChallengeTaskFileSubmissionDto>> SubmitFileAnswerAsync(Guid challengeId, Guid taskId, SubmitChallengeTaskFileRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var fileValidation = ValidateZipFile(request);
        if (fileValidation.IsFailure)
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure(fileValidation.ErrorMessage ?? "Invalid file.");
        }

        var task = await dbContext.ChallengeTasks
            .Include(task => task.Challenge)
            .FirstOrDefaultAsync(task => task.Id == taskId && task.ChallengeId == challengeId, cancellationToken);

        if (task is null || task.Challenge is null)
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure("Challenge task not found.");
        }

        if (task.TaskType != ChallengeTaskType.FileUpload)
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure("Challenge task is not a file upload task.");
        }
        if (task.Challenge?.ParticipationMode == ChallengeParticipationMode.TeamOnly)
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure("Team-only challenges support algorithm tasks only.");
        }

        if (!visibilityPolicy.CanViewChallenge(userResult.Value.Role, task.Challenge!))
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure("Challenge task not found.");
        }

        if (!visibilityPolicy.IsChallengeOpen(task.Challenge!))
        {
            return Result<ChallengeTaskFileSubmissionDto>.Failure("Challenge is not open.");
        }

        var now = DateTimeOffset.UtcNow;
        await EnsureParticipantAsync(challengeId, userResult.Value.Id, now, cancellationToken);

        var storedFileName = $"{Guid.NewGuid():N}.zip";
        var fullFilePath = storagePaths.ResolveChallengeFilePath(storedFileName);

        await using (var output = storagePaths.CreateChallengeFileWriteStream(storedFileName))
        {
            await request.FileStream.CopyToAsync(output, cancellationToken);
        }

        var fileSubmission = await dbContext.ChallengeTaskFileSubmissions
            .FirstOrDefaultAsync(
                submission => submission.UserId == userResult.Value.Id && submission.ChallengeTaskId == taskId,
                cancellationToken);

        if (fileSubmission is null)
        {
            fileSubmission = new ChallengeTaskFileSubmission
            {
                Id = Guid.NewGuid(),
                ChallengeId = challengeId,
                ChallengeTaskId = taskId,
                UserId = userResult.Value.Id,
                OriginalFileName = Path.GetFileName(request.OriginalFileName),
                StoredFileName = storedFileName,
                FilePath = fullFilePath,
                FileSizeBytes = request.FileSizeBytes,
                ContentType = request.ContentType,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.ChallengeTaskFileSubmissions.Add(fileSubmission);
        }
        else
        {
            TryDeleteStoredChallengeFile(fileSubmission.StoredFileName);
            fileSubmission.OriginalFileName = Path.GetFileName(request.OriginalFileName);
            fileSubmission.StoredFileName = storedFileName;
            fileSubmission.FilePath = fullFilePath;
            fileSubmission.FileSizeBytes = request.FileSizeBytes;
            fileSubmission.ContentType = request.ContentType;
            fileSubmission.ReviewScore = null;
            fileSubmission.ReviewComment = null;
            fileSubmission.ReviewedByUserId = null;
            fileSubmission.ReviewedAt = null;
            fileSubmission.UpdatedAt = now;
        }

        await ChallengeBestScoreStore.UpsertFileIndividualAsync(
            dbContext, challengeId, taskId, userResult.Value.Id, 0, false, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ChallengeTaskFileSubmissionDto>.Success(ToFileSubmissionDto(fileSubmission));
    }

    public async Task<Result> WithdrawMyFileSubmissionAsync(Guid challengeId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var task = await dbContext.ChallengeTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == taskId && task.ChallengeId == challengeId, cancellationToken);

        if (task is null)
        {
            return Result.Failure("Challenge task not found.");
        }

        if (task.TaskType != ChallengeTaskType.FileUpload)
        {
            return Result.Failure("Challenge task is not a file upload task.");
        }

        var fileSubmission = await dbContext.ChallengeTaskFileSubmissions
            .FirstOrDefaultAsync(
                submission => submission.ChallengeId == challengeId
                    && submission.ChallengeTaskId == taskId
                    && submission.UserId == userResult.Value.Id,
                cancellationToken);

        if (fileSubmission is null)
        {
            return Result.Failure("File submission not found.");
        }

        if (IsFileSubmissionReviewed(fileSubmission))
        {
            return Result.Failure("This file submission has already been reviewed and cannot be withdrawn.");
        }

        var storedFileName = fileSubmission.StoredFileName;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var completion = await dbContext.ChallengeTaskCompletions
            .FirstOrDefaultAsync(
                completion => completion.ChallengeId == challengeId
                    && completion.ChallengeTaskId == taskId
                    && completion.UserId == userResult.Value.Id,
                cancellationToken);

        if (completion is { Score: <= 0, IsCompleted: false })
        {
            dbContext.ChallengeTaskCompletions.Remove(completion);
        }

        dbContext.ChallengeTaskFileSubmissions.Remove(fileSubmission);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TryDeleteStoredChallengeFile(storedFileName);

        return Result.Success();
    }

    private async Task<Result<User>> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<User>.Failure("Account is blacklisted.");
        }

        return Result<User>.Success(user);
    }

    private async Task<Result<User?>> GetOptionalCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User?>.Success(null);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<User?>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<User?>.Failure("Account is blacklisted.");
        }

        return Result<User?>.Success(user);
    }

    private async Task<UserRole?> GetVisibilityRoleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && !user.IsBlacklisted && !user.IsDeleted)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, ChallengeTaskCompletion>> GetCurrentUserCompletionsAsync(IEnumerable<Guid> challengeIds, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return [];
        }

        var ids = challengeIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.UserId == userId && ids.Contains(completion.ChallengeId))
            .ToDictionaryAsync(completion => completion.ChallengeTaskId, cancellationToken);
    }

    private async Task<ChallengeTaskCompletion?> GetCurrentUserCompletionAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .FirstOrDefaultAsync(completion => completion.UserId == userId && completion.ChallengeTaskId == taskId, cancellationToken);
    }

    private async Task<Result> ValidateCreateTaskAsync(Guid challengeId, CreateChallengeTaskRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.TaskType))
        {
            return Result.Failure("Invalid challenge task type.");
        }

        if (!Enum.IsDefined(request.Difficulty))
        {
            return Result.Failure("Invalid challenge task difficulty.");
        }

        var commonValidation = await ValidateTaskCommonAsync(challengeId, null, request.TaskType, request.BoardX, request.BoardY, request.AlgorithmProblemId, cancellationToken);
        if (commonValidation.IsFailure)
        {
            return commonValidation;
        }

        return Result.Success();
    }

    private async Task<Result> ValidateUpdateTaskAsync(ChallengeTask task, UpdateChallengeTaskRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Difficulty))
        {
            return Result.Failure("Invalid challenge task difficulty.");
        }

        return await ValidateTaskCommonAsync(task.ChallengeId, task.Id, task.TaskType, request.BoardX, request.BoardY, request.AlgorithmProblemId, cancellationToken);
    }

    private async Task<Result> ValidateTaskCommonAsync(Guid challengeId, Guid? taskId, ChallengeTaskType taskType, int boardX, int boardY, Guid? algorithmProblemId, CancellationToken cancellationToken)
    {
        if (boardX is < 0 or > 7 || boardY is < 0 or > 7)
        {
            return Result.Failure("Board position must be between 0 and 7.");
        }

        var positionExists = await dbContext.ChallengeTasks
            .AsNoTracking()
            .AnyAsync(task =>
                task.ChallengeId == challengeId
                && task.BoardX == boardX
                && task.BoardY == boardY
                && (!taskId.HasValue || task.Id != taskId.Value),
                cancellationToken);

        if (positionExists)
        {
            return Result.Failure("Board position is already occupied.");
        }

        if (taskType == ChallengeTaskType.Algorithm)
        {
            if (!algorithmProblemId.HasValue)
            {
                return Result.Failure("AlgorithmProblemId is required for Algorithm task.");
            }

            var problemExists = await dbContext.Problems
                .AsNoTracking()
                .AnyAsync(problem => problem.Id == algorithmProblemId.Value && !problem.IsDeleted, cancellationToken);

            return problemExists
                ? Result.Success()
                : Result.Failure("Algorithm problem not found.");
        }

        if (taskType == ChallengeTaskType.FileUpload && algorithmProblemId.HasValue)
        {
            return Result.Failure("AlgorithmProblemId must be empty for file upload task.");
        }

        return Result.Success();
    }

    private static Result ValidateChallengeTime(DateTimeOffset startAt, DateTimeOffset endAt)
    {
        return startAt < endAt
            ? Result.Success()
            : Result.Failure("StartAt must be earlier than EndAt.");
    }

    private static Result ValidatePeerReviewConfiguration(
        ChallengeParticipationMode participationMode,
        bool peerReviewEnabled,
        DateTimeOffset? peerReviewEndAt,
        DateTimeOffset challengeEndAt)
    {
        if (!peerReviewEnabled) return Result.Success();
        if (participationMode != ChallengeParticipationMode.TeamOnly)
        {
            return Result.Failure("Peer review requires team-only participation.");
        }
        if (!peerReviewEndAt.HasValue)
        {
            return Result.Failure("Peer review deadline is required.");
        }
        return peerReviewEndAt.Value > challengeEndAt
            ? Result.Success()
            : Result.Failure("Peer review deadline must be after challenge end.");
    }

    private static bool CanManageChallenge(User user, Challenge challenge)
    {
        return user.Role == UserRole.Root || user.Role == UserRole.ProblemSetter && challenge.CreatedByUserId == user.Id;
    }

    private bool CanManageChallengeForCurrentUser(Challenge challenge, UserRole? role)
    {
        return role == UserRole.Root
            || role == UserRole.ProblemSetter
                && currentUser.UserId.HasValue
                && challenge.CreatedByUserId == currentUser.UserId.Value;
    }

    private static bool CanDownloadChallengeFile(User user, ChallengeTaskFileSubmission fileSubmission)
    {
        return user.Role == UserRole.Root
            || user.Role == UserRole.ProblemSetter
            || fileSubmission.UserId == user.Id;
    }

    private bool CanViewLeaderboard(User? user, Challenge challenge)
    {
        return visibilityPolicy.CanViewChallenge(user?.Role, challenge);
    }

    private async Task<bool> CanViewSelectedLeaderboardAsync(User? user, Guid challengeId, CancellationToken cancellationToken)
    {
        if (user?.Role is UserRole.ProblemSetter or UserRole.Root) return true;
        return await dbContext.LeaderboardSeasonBoards.AsNoTracking().AnyAsync(
            board => board.BoardType == LeaderboardSeasonBoardType.Challenge
                && board.ChallengeId == challengeId
                && board.Season!.IsCurrent
                && board.Season.Status != LeaderboardSeasonStatus.Archived,
            cancellationToken);
    }

    private bool CanModifyAfterEnd(User user, Challenge challenge)
    {
        return user.Role == UserRole.Root || visibilityPolicy.UtcNow <= challenge.EndAt;
    }

    private Result ValidateZipFile(SubmitChallengeTaskFileRequest request)
    {
        if (request.FileStream == Stream.Null || request.FileSizeBytes <= 0)
        {
            return Result.Failure("File is required.");
        }

        if (request.FileSizeBytes > MaxFileSubmissionSizeBytes)
        {
            return Result.Failure("File size must not exceed 50MB.");
        }

        var extension = Path.GetExtension(request.OriginalFileName);
        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("Only .zip files are allowed.");
        }

        if (!AllowedZipContentTypes.Contains(request.ContentType))
        {
            return Result.Failure("Unsupported file content type.");
        }

        return Result.Success();
    }

    private void TryDeleteStoredChallengeFile(string storedFileName)
    {
        try
        {
            var filePath = storagePaths.ResolveChallengeFilePath(storedFileName);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (InvalidDataException)
        {
        }
        catch (IOException)
        {
            // 撤回的业务结果以数据库记录为准；物理文件删除失败时保留孤儿文件，避免影响用户撤回流程。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上，避免上传目录权限波动导致撤回接口失败。
        }
    }

    private static bool IsFileSubmissionReviewed(ChallengeTaskFileSubmission fileSubmission)
    {
        return fileSubmission.ReviewScore.HasValue
            || fileSubmission.ReviewedAt.HasValue
            || fileSubmission.ReviewedByUserId.HasValue;
    }

    private static int CountCompletedTasks(Challenge challenge, IReadOnlyDictionary<Guid, ChallengeTaskCompletion> completions)
    {
        return challenge.Tasks.Count(task => completions.TryGetValue(task.Id, out var completion) && completion.IsCompleted);
    }

    private ChallengeDetailDto ToDetailDto(Challenge challenge, IReadOnlyDictionary<Guid, ChallengeTaskCompletion> completions, UserRole? role)
    {
        return new ChallengeDetailDto
        {
            Id = challenge.Id,
            Title = challenge.Title,
            Description = challenge.Description,
            StartAt = challenge.StartAt,
            EndAt = challenge.EndAt,
            CreatedByUserId = challenge.CreatedByUserId,
            IsPublished = challenge.IsPublished,
            ParticipationMode = challenge.ParticipationMode,
            ParticipationModeLocked = challenge.IsPublished || visibilityPolicy.UtcNow >= challenge.StartAt,
            PeerReviewEnabled = challenge.PeerReviewEnabled,
            PeerReviewEndAt = challenge.PeerReviewEndAt,
            PeerReviewConfigurationLocked = challenge.IsPublished || visibilityPolicy.UtcNow >= challenge.StartAt,
            CreatedAt = challenge.CreatedAt,
            UpdatedAt = challenge.UpdatedAt,
            TotalTaskCount = challenge.Tasks.Count,
            CompletedTaskCount = CountCompletedTasks(challenge, completions),
            CanManage = CanManageChallengeForCurrentUser(challenge, role),
            Tasks = challenge.Tasks
                .OrderBy(task => task.BoardY)
                .ThenBy(task => task.BoardX)
                .Select(task => ToTaskDto(task, completions.GetValueOrDefault(task.Id)))
                .ToList()
        };
    }

    private async Task PopulateTeamParticipationAsync(ChallengeDetailDto detail, CancellationToken cancellationToken)
    {
        if (detail.ParticipationMode != ChallengeParticipationMode.TeamOnly || !currentUser.IsAuthenticated || currentUser.UserId is not { } userId) return;

        var participant = await dbContext.ChallengeTeamParticipants
            .AsNoTracking()
            .Where(participant => participant.ChallengeId == detail.Id
                && participant.RosterMembers.Any(member => member.UserId == userId))
            .Include(participant => participant.RosterMembers)
            .FirstOrDefaultAsync(cancellationToken);
        if (participant is not null)
        {
            detail.TeamParticipation = ToTeamParticipationDto(participant, userId, false);
            var teamCompletions = await dbContext.ChallengeTeamTaskCompletions.AsNoTracking()
                .Where(completion => completion.ChallengeTeamParticipantId == participant.Id)
                .ToDictionaryAsync(completion => completion.ChallengeTaskId, cancellationToken);
            detail.CompletedTaskCount = teamCompletions.Values.Count(completion => completion.IsCompleted);
            foreach (var task in detail.Tasks)
            {
                if (!teamCompletions.TryGetValue(task.Id, out var completion)) continue;
                task.IsCompleted = completion.IsCompleted;
                task.CompletedAt = completion.IsCompleted ? completion.CompletedAt : null;
                task.CompletedScore = completion.IsCompleted ? completion.Score : null;
                task.EarnedScore = completion.Score;
            }
            return;
        }

        var currentTeamId = await dbContext.TeamMembers.AsNoTracking()
            .Where(member => member.UserId == userId && member.IsActive)
            .Select(member => (Guid?)member.TeamId)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentTeamId.HasValue)
        {
            var registeredTeam = await dbContext.ChallengeTeamParticipants.AsNoTracking()
                .Include(participant => participant.RosterMembers)
                .FirstOrDefaultAsync(participant => participant.ChallengeId == detail.Id && participant.TeamId == currentTeamId.Value, cancellationToken);
            if (registeredTeam is not null)
            {
                detail.TeamParticipation = ToTeamParticipationDto(registeredTeam, userId, false);
                return;
            }
        }

        var canRegister = await dbContext.TeamMembers.AsNoTracking()
            .AnyAsync(member => member.UserId == userId && member.IsActive && member.Role == TeamMemberRole.Owner
                && member.Team != null && !member.Team.IsDeleted && member.Team.OwnerUserId == userId, cancellationToken);
        detail.TeamParticipation = new ChallengeTeamParticipationDto { CanRegisterTeam = canRegister };
    }

    private async Task<bool> IsParticipationModeLockedAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        if (challenge.IsPublished || visibilityPolicy.UtcNow >= challenge.StartAt) return true;
        return await dbContext.ChallengeParticipants.AnyAsync(participant => participant.ChallengeId == challenge.Id, cancellationToken)
            || await dbContext.ChallengeTeamParticipants.AnyAsync(participant => participant.ChallengeId == challenge.Id, cancellationToken)
            || await dbContext.Submissions.AnyAsync(submission => submission.ChallengeTask != null && submission.ChallengeTask.ChallengeId == challenge.Id, cancellationToken);
    }

    private async Task<bool> IsPeerReviewConfigurationLockedAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        return challenge.IsPublished
            || visibilityPolicy.UtcNow >= challenge.StartAt
            || await dbContext.ChallengeTeamParticipants.AnyAsync(participant => participant.ChallengeId == challenge.Id, cancellationToken);
    }

    private static ChallengeTeamParticipationDto ToTeamParticipationDto(ChallengeTeamParticipant participant, Guid userId, bool canRegister)
    {
        return new ChallengeTeamParticipationDto
        {
            Id = participant.Id, TeamId = participant.TeamId, TeamName = participant.TeamNameSnapshot,
            RegisteredAt = participant.RegisteredAt, RosterMemberCount = participant.RosterMembers.Count,
            IsRosterMember = participant.RosterMembers.Any(member => member.UserId == userId), CanRegisterTeam = canRegister,
            SelectedTeamProjectId = participant.SelectedTeamProjectId, ProjectName = participant.ProjectNameSnapshot,
            RepositoryUrl = participant.RepositoryUrlSnapshot
        };
    }

    private async Task<Result<ChallengeLeaderboardDto>> GetTeamLeaderboardAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        var totalTaskCount = await dbContext.ChallengeTasks.AsNoTracking().CountAsync(task => task.ChallengeId == challenge.Id, cancellationToken);
        var participants = await dbContext.ChallengeTeamParticipants.AsNoTracking()
            .Where(participant => participant.ChallengeId == challenge.Id)
            .Include(participant => participant.TaskCompletions)
            .ToListAsync(cancellationToken);
        var ordered = participants.Select(participant => new
            {
                Participant = participant,
                Completed = participant.TaskCompletions.Count(completion => completion.IsCompleted),
                Score = participant.TaskCompletions.Sum(completion => completion.Score),
                UpdatedAt = participant.TaskCompletions.Count == 0 ? (DateTimeOffset?)null : participant.TaskCompletions.Max(completion => completion.UpdatedAt)
            })
            .OrderByDescending(entry => entry.Score).ThenByDescending(entry => entry.Completed)
            .ThenBy(entry => entry.UpdatedAt).ThenBy(entry => entry.Participant.TeamNameSnapshot).ToList();
        return Result<ChallengeLeaderboardDto>.Success(new ChallengeLeaderboardDto
        {
            ChallengeId = challenge.Id, ChallengeTitle = challenge.Title, TotalTaskCount = totalTaskCount,
            ParticipationMode = ChallengeParticipationMode.TeamOnly,
            TeamEntries = ordered.Select((entry, index) => new ChallengeTeamLeaderboardEntryDto
            {
                Rank = index + 1, TeamParticipantId = entry.Participant.Id, TeamName = entry.Participant.TeamNameSnapshot,
                CompletedTaskCount = entry.Completed, TotalScore = entry.Score, LastImprovedAt = entry.UpdatedAt
            }).ToList()
        });
    }

    private async Task<Result<ChallengeLeaderboardProgressDto>> GetTeamLeaderboardProgressAsync(Challenge challenge, CancellationToken cancellationToken)
    {
        var leaderboard = await GetTeamLeaderboardAsync(challenge, cancellationToken);
        var tasks = await dbContext.ChallengeTasks.AsNoTracking().Where(task => task.ChallengeId == challenge.Id)
            .OrderBy(task => task.BoardY).ThenBy(task => task.BoardX).ToListAsync(cancellationToken);
        var completions = await dbContext.ChallengeTeamTaskCompletions.AsNoTracking()
            .Where(completion => completion.ChallengeId == challenge.Id).ToListAsync(cancellationToken);
        return Result<ChallengeLeaderboardProgressDto>.Success(new ChallengeLeaderboardProgressDto
        {
            ChallengeId = challenge.Id, ChallengeTitle = challenge.Title, ParticipationMode = ChallengeParticipationMode.TeamOnly,
            Tasks = tasks.Select(task => new ChallengeLeaderboardProgressTaskDto { TaskId = task.Id, Title = task.Title, Score = task.Score }).ToList(),
            Teams = leaderboard.Value!.TeamEntries.Select(entry =>
            {
                var rows = completions.Where(completion => completion.ChallengeTeamParticipantId == entry.TeamParticipantId).ToList();
                return new ChallengeTeamLeaderboardProgressDto
                {
                    TeamParticipantId = entry.TeamParticipantId, TeamName = entry.TeamName, Rank = entry.Rank,
                    CompletedTaskCount = entry.CompletedTaskCount, TotalScore = entry.TotalScore, LastImprovedAt = entry.LastImprovedAt,
                    CompletedTaskIds = rows.Where(row => row.IsCompleted).Select(row => row.ChallengeTaskId).ToList(),
                    TaskScores = rows.ToDictionary(row => row.ChallengeTaskId, row => row.Score)
                };
            }).ToList()
        });
    }

    private static ChallengeTaskDto ToTaskDto(ChallengeTask task, ChallengeTaskCompletion? completion)
    {
        return new ChallengeTaskDto
        {
            Id = task.Id,
            ChallengeId = task.ChallengeId,
            Title = task.Title,
            Description = task.Description,
            TaskType = task.TaskType,
            Difficulty = task.Difficulty,
            BoardX = task.BoardX,
            BoardY = task.BoardY,
            AlgorithmProblemId = task.AlgorithmProblemId,
            Score = task.Score,
            IsPublished = task.IsPublished,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            IsCompleted = completion?.IsCompleted == true,
            CompletedAt = completion?.IsCompleted == true ? completion.CompletedAt : null,
            CompletedScore = completion?.IsCompleted == true ? completion.Score : null,
            EarnedScore = completion?.Score ?? 0
        };
    }

    private static ChallengeAdminUserTaskStatusDto ToAdminUserTaskStatusDto(
        ChallengeTask task,
        ChallengeTaskCompletion? completion,
        ChallengeTaskFileSubmission? fileSubmission)
    {
        return new ChallengeAdminUserTaskStatusDto
        {
            TaskId = task.Id,
            TaskTitle = task.Title,
            TaskType = (int)task.TaskType,
            Difficulty = (int)task.Difficulty,
            Score = task.Score,
            IsCompleted = completion?.IsCompleted == true,
            CompletedAt = completion?.IsCompleted == true ? completion.CompletedAt : null,
            CompletedScore = completion?.IsCompleted == true ? completion.Score : null,
            EarnedScore = completion?.Score ?? 0,
            SubmissionId = completion?.SubmissionId,
            FileSubmissionId = fileSubmission?.Id,
            OriginalFileName = fileSubmission?.OriginalFileName,
            FileSizeBytes = fileSubmission?.FileSizeBytes,
            ReviewScore = fileSubmission?.ReviewScore,
            ReviewComment = fileSubmission?.ReviewComment,
            ReviewedByUserId = fileSubmission?.ReviewedByUserId,
            ReviewedByUserName = fileSubmission?.ReviewedByUser?.UserName,
            ReviewedAt = fileSubmission?.ReviewedAt,
            IsReviewed = fileSubmission?.ReviewScore.HasValue == true
        };
    }

    private static ChallengeTaskFileSubmissionDto ToFileSubmissionDto(ChallengeTaskFileSubmission fileSubmission)
    {
        return new ChallengeTaskFileSubmissionDto
        {
            Id = fileSubmission.Id,
            ChallengeId = fileSubmission.ChallengeId,
            ChallengeTaskId = fileSubmission.ChallengeTaskId,
            UserId = fileSubmission.UserId,
            OriginalFileName = fileSubmission.OriginalFileName,
            FileSizeBytes = fileSubmission.FileSizeBytes,
            CreatedAt = fileSubmission.CreatedAt,
            UpdatedAt = fileSubmission.UpdatedAt,
            ReviewScore = fileSubmission.ReviewScore,
            ReviewComment = fileSubmission.ReviewComment,
            ReviewedByUserId = fileSubmission.ReviewedByUserId,
            ReviewedByUserName = fileSubmission.ReviewedByUser?.UserName,
            ReviewedAt = fileSubmission.ReviewedAt,
            IsReviewed = IsFileSubmissionReviewed(fileSubmission),
            CanWithdrawSubmission = !IsFileSubmissionReviewed(fileSubmission)
        };
    }

    private static string BuildAdminUsersCsv(ChallengeAdminSummaryDto summary)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, ["用户ID", "用户名", "完成题数", "总分", "最后完成时间"]);

        foreach (var user in summary.Users)
        {
            AppendCsvRow(builder, [
                user.UserId.ToString(),
                user.UserName,
                user.CompletedTaskCount.ToString(),
                user.TotalScore.ToString(),
                FormatCsvDate(user.LastCompletedAt)
            ]);
        }

        return builder.ToString();
    }

    private static string BuildAdminTasksCsv(ChallengeAdminSummaryDto summary)
    {
        var builder = new StringBuilder();
        AppendCsvRow(builder, [
            "用户ID",
            "用户名",
            "题目ID",
            "题目名称",
            "题目类型",
            "难度",
            "满分",
            "完成状态",
            "得分",
            "完成时间",
            "SubmissionId",
            "FileSubmissionId",
            "文件名",
            "文件大小",
            "评分状态",
            "评分分数",
            "评分评语",
            "评分人",
            "评分时间"
        ]);

        foreach (var user in summary.Users)
        {
            foreach (var status in user.TaskStatuses)
            {
                AppendCsvRow(builder, [
                    user.UserId.ToString(),
                    user.UserName,
                    status.TaskId.ToString(),
                    status.TaskTitle,
                    FormatTaskType(status.TaskType),
                    FormatDifficulty(status.Difficulty),
                    status.Score.ToString(),
                    status.IsCompleted ? "已完成" : "未完成",
                    status.CompletedScore?.ToString() ?? string.Empty,
                    FormatCsvDate(status.CompletedAt),
                    status.SubmissionId?.ToString() ?? string.Empty,
                    status.FileSubmissionId?.ToString() ?? string.Empty,
                    status.OriginalFileName ?? string.Empty,
                    status.FileSizeBytes?.ToString() ?? string.Empty,
                    status.IsReviewed ? "已评分" : "未评分",
                    status.ReviewScore?.ToString() ?? string.Empty,
                    status.ReviewComment ?? string.Empty,
                    status.ReviewedByUserName ?? string.Empty,
                    FormatCsvDate(status.ReviewedAt)
                ]);
            }
        }

        return builder.ToString();
    }

    private static byte[] BuildCsvBytes(string csv)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var content = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, content, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, content, preamble.Length, body.Length);
        return content;
    }

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string?> values)
    {
        builder.AppendJoin(',', values.Select(EscapeCsvValue));
        builder.AppendLine();
    }

    private static string EscapeCsvValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{escaped}\""
            : escaped;
    }

    private static string FormatCsvDate(DateTimeOffset? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss zzz") ?? string.Empty;
    }

    private static string FormatTaskType(int taskType)
    {
        return taskType switch
        {
            1 => "算法题",
            2 => "文件题",
            _ => taskType.ToString()
        };
    }

    private static string FormatDifficulty(int difficulty)
    {
        return difficulty switch
        {
            1 => "兵",
            2 => "马",
            3 => "象",
            4 => "车",
            5 => "皇后",
            6 => "国王",
            _ => difficulty.ToString()
        };
    }

    private sealed class ChallengeProgressUserRow
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public bool IsLeaderboardAnonymous { get; set; }
    }

    private sealed class ChallengeProgressCompletionRow
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }

        public bool IsLeaderboardAnonymous { get; set; }

        public Guid TaskId { get; set; }

        public int Score { get; set; }

        public bool IsCompleted { get; set; }

        public DateTimeOffset CompletedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }

    private async Task EnsureParticipantAsync(Guid challengeId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ChallengeParticipants" ("Id", "ChallengeId", "UserId", "JoinedAt")
             VALUES ({Guid.NewGuid()}, {challengeId}, {userId}, {joinedAt})
             ON CONFLICT ("ChallengeId", "UserId") DO NOTHING;
             """,
            cancellationToken);
    }
}
