using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Leaderboards.Dtos;
using OnlineJudge.Application.Leaderboards.Requests;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardSeasonService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    LeaderboardIdentityService identityService) : ILeaderboardSeasonService
{
    public async Task<Result<SeasonLeaderboardDto>> GetCurrentLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null) return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());

        var effectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow());
        if (effectiveStatus is not LeaderboardSeasonStatus.Active and not LeaderboardSeasonStatus.Public)
        {
            return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());
        }

        var viewer = await identityService.GetViewerAsync(cancellationToken);
        return Result<SeasonLeaderboardDto>.Success(await BuildLeaderboardAsync(season, viewer, useArchive: effectiveStatus == LeaderboardSeasonStatus.Public, cancellationToken));
    }

    public async Task<Result<SeasonLeaderboardDto>> GetCurrentAuditLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireProblemSetterAsync(cancellationToken);
        if (userResult.IsFailure) return Result<SeasonLeaderboardDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null) return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());

        var viewer = new LeaderboardViewer(userResult.Value!.Id, userResult.Value.Role, true);
        var useArchive = season.Status == LeaderboardSeasonStatus.Public && season.ArchiveEntries.Count > 0;
        return Result<SeasonLeaderboardDto>.Success(await BuildLeaderboardAsync(season, viewer, useArchive, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<LeaderboardSeasonDto>>> GetSeasonsAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireProblemSetterAsync(cancellationToken);
        if (userResult.IsFailure) return Result<IReadOnlyList<LeaderboardSeasonDto>>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var seasons = await dbContext.LeaderboardSeasons.AsNoTracking()
            .Include(season => season.Problems)
            .ThenInclude(problem => problem.Problem)
            .OrderByDescending(season => season.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LeaderboardSeasonDto>>.Success(seasons.Select(ToDto).ToList());
    }

    public async Task<Result<LeaderboardSeasonDto>> CreateSeasonAsync(CreateLeaderboardSeasonRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var validationError = ValidateSchedule(request.Name, request.StartAt, request.FreezeAt, request.PublicUntil, requireFutureStart: true);
        if (validationError is not null) return Result<LeaderboardSeasonDto>.Failure(validationError);

        if (await dbContext.LeaderboardSeasons.AnyAsync(season => season.IsCurrent, cancellationToken))
        {
            return Result<LeaderboardSeasonDto>.Failure("A current leaderboard season already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var season = new LeaderboardSeason
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            StartAt = request.StartAt,
            FreezeAt = request.FreezeAt,
            PublicUntil = request.PublicUntil,
            Status = LeaderboardSeasonStatus.Scheduled,
            IsCurrent = true,
            CreatedByUserId = userResult.Value!.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.LeaderboardSeasons.Add(season);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonDto>> UpdateSeasonAsync(Guid seasonId, UpdateLeaderboardSeasonRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result<LeaderboardSeasonDto>.Failure("Only a scheduled season can be updated.");

        var validationError = ValidateSchedule(request.Name, request.StartAt, request.FreezeAt, request.PublicUntil, requireFutureStart: true);
        if (validationError is not null) return Result<LeaderboardSeasonDto>.Failure(validationError);

        season.Name = request.Name.Trim();
        season.StartAt = request.StartAt;
        season.FreezeAt = request.FreezeAt;
        season.PublicUntil = request.PublicUntil;
        season.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonDto>> AddProblemAsync(Guid seasonId, AddLeaderboardSeasonProblemRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result<LeaderboardSeasonDto>.Failure("Season problems are frozen after the season starts.");
        if (season.Problems.Any(item => item.ProblemId == request.ProblemId)) return Result<LeaderboardSeasonDto>.Failure("Problem is already in this season.");

        var problem = await dbContext.Problems
            .Include(problem => problem.TestCases.Where(testCase => !testCase.IsDeleted))
            .FirstOrDefaultAsync(problem => problem.Id == request.ProblemId && !problem.IsDeleted, cancellationToken);
        if (problem is null) return Result<LeaderboardSeasonDto>.Failure("Problem not found.");

        var seasonProblem = new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            ProblemId = problem.Id,
            Season = season,
            Problem = problem,
            BaseScore = problem.TestCases.Sum(testCase => Math.Max(0, testCase.Score)),
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.LeaderboardSeasonProblems.Add(seasonProblem);
        season.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result> RemoveProblemAsync(Guid seasonId, Guid problemId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result.Failure("Season problems are frozen after the season starts.");

        var item = season.Problems.FirstOrDefault(item => item.ProblemId == problemId);
        if (item is null) return Result.Failure("Season problem not found.");
        dbContext.LeaderboardSeasonProblems.Remove(item);
        season.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LeaderboardSeasonDto>> FreezeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (season.Status is LeaderboardSeasonStatus.Public or LeaderboardSeasonStatus.Archived)
        {
            return Result<LeaderboardSeasonDto>.Failure("Leaderboard season cannot be frozen in its current state.");
        }

        var now = timeProvider.GetUtcNow();
        if (now < season.FreezeAt) return Result<LeaderboardSeasonDto>.Failure("Season freeze time has not been reached.");
        season.Status = LeaderboardSeasonStatus.Frozen;
        season.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonArchiveDto>> FinalizeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonArchiveDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonArchiveDto>.Failure("Leaderboard season not found.");
        if (season.Status == LeaderboardSeasonStatus.Archived) return Result<LeaderboardSeasonArchiveDto>.Failure("Archived leaderboard snapshots are immutable.");

        var now = timeProvider.GetUtcNow();
        if (now < season.FreezeAt) return Result<LeaderboardSeasonArchiveDto>.Failure("Season freeze time has not been reached.");

        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var ranked = Rank(rows);

        if (season.ArchiveEntries.Count > 0)
        {
            dbContext.LeaderboardSeasonArchiveProblemScores.RemoveRange(season.ArchiveEntries.SelectMany(entry => entry.ProblemScores));
            dbContext.LeaderboardSeasonArchiveEntries.RemoveRange(season.ArchiveEntries);
        }

        foreach (var rankedUser in ranked)
        {
            var entry = new LeaderboardSeasonArchiveEntry
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                UserId = rankedUser.UserId,
                Alias = aliases[rankedUser.UserId],
                DisplayNameSnapshot = rankedUser.UserName,
                WasAnonymous = rankedUser.IsAnonymous,
                FinalRank = rankedUser.Rank,
                FinalScore = rankedUser.BaseScore,
                FinalBaseScore = rankedUser.BaseScore,
                SolvedCount = rankedUser.SolvedCount,
                LastScoreImprovedAt = rankedUser.LastScoreImprovedAt,
                CreatedAt = now
            };

            entry.ProblemScores = rankedUser.Problems.Select(problem => new LeaderboardSeasonArchiveProblemScore
            {
                Id = Guid.NewGuid(),
                SeasonId = season.Id,
                ArchiveEntryId = entry.Id,
                ProblemId = problem.ProblemId,
                ProblemTitleSnapshot = problem.ProblemTitle,
                BaseScore = problem.BaseScore,
                EarnedBaseScore = problem.EarnedBaseScore,
                TimeBonus = 0,
                RuntimeBonus = 0,
                MemoryBonus = 0,
                FinalProblemScore = problem.EarnedBaseScore
            }).ToList();
            dbContext.LeaderboardSeasonArchiveEntries.Add(entry);
        }

        season.Status = LeaderboardSeasonStatus.Public;
        season.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetArchiveCoreAsync(season, cancellationToken);
    }

    public async Task<Result<LeaderboardSeasonDto>> ArchiveSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (season.Status != LeaderboardSeasonStatus.Public) return Result<LeaderboardSeasonDto>.Failure("Only a public season can be archived.");
        if (timeProvider.GetUtcNow() < season.PublicUntil) return Result<LeaderboardSeasonDto>.Failure("Season public period has not ended.");

        season.Status = LeaderboardSeasonStatus.Archived;
        season.IsCurrent = false;
        season.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonArchiveDto>> GetArchiveAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonArchiveDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        return season is null
            ? Result<LeaderboardSeasonArchiveDto>.Failure("Leaderboard season not found.")
            : await GetArchiveCoreAsync(season, cancellationToken);
    }

    private async Task<SeasonLeaderboardDto> BuildLeaderboardAsync(
        LeaderboardSeason season,
        LeaderboardViewer viewer,
        bool useArchive,
        CancellationToken cancellationToken)
    {
        if (useArchive && season.ArchiveEntries.Count > 0)
        {
            return new SeasonLeaderboardDto
            {
                Season = ToDto(season),
                Entries = season.ArchiveEntries.OrderBy(entry => entry.FinalRank).Select(entry =>
                {
                    var isHidden = entry.WasAnonymous && !viewer.CanAudit;
                    return new SeasonLeaderboardEntryDto
                    {
                        Rank = entry.FinalRank,
                        UserId = isHidden ? null : entry.UserId,
                        UserName = viewer.CanAudit ? entry.DisplayNameSnapshot : null,
                        DisplayName = isHidden ? entry.Alias : entry.DisplayNameSnapshot,
                        Alias = entry.Alias,
                        IsAnonymous = entry.WasAnonymous,
                        IsCurrentUser = viewer.UserId == entry.UserId,
                        TotalScore = entry.FinalScore,
                        BaseScore = entry.FinalBaseScore,
                        SolvedCount = entry.SolvedCount,
                        LastScoreImprovedAt = entry.LastScoreImprovedAt
                    };
                }).ToList()
            };
        }

        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var entries = Rank(rows).Select(item =>
        {
            var identity = LeaderboardIdentityService.Project(
                new LeaderboardIdentityUser(item.UserId, item.UserName, item.AvatarUrl, item.IsAnonymous), viewer, aliases);
            return new SeasonLeaderboardEntryDto
            {
                Rank = item.Rank,
                UserId = identity.UserId,
                UserName = viewer.CanAudit ? item.UserName : null,
                DisplayName = identity.DisplayName,
                Alias = identity.Alias,
                IsAnonymous = item.IsAnonymous,
                IsCurrentUser = viewer.UserId == item.UserId,
                BaseScore = item.BaseScore,
                TotalScore = item.BaseScore,
                SolvedCount = item.SolvedCount,
                TimeBonus = 0,
                RuntimeBonus = 0,
                MemoryBonus = 0,
                LastScoreImprovedAt = item.LastScoreImprovedAt
            };
        }).ToList();

        return new SeasonLeaderboardDto { Season = ToDto(season), Entries = entries };
    }

    private async Task<List<ScoreUserRow>> LoadEligibleScoreRowsAsync(Guid seasonId, CancellationToken cancellationToken)
    {
        var scores = await (
            from score in dbContext.LeaderboardUserProblemScores.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on score.UserId equals user.Id
            join seasonProblem in dbContext.LeaderboardSeasonProblems.AsNoTracking() on score.SeasonProblemId equals seasonProblem.Id
            join problem in dbContext.Problems.AsNoTracking() on score.ProblemId equals problem.Id
            where score.SeasonId == seasonId
                && score.IsFullScore
                && user.Role == UserRole.Answerer
                && !user.IsBlacklisted
                && !user.IsDeleted
            select new
            {
                score.UserId,
                user.UserName,
                user.AvatarUrl,
                user.IsLeaderboardAnonymous,
                score.ProblemId,
                ProblemTitle = problem.Title,
                seasonProblem.BaseScore,
                EarnedBaseScore = score.BestBaseScore,
                score.LastScoreImprovedAt
            }).ToListAsync(cancellationToken);

        return scores.GroupBy(score => new { score.UserId, score.UserName, score.AvatarUrl, score.IsLeaderboardAnonymous })
            .Select(group => new ScoreUserRow(
                group.Key.UserId,
                group.Key.UserName,
                group.Key.AvatarUrl,
                group.Key.IsLeaderboardAnonymous,
                group.Sum(score => score.EarnedBaseScore),
                group.Count(),
                group.Max(score => score.LastScoreImprovedAt),
                group.Select(score => new ScoreProblemRow(score.ProblemId, score.ProblemTitle, score.BaseScore, score.EarnedBaseScore)).ToList()))
            .ToList();
    }

    private static IReadOnlyList<RankedScoreUserRow> Rank(IEnumerable<ScoreUserRow> rows)
    {
        return rows.OrderByDescending(row => row.BaseScore)
            .ThenByDescending(row => row.SolvedCount)
            .ThenBy(row => row.LastScoreImprovedAt)
            .ThenBy(row => row.UserName)
            .Select((row, index) => new RankedScoreUserRow(row, index + 1))
            .ToList();
    }

    private async Task<Result<LeaderboardSeasonArchiveDto>> GetArchiveCoreAsync(LeaderboardSeason season, CancellationToken cancellationToken)
    {
        var entries = await dbContext.LeaderboardSeasonArchiveEntries.AsNoTracking()
            .Where(entry => entry.SeasonId == season.Id)
            .Include(entry => entry.ProblemScores)
            .OrderBy(entry => entry.FinalRank)
            .ToListAsync(cancellationToken);

        return Result<LeaderboardSeasonArchiveDto>.Success(new LeaderboardSeasonArchiveDto
        {
            SeasonId = season.Id,
            SeasonName = season.Name,
            Entries = entries.Select(entry => new LeaderboardSeasonArchiveEntryDto
            {
                UserId = entry.UserId,
                Alias = entry.Alias,
                DisplayNameSnapshot = entry.DisplayNameSnapshot,
                WasAnonymous = entry.WasAnonymous,
                FinalRank = entry.FinalRank,
                FinalScore = entry.FinalScore,
                FinalBaseScore = entry.FinalBaseScore,
                SolvedCount = entry.SolvedCount,
                LastScoreImprovedAt = entry.LastScoreImprovedAt,
                ProblemScores = entry.ProblemScores.Select(score => new LeaderboardSeasonArchiveProblemScoreDto
                {
                    ProblemId = score.ProblemId,
                    ProblemTitleSnapshot = score.ProblemTitleSnapshot,
                    BaseScore = score.BaseScore,
                    EarnedBaseScore = score.EarnedBaseScore,
                    TimeBonus = score.TimeBonus,
                    RuntimeBonus = score.RuntimeBonus,
                    MemoryBonus = score.MemoryBonus,
                    FinalProblemScore = score.FinalProblemScore
                }).ToList()
            }).ToList()
        });
    }

    private async Task<LeaderboardSeason?> LoadCurrentSeasonAsync(CancellationToken cancellationToken) =>
        await dbContext.LeaderboardSeasons
            .Include(season => season.Problems).ThenInclude(problem => problem.Problem)
            .Include(season => season.ArchiveEntries).ThenInclude(entry => entry.ProblemScores)
            .SingleOrDefaultAsync(season => season.IsCurrent, cancellationToken);

    private async Task<LeaderboardSeason?> LoadSeasonAsync(Guid seasonId, CancellationToken cancellationToken) =>
        await dbContext.LeaderboardSeasons
            .Include(season => season.Problems).ThenInclude(problem => problem.Problem)
            .Include(season => season.ArchiveEntries).ThenInclude(entry => entry.ProblemScores)
            .FirstOrDefaultAsync(season => season.Id == seasonId, cancellationToken);

    private bool CanEditScheduledSeason(LeaderboardSeason season) =>
        season.Status == LeaderboardSeasonStatus.Scheduled
        && LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow()) == LeaderboardSeasonStatus.Scheduled;

    private string? ValidateSchedule(string name, DateTimeOffset startAt, DateTimeOffset freezeAt, DateTimeOffset publicUntil, bool requireFutureStart)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Season name is required.";
        if (name.Trim().Length > 120) return "Season name must not exceed 120 characters.";
        if (!(startAt < freezeAt && freezeAt < publicUntil)) return "Season times must satisfy StartAt < FreezeAt < PublicUntil.";
        if (requireFutureStart && startAt <= timeProvider.GetUtcNow()) return "A scheduled season must start in the future.";
        return null;
    }

    private async Task<Result<User>> RequireProblemSetterAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId) return Result<User>.Failure("Unauthorized.");
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            user => user.Id == userId && !user.IsDeleted && !user.IsBlacklisted,
            cancellationToken);
        return user is not null && user.Role is UserRole.ProblemSetter or UserRole.Root
            ? Result<User>.Success(user)
            : Result<User>.Failure("Forbidden.");
    }

    private async Task<Result<User>> RequireRootAsync(CancellationToken cancellationToken)
    {
        var result = await RequireProblemSetterAsync(cancellationToken);
        return result.IsSuccess && result.Value?.Role == UserRole.Root
            ? result
            : Result<User>.Failure(result.ErrorMessage == "Unauthorized." ? "Unauthorized." : "Forbidden.");
    }

    private LeaderboardSeasonDto ToDto(LeaderboardSeason season) => new()
    {
        Id = season.Id,
        Name = season.Name,
        StartAt = season.StartAt,
        FreezeAt = season.FreezeAt,
        PublicUntil = season.PublicUntil,
        Status = season.Status,
        EffectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow()),
        IsCurrent = season.IsCurrent,
        Problems = season.Problems.OrderBy(problem => problem.CreatedAt).Select(problem => new LeaderboardSeasonProblemDto
        {
            Id = problem.Id,
            ProblemId = problem.ProblemId,
            ProblemTitle = problem.Problem?.Title ?? "题目已删除",
            BaseScore = problem.BaseScore
        }).ToList()
    };

    private sealed record ScoreProblemRow(Guid ProblemId, string ProblemTitle, int BaseScore, int EarnedBaseScore);

    private record ScoreUserRow(
        Guid UserId,
        string UserName,
        string? AvatarUrl,
        bool IsAnonymous,
        int BaseScore,
        int SolvedCount,
        DateTimeOffset LastScoreImprovedAt,
        IReadOnlyList<ScoreProblemRow> Problems);

    private sealed record RankedScoreUserRow(ScoreUserRow Row, int Rank) : ScoreUserRow(
        Row.UserId,
        Row.UserName,
        Row.AvatarUrl,
        Row.IsAnonymous,
        Row.BaseScore,
        Row.SolvedCount,
        Row.LastScoreImprovedAt,
        Row.Problems);
}
