using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Leaderboards.Dtos;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Requests;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardSeasonService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    LeaderboardIdentityService identityService,
    ILeaderboardScoringEngine scoringEngine,
    LeaderboardScoringOptions scoringOptions,
    LeaderboardSeasonLifecycleOptions lifecycleOptions,
    ILogger<LeaderboardSeasonService> logger,
    ISecurityAuditWriter? auditWriter = null) : ILeaderboardSeasonService, ILeaderboardSeasonLifecycleService
{
    public LeaderboardSeasonService(
        OnlineJudgeDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        LeaderboardIdentityService identityService)
        : this(dbContext, currentUser, timeProvider, identityService, new LeaderboardScoringEngine(), new LeaderboardScoringOptions(), new LeaderboardSeasonLifecycleOptions(), NullLogger<LeaderboardSeasonService>.Instance)
    {
    }

    public async Task<Result<SeasonLeaderboardDto>> GetCurrentLeaderboardAsync(CancellationToken cancellationToken = default)
    {
        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null || !season.Boards.Any(board => board.BoardType == LeaderboardSeasonBoardType.Global))
        {
            return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());
        }

        var effectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow());
        if (effectiveStatus == LeaderboardSeasonStatus.Archived)
        {
            return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());
        }

        if (effectiveStatus == LeaderboardSeasonStatus.Scheduled)
        {
            return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto { Season = ToDto(season) });
        }
        if (effectiveStatus == LeaderboardSeasonStatus.Frozen)
        {
            return Result<SeasonLeaderboardDto>.Success(new SeasonLeaderboardDto());
        }

        var viewer = await identityService.GetViewerAsync(cancellationToken);
        return Result<SeasonLeaderboardDto>.Success(await BuildLeaderboardAsync(season, viewer, useArchive: effectiveStatus == LeaderboardSeasonStatus.Public, cancellationToken));
    }

    public async Task<Result<LeaderboardSeasonPublicSummaryResponseDto>> GetCurrentPublicSummaryAsync(CancellationToken cancellationToken = default)
    {
        var season = await dbContext.LeaderboardSeasons.AsNoTracking()
            .Include(item => item.Boards).ThenInclude(board => board.Challenge)
            .SingleOrDefaultAsync(item => item.IsCurrent, cancellationToken);
        if (season is null)
        {
            return Result<LeaderboardSeasonPublicSummaryResponseDto>.Success(new LeaderboardSeasonPublicSummaryResponseDto());
        }

        var effectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow());
        if (effectiveStatus == LeaderboardSeasonStatus.Archived)
        {
            return Result<LeaderboardSeasonPublicSummaryResponseDto>.Success(new LeaderboardSeasonPublicSummaryResponseDto());
        }

        return Result<LeaderboardSeasonPublicSummaryResponseDto>.Success(new LeaderboardSeasonPublicSummaryResponseDto
        {
            Season = new LeaderboardSeasonPublicSummaryDto
            {
                Name = season.Name,
                Status = effectiveStatus,
                StartAt = season.StartAt,
                FreezeAt = season.FreezeAt,
                PublicUntil = season.PublicUntil,
                Boards = season.Boards
                    .Where(board => board.BoardType == LeaderboardSeasonBoardType.Global || board.Challenge!.IsPublished)
                    .OrderBy(board => board.BoardType).ThenBy(board => board.Challenge?.Title ?? string.Empty).Select(board => new LeaderboardSeasonPublicBoardDto
                {
                    BoardType = board.BoardType,
                    ChallengeId = board.ChallengeId,
                    ChallengeTitle = board.Challenge?.Title
                }).ToList()
            }
        });
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

    public async Task<Result<SeasonProblemLeaderboardDto>> GetCurrentProblemLeaderboardAsync(
        Guid problemId,
        CancellationToken cancellationToken = default)
    {
        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null || !season.Boards.Any(board => board.BoardType == LeaderboardSeasonBoardType.Global))
        {
            return Result<SeasonProblemLeaderboardDto>.Success(new SeasonProblemLeaderboardDto());
        }

        var effectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow());
        var seasonProblem = season.Problems.FirstOrDefault(problem => problem.ProblemId == problemId);
        if (seasonProblem is null || effectiveStatus is not LeaderboardSeasonStatus.Active and not LeaderboardSeasonStatus.Public)
        {
            return Result<SeasonProblemLeaderboardDto>.Success(new SeasonProblemLeaderboardDto());
        }

        var viewer = await identityService.GetViewerAsync(cancellationToken);
        return Result<SeasonProblemLeaderboardDto>.Success(await BuildProblemLeaderboardAsync(
            season,
            seasonProblem,
            viewer,
            useArchive: effectiveStatus == LeaderboardSeasonStatus.Public,
            cancellationToken));
    }

    public async Task<Result<IReadOnlyList<LeaderboardSeasonDto>>> GetSeasonsAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireProblemSetterAsync(cancellationToken);
        if (userResult.IsFailure) return Result<IReadOnlyList<LeaderboardSeasonDto>>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var seasons = await dbContext.LeaderboardSeasons.AsNoTracking()
            .Include(season => season.Boards)
            .ThenInclude(board => board.Challenge)
            .Include(season => season.Problems)
            .ThenInclude(problem => problem.Problem)
            .Include(season => season.Problems)
            .ThenInclude(problem => problem.Benchmarks)
            .OrderByDescending(season => season.CreatedAt)
            .ToListAsync(cancellationToken);

        var currentScores = await ProblemScoreQuery.GetTotalsAsync(
            dbContext,
            seasons.Where(season => season.Status == LeaderboardSeasonStatus.Scheduled)
                .SelectMany(season => season.Problems).Select(problem => problem.ProblemId),
            cancellationToken);
        return Result<IReadOnlyList<LeaderboardSeasonDto>>.Success(seasons.Select(season => ToDto(season, currentScores)).ToList());
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
        var rules = scoringOptions.CreateSnapshot();
        rules.FirstCompletionBonusEnabled = request.FirstCompletionBonusEnabled;
        rules.RuntimeBonusEnabled = request.RuntimeBonusEnabled;
        rules.MemoryBonusEnabled = request.MemoryBonusEnabled;
        var season = new LeaderboardSeason
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            StartAt = request.StartAt,
            FreezeAt = request.FreezeAt,
            PublicUntil = request.PublicUntil,
            Status = LeaderboardSeasonStatus.Scheduled,
            IsCurrent = true,
            ScoringRulesJson = LeaderboardScoringRulesSerializer.Serialize(rules),
            CreatedByUserId = userResult.Value!.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        var boardError = await SynchronizeBoardsAsync(season, request.IncludeGlobalBoard, request.ChallengeIds, cancellationToken);
        if (boardError is not null) return Result<LeaderboardSeasonDto>.Failure(boardError);

        dbContext.LeaderboardSeasons.Add(season);
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonCreated, "LeaderboardSeason", season.Id.ToString()));
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
        var rules = LeaderboardScoringRulesSerializer.Deserialize(season.ScoringRulesJson);
        rules.FirstCompletionBonusEnabled = request.FirstCompletionBonusEnabled;
        rules.RuntimeBonusEnabled = request.RuntimeBonusEnabled;
        rules.MemoryBonusEnabled = request.MemoryBonusEnabled;
        season.ScoringRulesJson = LeaderboardScoringRulesSerializer.Serialize(rules);
        var boardError = await SynchronizeBoardsAsync(season, request.IncludeGlobalBoard, request.ChallengeIds, cancellationToken);
        if (boardError is not null) return Result<LeaderboardSeasonDto>.Failure(boardError);
        season.UpdatedAt = timeProvider.GetUtcNow();
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
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
        var problem = await dbContext.Problems.FirstOrDefaultAsync(
            problem => problem.Id == request.ProblemId && !problem.IsDeleted,
            cancellationToken);
        if (problem is null) return Result<LeaderboardSeasonDto>.Failure("Problem not found.");
        var currentScores = await ProblemScoreQuery.GetTotalsAsync(dbContext, [problem.Id], cancellationToken);

        var seasonProblem = new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            ProblemId = problem.Id,
            Season = season,
            Problem = problem,
            BaseScore = currentScores.GetValueOrDefault(problem.Id),
            CreatedAt = timeProvider.GetUtcNow()
        };
        dbContext.LeaderboardSeasonProblems.Add(seasonProblem);
        season.UpdatedAt = timeProvider.GetUtcNow();
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonDto>> AddProblemsAsync(Guid seasonId, AddLeaderboardSeasonProblemsRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");
        var problemIds = request.ProblemIds.Distinct().ToList();
        if (problemIds.Count == 0) return Result<LeaderboardSeasonDto>.Failure("At least one problem is required.");
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result<LeaderboardSeasonDto>.Failure("Season problems are frozen after the season starts.");
        if (season.Problems.Any(item => problemIds.Contains(item.ProblemId)))
        {
            return Result<LeaderboardSeasonDto>.Failure("Problem is already in this season.");
        }
        var problems = await dbContext.Problems
            .Where(problem => problemIds.Contains(problem.Id) && !problem.IsDeleted)
            .ToListAsync(cancellationToken);
        if (problems.Count != problemIds.Count) return Result<LeaderboardSeasonDto>.Failure("Problem not found.");
        var currentScores = await ProblemScoreQuery.GetTotalsAsync(dbContext, problemIds, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var additions = problems.Select(problem => new LeaderboardSeasonProblem
        {
            Id = Guid.NewGuid(), SeasonId = season.Id, ProblemId = problem.Id, Season = season, Problem = problem,
            BaseScore = currentScores.GetValueOrDefault(problem.Id), CreatedAt = now
        }).ToList();
        dbContext.LeaderboardSeasonProblems.AddRange(additions);
        season.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result> RemoveProblemsAsync(Guid seasonId, RemoveLeaderboardSeasonProblemsRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result.Failure(userResult.ErrorMessage ?? "Forbidden.");
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result.Failure("Season problems are frozen after the season starts.");
        var problemIds = request.ProblemIds.Distinct().ToHashSet();
        var items = season.Problems.Where(item => problemIds.Contains(item.ProblemId)).ToList();
        if (items.Count != problemIds.Count) return Result.Failure("Season problem not found.");
        dbContext.LeaderboardSeasonProblems.RemoveRange(items);
        season.UpdatedAt = timeProvider.GetUtcNow();
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LeaderboardSeasonDto>> UpdateProblemBenchmarkAsync(
        Guid seasonId,
        Guid problemId,
        JudgeLanguage language,
        UpdateLeaderboardSeasonProblemBenchmarkRequest request,
        CancellationToken cancellationToken = default)
    {
        var userResult = await RequireProblemSetterAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (!CanEditScheduledSeason(season)) return Result<LeaderboardSeasonDto>.Failure("Season benchmarks are frozen after the season starts.");
        var rules = LeaderboardScoringRulesSerializer.Deserialize(season.ScoringRulesJson);
        if ((rules.RuntimeBonusEnabled && request.RuntimeBaselineMs is null or <= 0)
            || (rules.MemoryBonusEnabled && request.MemoryBaselineKb is null or <= 0))
        {
            return Result<LeaderboardSeasonDto>.Failure("Enabled reward baselines must be greater than zero.");
        }

        var seasonProblem = season.Problems.FirstOrDefault(problem => problem.ProblemId == problemId);
        if (seasonProblem?.Problem is null) return Result<LeaderboardSeasonDto>.Failure("Season problem not found.");
        if (!IsLanguageAllowed(seasonProblem.Problem.AllowedLanguagesMask, language))
        {
            return Result<LeaderboardSeasonDto>.Failure("Benchmark language is not allowed for this problem.");
        }

        var now = timeProvider.GetUtcNow();
        var benchmark = seasonProblem.Benchmarks.FirstOrDefault(item => item.Language == language);
        if (benchmark is null)
        {
            benchmark = new LeaderboardSeasonProblemBenchmark
            {
                Id = Guid.NewGuid(),
                SeasonProblemId = seasonProblem.Id,
                Language = language,
                CreatedAt = now
            };
            seasonProblem.Benchmarks.Add(benchmark);
            dbContext.LeaderboardSeasonProblemBenchmarks.Add(benchmark);
        }

        benchmark.RuntimeBaselineMs = rules.RuntimeBonusEnabled ? request.RuntimeBaselineMs!.Value : 0;
        benchmark.MemoryBaselineKb = rules.MemoryBonusEnabled ? request.MemoryBaselineKb!.Value : 0;
        benchmark.UpdatedAt = now;
        season.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
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
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonUpdated, "LeaderboardSeason", season.Id.ToString()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<LeaderboardSeasonDto>> FreezeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (season.Status == LeaderboardSeasonStatus.Archived)
        {
            return Result<LeaderboardSeasonDto>.Failure("Leaderboard season cannot be frozen in its current state.");
        }

        var now = timeProvider.GetUtcNow();
        if (now < season.StartAt) return Result<LeaderboardSeasonDto>.Failure("A scheduled season cannot be frozen before it starts.");
        if (season.Status == LeaderboardSeasonStatus.Frozen) return Result<LeaderboardSeasonDto>.Success(ToDto(season));
        if (season.Status == LeaderboardSeasonStatus.Public) return Result<LeaderboardSeasonDto>.Failure("A public season must be re-finalized instead of frozen.");
        var previousStatus = season.Status;
        season.Status = LeaderboardSeasonStatus.Frozen;
        season.ActivatedAt ??= now;
        season.FrozenAt ??= now;
        if (now < season.FreezeAt) season.ManuallyFrozenAt ??= now;
        season.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonFrozen, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
        {
            ["seasonStateBefore"] = previousStatus.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Frozen.ToString()
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Leaderboard season operation completed. SeasonId={SeasonId}, Operation={Operation}, ActorUserId={ActorUserId}, Timestamp={Timestamp}", season.Id, "Freeze", userResult.Value!.Id, now);
        return Result<LeaderboardSeasonDto>.Success(ToDto(season));
    }

    public async Task<Result<LeaderboardSeasonArchiveDto>> FinalizeSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonArchiveDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonArchiveDto>.Failure("Leaderboard season not found.");
        if (season.Status == LeaderboardSeasonStatus.Archived) return Result<LeaderboardSeasonArchiveDto>.Failure("Archived leaderboard snapshots are immutable.");

        var now = timeProvider.GetUtcNow();
        if (season.Status is not LeaderboardSeasonStatus.Frozen and not LeaderboardSeasonStatus.Public
            && LeaderboardSeasonLifecycle.GetEffectiveStatus(season, now) != LeaderboardSeasonStatus.Frozen)
        {
            return Result<LeaderboardSeasonArchiveDto>.Failure("Only a frozen or public season can be finalized.");
        }
        var previousStatus = season.Status;
        if (season.Status != LeaderboardSeasonStatus.Public)
        {
            season.Status = LeaderboardSeasonStatus.Frozen;
            season.ActivatedAt ??= now;
            season.FrozenAt ??= now;
        }

        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonPublished, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
        {
            ["seasonStateBefore"] = previousStatus.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Public.ToString()
        }));
        await FinalizeCoreAsync(season, now, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Leaderboard season operation completed. SeasonId={SeasonId}, Operation={Operation}, ActorUserId={ActorUserId}, Timestamp={Timestamp}", season.Id, "Finalize", userResult.Value!.Id, now);
        return await GetArchiveCoreAsync(season, cancellationToken);
    }

    public async Task<Result<LeaderboardSeasonDto>> ArchiveSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireRootAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null) return Result<LeaderboardSeasonDto>.Failure("Leaderboard season not found.");
        if (season.Status == LeaderboardSeasonStatus.Archived) return Result<LeaderboardSeasonDto>.Success(ToDto(season));
        if (season.Status != LeaderboardSeasonStatus.Public) return Result<LeaderboardSeasonDto>.Failure("Only a public season can be archived.");
        if (timeProvider.GetUtcNow() < season.PublicUntil) return Result<LeaderboardSeasonDto>.Failure("Season public period has not ended.");

        season.Status = LeaderboardSeasonStatus.Archived;
        season.IsCurrent = false;
        season.ArchivedAt = timeProvider.GetUtcNow();
        season.UpdatedAt = season.ArchivedAt.Value;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonArchived, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
        {
            ["seasonStateBefore"] = LeaderboardSeasonStatus.Public.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Archived.ToString()
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Leaderboard season operation completed. SeasonId={SeasonId}, Operation={Operation}, ActorUserId={ActorUserId}, Timestamp={Timestamp}", season.Id, "Archive", userResult.Value!.Id, season.ArchivedAt);
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

    public async Task<Result<IReadOnlyList<LeaderboardSeasonHistorySummaryDto>>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await identityService.GetViewerAsync(cancellationToken);
        var seasons = await dbContext.LeaderboardSeasons.AsNoTracking()
            .Where(season => season.Status == LeaderboardSeasonStatus.Archived)
            .Include(season => season.ArchiveEntries)
            .OrderByDescending(season => season.ArchivedAt)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LeaderboardSeasonHistorySummaryDto>>.Success(seasons.Select(season => new LeaderboardSeasonHistorySummaryDto
        {
            SeasonId = season.Id,
            Name = season.Name,
            StartAt = season.StartAt,
            FreezeAt = season.FreezeAt,
            PublicUntil = season.PublicUntil,
            ArchivedAt = season.ArchivedAt,
            ParticipantCount = season.ArchiveEntries.Count,
            Top3 = season.ArchiveEntries.OrderBy(entry => entry.FinalRank).Take(3).Select(entry => new LeaderboardSeasonHistoryTopEntryDto
            {
                Rank = entry.FinalRank,
                DisplayName = entry.WasAnonymous && !viewer.CanAudit ? entry.Alias : entry.DisplayNameSnapshot,
                FinalScore = entry.FinalScore
            }).ToList()
        }).ToList());
    }

    public async Task<Result<LeaderboardSeasonArchiveDto>> GetHistoryAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season is null || season.Status != LeaderboardSeasonStatus.Archived)
        {
            return Result<LeaderboardSeasonArchiveDto>.Failure("Leaderboard season history not found.");
        }

        var viewer = await identityService.GetViewerAsync(cancellationToken);
        return Result<LeaderboardSeasonArchiveDto>.Success(BuildArchiveDto(season, viewer.CanAudit));
    }

    public async Task<Result<LeaderboardSeasonPersonalDto>> GetCurrentPersonalAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireAnswererAsync(cancellationToken);
        if (userResult.IsFailure) return Result<LeaderboardSeasonPersonalDto>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null || !season.Boards.Any(board => board.BoardType == LeaderboardSeasonBoardType.Global))
        {
            return Result<LeaderboardSeasonPersonalDto>.Success(new LeaderboardSeasonPersonalDto());
        }

        var userId = userResult.Value!.Id;
        var snapshots = await dbContext.LeaderboardSeasonRankSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SeasonId == season.Id && snapshot.UserId == userId)
            .OrderBy(snapshot => snapshot.RecordedAt)
            .ToListAsync(cancellationToken);

        RankedScoreUserRow? current = null;
        LeaderboardSeasonArchiveEntry? archived = null;
        if (season.Status == LeaderboardSeasonStatus.Public)
        {
            archived = season.ArchiveEntries.SingleOrDefault(entry => entry.UserId == userId);
        }
        else
        {
            var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
            var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
            current = Rank(rows, aliases).SingleOrDefault(row => row.UserId == userId);
        }

        var currentRank = current?.Rank ?? archived?.FinalRank;
        var currentScore = current?.TotalScore ?? archived?.FinalScore ?? 0;
        var previousSnapshotIndex = snapshots.Count - 1;
        if (previousSnapshotIndex >= 0 && currentRank.HasValue
            && snapshots[previousSnapshotIndex].Rank == currentRank.Value
            && snapshots[previousSnapshotIndex].TotalScore == currentScore)
        {
            previousSnapshotIndex--;
        }
        var previousRank = previousSnapshotIndex >= 0 ? snapshots[previousSnapshotIndex].Rank : (int?)null;
        var problemRows = current?.Problems.Select(problem => new LeaderboardSeasonPersonalProblemDto
        {
            ProblemId = problem.ProblemId,
            Title = problem.ProblemTitle,
            Score = problem.TotalProblemScore,
            TimeRank = problem.TimeRank,
            TimeBonus = problem.TimeBonus,
            PerformanceBonus = problem.RuntimeBonus + problem.MemoryBonus
        }).ToList() ?? archived?.ProblemScores.Select(problem => new LeaderboardSeasonPersonalProblemDto
        {
            ProblemId = problem.ProblemId,
            Title = problem.ProblemTitleSnapshot,
            Score = problem.FinalProblemScore,
            TimeRank = problem.TimeRank,
            TimeBonus = problem.TimeBonus,
            PerformanceBonus = problem.RuntimeBonus + problem.MemoryBonus
        }).ToList() ?? [];

        return Result<LeaderboardSeasonPersonalDto>.Success(new LeaderboardSeasonPersonalDto
        {
            Season = ToDto(season),
            CurrentRank = currentRank,
            TotalParticipants = season.Status == LeaderboardSeasonStatus.Public
                ? season.ArchiveEntries.Count
                : await CountEligibleParticipantsAsync(season.Id, cancellationToken),
            TotalScore = currentScore,
            TotalBaseScore = current?.BaseScore ?? archived?.FinalBaseScore ?? 0,
            TotalTimeBonus = current?.TimeBonus ?? archived?.FinalTimeBonus ?? 0,
            TotalRuntimeBonus = current?.RuntimeBonus ?? archived?.FinalRuntimeBonus ?? 0,
            TotalMemoryBonus = current?.MemoryBonus ?? archived?.FinalMemoryBonus ?? 0,
            SolvedCount = current?.SolvedCount ?? archived?.SolvedCount ?? 0,
            SeasonProblemCount = season.Problems.Count,
            Top10ProblemCount = problemRows.Count(problem => problem.TimeRank is >= 1 and <= 10),
            FirstPlaceProblemCount = problemRows.Count(problem => problem.TimeRank == 1),
            BestRank = snapshots.Select(snapshot => (int?)snapshot.Rank).Append(currentRank).Min(),
            RankChange = currentRank.HasValue && previousRank.HasValue ? previousRank.Value - currentRank.Value : null,
            Problems = problemRows,
            RankHistory = snapshots.Select(snapshot => new LeaderboardSeasonRankPointDto
            {
                RecordedAt = snapshot.RecordedAt,
                Rank = snapshot.Rank,
                TotalScore = snapshot.TotalScore
            }).ToList()
        });
    }

    public async Task<Result<IReadOnlyList<LeaderboardSeasonPersonalHistoryDto>>> GetPersonalHistoryAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireAnswererAsync(cancellationToken);
        if (userResult.IsFailure) return Result<IReadOnlyList<LeaderboardSeasonPersonalHistoryDto>>.Failure(userResult.ErrorMessage ?? "Forbidden.");

        var entries = await dbContext.LeaderboardSeasonArchiveEntries.AsNoTracking()
            .Where(entry => entry.UserId == userResult.Value!.Id && entry.Season!.Status == LeaderboardSeasonStatus.Archived)
            .Include(entry => entry.Season)
            .Include(entry => entry.ProblemScores)
            .OrderByDescending(entry => entry.Season!.ArchivedAt)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LeaderboardSeasonPersonalHistoryDto>>.Success(entries.Select(entry => new LeaderboardSeasonPersonalHistoryDto
        {
            SeasonId = entry.SeasonId,
            SeasonName = entry.Season!.Name,
            FinalRank = entry.FinalRank,
            FinalScore = entry.FinalScore,
            SolvedCount = entry.SolvedCount,
            TimeBonus = entry.FinalTimeBonus,
            PerformanceBonus = entry.FinalRuntimeBonus + entry.FinalMemoryBonus,
            Problems = entry.ProblemScores.Select(ToArchiveProblemDto).ToList()
        }).ToList());
    }

    public async Task ReconcileCurrentSeasonAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var season = await LoadCurrentSeasonAsync(cancellationToken);
        if (season is null) return;

        var now = timeProvider.GetUtcNow();
        if (season.Status == LeaderboardSeasonStatus.Scheduled && now >= season.StartAt)
        {
            await SynchronizeScheduledProblemScoresAsync(season, cancellationToken);
            season.Status = LeaderboardSeasonStatus.Active;
            season.ActivatedAt ??= now;
            season.UpdatedAt = now;
            auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonActivated, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
            {
                ["seasonStateBefore"] = LeaderboardSeasonStatus.Scheduled.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Active.ToString()
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Leaderboard season lifecycle advanced. SeasonId={SeasonId}, Operation={Operation}, Timestamp={Timestamp}", season.Id, "Activate", now);
            await CaptureRankSnapshotsAsync(season, now, force: true, cancellationToken);
        }

        if (season.Status == LeaderboardSeasonStatus.Active)
        {
            await CaptureRankSnapshotsAsync(season, now, force: false, cancellationToken);
            if (now >= season.FreezeAt)
            {
                await CaptureRankSnapshotsAsync(season, now, force: true, cancellationToken);
                season.Status = LeaderboardSeasonStatus.Frozen;
                season.FrozenAt ??= now;
                season.UpdatedAt = now;
                auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonFrozen, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
                {
                    ["seasonStateBefore"] = LeaderboardSeasonStatus.Active.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Frozen.ToString()
                }));
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Leaderboard season lifecycle advanced. SeasonId={SeasonId}, Operation={Operation}, Timestamp={Timestamp}", season.Id, "Freeze", now);
            }
        }

        if (season.Status == LeaderboardSeasonStatus.Frozen)
        {
            auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonPublished, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
            {
                ["seasonStateBefore"] = LeaderboardSeasonStatus.Frozen.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Public.ToString()
            }));
            await FinalizeCoreAsync(season, now, cancellationToken);
            logger.LogInformation("Leaderboard season lifecycle advanced. SeasonId={SeasonId}, Operation={Operation}, Timestamp={Timestamp}", season.Id, "Finalize", now);
        }

        if (season.Status == LeaderboardSeasonStatus.Public && now >= season.PublicUntil)
        {
            season.Status = LeaderboardSeasonStatus.Archived;
            season.IsCurrent = false;
            season.ArchivedAt ??= now;
            season.UpdatedAt = now;
            auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonArchived, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
            {
                ["seasonStateBefore"] = LeaderboardSeasonStatus.Public.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Archived.ToString()
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Leaderboard season lifecycle advanced. SeasonId={SeasonId}, Operation={Operation}, Timestamp={Timestamp}", season.Id, "Archive", now);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async Task RefreshPublicSeasonAsync(Guid seasonId, CancellationToken cancellationToken = default)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var season = await LoadSeasonAsync(seasonId, cancellationToken);
        if (season?.Status is LeaderboardSeasonStatus.Frozen or LeaderboardSeasonStatus.Public)
        {
            if (season.Status == LeaderboardSeasonStatus.Frozen)
            {
                auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.SeasonPublished, "LeaderboardSeason", season.Id.ToString(), Metadata: new Dictionary<string, string?>
                {
                    ["seasonStateBefore"] = LeaderboardSeasonStatus.Frozen.ToString(), ["seasonStateAfter"] = LeaderboardSeasonStatus.Public.ToString()
                }));
            }
            await FinalizeCoreAsync(season, timeProvider.GetUtcNow(), cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task FinalizeCoreAsync(LeaderboardSeason season, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var ranked = Rank(rows, aliases);

        if (season.ArchiveEntries.Count > 0)
        {
            dbContext.LeaderboardSeasonArchiveProblemScores.RemoveRange(season.ArchiveEntries.SelectMany(entry => entry.ProblemScores));
            dbContext.LeaderboardSeasonArchiveEntries.RemoveRange(season.ArchiveEntries);
            await dbContext.SaveChangesAsync(cancellationToken);
            season.ArchiveEntries.Clear();
        }

        foreach (var rankedUser in ranked)
        {
            var entry = new LeaderboardSeasonArchiveEntry
            {
                Id = Guid.NewGuid(), SeasonId = season.Id, UserId = rankedUser.UserId,
                Alias = aliases[rankedUser.UserId], DisplayNameSnapshot = rankedUser.UserName,
                WasAnonymous = rankedUser.IsAnonymous, FinalRank = rankedUser.Rank,
                FinalScore = rankedUser.TotalScore, FinalBaseScore = rankedUser.BaseScore,
                FinalTimeBonus = rankedUser.TimeBonus, FinalRuntimeBonus = rankedUser.RuntimeBonus,
                FinalMemoryBonus = rankedUser.MemoryBonus, SolvedCount = rankedUser.SolvedCount,
                LastScoreImprovedAt = rankedUser.LastScoreImprovedAt, CreatedAt = now,
                ProblemScores = rankedUser.Problems.Select(problem => new LeaderboardSeasonArchiveProblemScore
                {
                    Id = Guid.NewGuid(), SeasonId = season.Id, ProblemId = problem.ProblemId,
                    ProblemTitleSnapshot = problem.ProblemTitle, BaseScore = problem.BaseScore,
                    EarnedBaseScore = problem.EarnedBaseScore, TimeRank = problem.TimeRank,
                    FirstFullScoreAt = problem.FirstFullScoreAt, TimeBonus = problem.TimeBonus,
                    PerformanceLanguage = problem.PerformanceLanguage, RuntimeMs = problem.RuntimeMs,
                    RuntimeBaselineMs = problem.RuntimeBaselineMs, RuntimeBonus = problem.RuntimeBonus,
                    MemoryKb = problem.MemoryKb, MemoryBaselineKb = problem.MemoryBaselineKb,
                    MemoryBonus = problem.MemoryBonus, FinalProblemScore = problem.TotalProblemScore
                }).ToList()
            };
            entry.Season = season;
            foreach (var problemScore in entry.ProblemScores)
            {
                problemScore.ArchiveEntryId = entry.Id;
                problemScore.ArchiveEntry = entry;
                problemScore.Season = season;
            }
            season.ArchiveEntries.Add(entry);
            dbContext.LeaderboardSeasonArchiveEntries.Add(entry);
        }

        season.Status = LeaderboardSeasonStatus.Public;
        season.FinalizedAt = now;
        season.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await CaptureRankSnapshotsAsync(season, now, force: true, cancellationToken);
    }

    private async Task CaptureRankSnapshotsAsync(LeaderboardSeason season, DateTimeOffset now, bool force, CancellationToken cancellationToken)
    {
        var latestRecordedAt = await dbContext.LeaderboardSeasonRankSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SeasonId == season.Id)
            .MaxAsync(snapshot => (DateTimeOffset?)snapshot.RecordedAt, cancellationToken);
        if (!force && latestRecordedAt.HasValue
            && now - latestRecordedAt.Value < TimeSpan.FromMinutes(lifecycleOptions.RankSnapshotIntervalMinutes)) return;

        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var ranked = Rank(rows, aliases);
        var latest = await dbContext.LeaderboardSeasonRankSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.SeasonId == season.Id)
            .OrderBy(snapshot => snapshot.RecordedAt)
            .ToListAsync(cancellationToken);
        var latestByUser = latest.GroupBy(snapshot => snapshot.UserId).ToDictionary(group => group.Key, group => group.Last());

        foreach (var row in ranked)
        {
            if (latestByUser.TryGetValue(row.UserId, out var previous)
                && previous.Rank == row.Rank && previous.TotalScore == row.TotalScore) continue;
            dbContext.LeaderboardSeasonRankSnapshots.Add(new LeaderboardSeasonRankSnapshot
            {
                Id = Guid.NewGuid(), SeasonId = season.Id, UserId = row.UserId,
                Rank = row.Rank, TotalScore = row.TotalScore, RecordedAt = now
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<int> CountEligibleParticipantsAsync(Guid seasonId, CancellationToken cancellationToken) =>
        dbContext.LeaderboardUserProblemScores.AsNoTracking()
            .Where(score => score.SeasonId == seasonId && score.IsFullScore && score.BestBaseScore > 0
                && score.User!.Role == UserRole.Answerer && !score.User.IsBlacklisted && !score.User.IsDeleted)
            .Select(score => score.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

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
                        TimeBonus = entry.FinalTimeBonus,
                        RuntimeBonus = entry.FinalRuntimeBonus,
                        MemoryBonus = entry.FinalMemoryBonus,
                        SolvedCount = entry.SolvedCount,
                        LastScoreImprovedAt = entry.LastScoreImprovedAt
                    };
                }).ToList()
            };
        }

        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var entries = Rank(rows, aliases).Select(item =>
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
                TotalScore = item.TotalScore,
                SolvedCount = item.SolvedCount,
                TimeBonus = item.TimeBonus,
                RuntimeBonus = item.RuntimeBonus,
                MemoryBonus = item.MemoryBonus,
                LastScoreImprovedAt = item.LastScoreImprovedAt
            };
        }).ToList();

        return new SeasonLeaderboardDto { Season = ToDto(season), Entries = entries };
    }

    private async Task<SeasonProblemLeaderboardDto> BuildProblemLeaderboardAsync(
        LeaderboardSeason season,
        LeaderboardSeasonProblem seasonProblem,
        LeaderboardViewer viewer,
        bool useArchive,
        CancellationToken cancellationToken)
    {
        var problemDto = ToDto(season).Problems.Single(problem => problem.ProblemId == seasonProblem.ProblemId);
        if (useArchive && season.ArchiveEntries.Count > 0)
        {
            var archived = season.ArchiveEntries
                .Select(entry => new { Entry = entry, Score = entry.ProblemScores.SingleOrDefault(score => score.ProblemId == seasonProblem.ProblemId) })
                .Where(item => item.Score is not null)
                .OrderByDescending(item => item.Score!.FinalProblemScore)
                .ThenByDescending(item => item.Score!.EarnedBaseScore)
                .ThenBy(item => item.Score!.TimeRank ?? int.MaxValue)
                .ThenByDescending(item => item.Score!.RuntimeBonus + item.Score.MemoryBonus)
                .ThenBy(item => item.Score!.FirstFullScoreAt)
                .ThenBy(item => item.Entry.WasAnonymous ? item.Entry.Alias : item.Entry.DisplayNameSnapshot, StringComparer.Ordinal)
                .Select((item, index) =>
                {
                    var hidden = item.Entry.WasAnonymous && !viewer.CanAudit;
                    var score = item.Score!;
                    return new SeasonProblemLeaderboardEntryDto
                    {
                        Rank = index + 1,
                        UserId = hidden ? null : item.Entry.UserId,
                        UserName = viewer.CanAudit ? item.Entry.DisplayNameSnapshot : null,
                        DisplayName = hidden ? item.Entry.Alias : item.Entry.DisplayNameSnapshot,
                        Alias = item.Entry.Alias,
                        IsAnonymous = item.Entry.WasAnonymous,
                        IsCurrentUser = viewer.UserId == item.Entry.UserId,
                        BaseScore = score.BaseScore,
                        EarnedBaseScore = score.EarnedBaseScore,
                        TimeRank = score.TimeRank,
                        TimeBonus = score.TimeBonus,
                        PerformanceLanguage = score.PerformanceLanguage,
                        RuntimeMs = score.RuntimeMs,
                        RuntimeBaselineMs = score.RuntimeBaselineMs,
                        RuntimeBonus = score.RuntimeBonus,
                        MemoryKb = score.MemoryKb,
                        MemoryBaselineKb = score.MemoryBaselineKb,
                        MemoryBonus = score.MemoryBonus,
                        TotalProblemScore = score.FinalProblemScore,
                        FirstFullScoreAt = score.FirstFullScoreAt
                    };
                }).ToList();

            return new SeasonProblemLeaderboardDto { Season = ToDto(season), Problem = problemDto, Entries = archived };
        }

        var rows = await LoadEligibleScoreRowsAsync(season.Id, cancellationToken);
        var aliases = await identityService.EnsureAliasesAsync(season.Id, rows.Select(row => row.UserId), cancellationToken);
        var live = rows.Select(row => new { User = row, Score = row.Problems.SingleOrDefault(score => score.ProblemId == seasonProblem.ProblemId) })
            .Where(item => item.Score is not null)
            .OrderByDescending(item => item.Score!.TotalProblemScore)
            .ThenByDescending(item => item.Score!.EarnedBaseScore)
            .ThenBy(item => item.Score!.TimeRank ?? int.MaxValue)
            .ThenByDescending(item => item.Score!.RuntimeBonus + item.Score.MemoryBonus)
            .ThenBy(item => item.Score!.FirstFullScoreAt)
            .ThenBy(item => item.User.IsAnonymous ? aliases[item.User.UserId] : item.User.UserName, StringComparer.Ordinal)
            .Select((item, index) =>
            {
                var identity = LeaderboardIdentityService.Project(
                    new LeaderboardIdentityUser(item.User.UserId, item.User.UserName, item.User.AvatarUrl, item.User.IsAnonymous),
                    viewer,
                    aliases);
                var score = item.Score!;
                return new SeasonProblemLeaderboardEntryDto
                {
                    Rank = index + 1,
                    UserId = identity.UserId,
                    UserName = viewer.CanAudit ? item.User.UserName : null,
                    DisplayName = identity.DisplayName,
                    Alias = identity.Alias,
                    IsAnonymous = item.User.IsAnonymous,
                    IsCurrentUser = viewer.UserId == item.User.UserId,
                    BaseScore = score.BaseScore,
                    EarnedBaseScore = score.EarnedBaseScore,
                    TimeRank = score.TimeRank,
                    TimeBonus = score.TimeBonus,
                    PerformanceLanguage = score.PerformanceLanguage,
                    RuntimeMs = score.RuntimeMs,
                    RuntimeBaselineMs = score.RuntimeBaselineMs,
                    RuntimeBonus = score.RuntimeBonus,
                    MemoryKb = score.MemoryKb,
                    MemoryBaselineKb = score.MemoryBaselineKb,
                    MemoryBonus = score.MemoryBonus,
                    TotalProblemScore = score.TotalProblemScore,
                    FirstFullScoreAt = score.FirstFullScoreAt
                };
            }).ToList();

        return new SeasonProblemLeaderboardDto { Season = ToDto(season), Problem = problemDto, Entries = live };
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
                && score.FirstFullScoreAt.HasValue
                && user.Role == UserRole.Answerer
                && !user.IsBlacklisted
                && !user.IsDeleted
            select new
            {
                score.UserId,
                ScoreId = score.Id,
                user.UserName,
                user.AvatarUrl,
                user.IsLeaderboardAnonymous,
                score.ProblemId,
                score.SeasonProblemId,
                ProblemTitle = problem.Title,
                seasonProblem.BaseScore,
                EarnedBaseScore = score.BestBaseScore,
                score.FirstFullScoreAt,
                score.FirstFullSubmissionId,
                score.BestPerformanceSubmissionId,
                score.BestPerformanceLanguage,
                score.BestRuntimeMs,
                score.BestMemoryKb,
                score.BestPerformanceFinishedAt,
                score.LastScoreImprovedAt
            }).ToListAsync(cancellationToken);

        var season = await dbContext.LeaderboardSeasons.AsNoTracking()
            .SingleAsync(item => item.Id == seasonId, cancellationToken);
        var rules = LeaderboardScoringRulesSerializer.Deserialize(season.ScoringRulesJson);
        var benchmarks = await dbContext.LeaderboardSeasonProblemBenchmarks.AsNoTracking()
            .Where(item => item.SeasonProblem!.SeasonId == seasonId)
            .Select(item => new { item.SeasonProblemId, item.Language, item.RuntimeBaselineMs, item.MemoryBaselineKb })
            .ToListAsync(cancellationToken);

        var calculated = scores.GroupBy(score => new { score.ProblemId, score.BaseScore })
            .SelectMany(group => scoringEngine.CalculateProblemScores(
                group.Key.BaseScore,
                rules,
                group.Select(score => new LeaderboardProblemScoreFact(
                    score.ScoreId,
                    score.UserId,
                    score.EarnedBaseScore,
                    score.FirstFullScoreAt!.Value,
                    score.FirstFullSubmissionId,
                    score.BestPerformanceSubmissionId.HasValue && score.BestPerformanceLanguage.HasValue && score.BestPerformanceFinishedAt.HasValue
                        ? new LeaderboardPerformanceCandidate(
                            score.BestPerformanceSubmissionId.Value,
                            score.BestPerformanceLanguage.Value,
                            score.BestRuntimeMs,
                            score.BestMemoryKb,
                            score.BestPerformanceFinishedAt.Value)
                        : null,
                    score.LastScoreImprovedAt)).ToList(),
                benchmarks.Where(item => item.SeasonProblemId == group.First().SeasonProblemId)
                    .Select(item => new LeaderboardProblemBenchmarkFact(item.Language, item.RuntimeBaselineMs, item.MemoryBaselineKb))
                    .ToList()))
            .ToDictionary(item => item.ScoreId);

        return scores.GroupBy(score => new { score.UserId, score.UserName, score.AvatarUrl, score.IsLeaderboardAnonymous })
            .Select(group => new ScoreUserRow(
                group.Key.UserId,
                group.Key.UserName,
                group.Key.AvatarUrl,
                group.Key.IsLeaderboardAnonymous,
                group.Sum(score => score.EarnedBaseScore),
                group.Sum(score => calculated[score.ScoreId].TimeBonus),
                group.Sum(score => calculated[score.ScoreId].RuntimeBonus),
                group.Sum(score => calculated[score.ScoreId].MemoryBonus),
                group.Count(),
                group.Max(score => score.LastScoreImprovedAt),
                group.Select(score =>
                {
                    var value = calculated[score.ScoreId];
                    return new ScoreProblemRow(
                        score.ProblemId,
                        score.ProblemTitle,
                        score.BaseScore,
                        score.EarnedBaseScore,
                        value.TimeRank,
                        value.TimeBonus,
                        value.Performance?.Candidate.Language,
                        value.Performance?.Candidate.RuntimeMs,
                        value.Performance?.RuntimeBaselineMs,
                        value.RuntimeBonus,
                        value.Performance?.Candidate.MemoryKb,
                        value.Performance?.MemoryBaselineKb,
                        value.MemoryBonus,
                        value.TotalProblemScore,
                        value.FirstFullScoreAt);
                }).ToList()))
            .ToList();
    }

    private static IReadOnlyList<RankedScoreUserRow> Rank(
        IEnumerable<ScoreUserRow> rows,
        IReadOnlyDictionary<Guid, string> aliases)
    {
        return rows.OrderByDescending(row => row.TotalScore)
            .ThenByDescending(row => row.SolvedCount)
            .ThenByDescending(row => row.BaseScore)
            .ThenByDescending(row => row.PerformanceBonus)
            .ThenBy(row => row.LastScoreImprovedAt)
            .ThenBy(row => row.IsAnonymous ? aliases[row.UserId] : row.UserName, StringComparer.Ordinal)
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

        return Result<LeaderboardSeasonArchiveDto>.Success(BuildArchiveDto(season, canAudit: true, entries));
    }

    private static LeaderboardSeasonArchiveDto BuildArchiveDto(
        LeaderboardSeason season,
        bool canAudit,
        IReadOnlyCollection<LeaderboardSeasonArchiveEntry>? archiveEntries = null) => new()
    {
        SeasonId = season.Id,
        SeasonName = season.Name,
        Entries = (archiveEntries ?? season.ArchiveEntries).OrderBy(entry => entry.FinalRank).Select(entry => new LeaderboardSeasonArchiveEntryDto
        {
                UserId = entry.WasAnonymous && !canAudit ? null : entry.UserId,
                Alias = entry.Alias,
                DisplayNameSnapshot = entry.WasAnonymous && !canAudit ? entry.Alias : entry.DisplayNameSnapshot,
                WasAnonymous = entry.WasAnonymous,
                FinalRank = entry.FinalRank,
                FinalScore = entry.FinalScore,
                FinalBaseScore = entry.FinalBaseScore,
                FinalTimeBonus = entry.FinalTimeBonus,
                FinalRuntimeBonus = entry.FinalRuntimeBonus,
                FinalMemoryBonus = entry.FinalMemoryBonus,
                SolvedCount = entry.SolvedCount,
                LastScoreImprovedAt = entry.LastScoreImprovedAt,
                ProblemScores = entry.ProblemScores.Select(ToArchiveProblemDto).ToList()
            }).ToList()
    };

    private static LeaderboardSeasonArchiveProblemScoreDto ToArchiveProblemDto(LeaderboardSeasonArchiveProblemScore score) => new()
    {
        ProblemId = score.ProblemId,
        ProblemTitleSnapshot = score.ProblemTitleSnapshot,
        BaseScore = score.BaseScore,
        EarnedBaseScore = score.EarnedBaseScore,
        TimeRank = score.TimeRank,
        FirstFullScoreAt = score.FirstFullScoreAt,
        TimeBonus = score.TimeBonus,
        PerformanceLanguage = score.PerformanceLanguage,
        RuntimeMs = score.RuntimeMs,
        RuntimeBaselineMs = score.RuntimeBaselineMs,
        RuntimeBonus = score.RuntimeBonus,
        MemoryKb = score.MemoryKb,
        MemoryBaselineKb = score.MemoryBaselineKb,
        MemoryBonus = score.MemoryBonus,
        FinalProblemScore = score.FinalProblemScore
    };

    private async Task<LeaderboardSeason?> LoadCurrentSeasonAsync(CancellationToken cancellationToken) =>
        await dbContext.LeaderboardSeasons
            .Include(season => season.Boards).ThenInclude(board => board.Challenge)
            .Include(season => season.Problems).ThenInclude(problem => problem.Problem)
            .Include(season => season.Problems).ThenInclude(problem => problem.Benchmarks)
            .Include(season => season.ArchiveEntries).ThenInclude(entry => entry.ProblemScores)
            .SingleOrDefaultAsync(season => season.IsCurrent, cancellationToken);

    private async Task<LeaderboardSeason?> LoadSeasonAsync(Guid seasonId, CancellationToken cancellationToken) =>
        await dbContext.LeaderboardSeasons
            .Include(season => season.Boards).ThenInclude(board => board.Challenge)
            .Include(season => season.Problems).ThenInclude(problem => problem.Problem)
            .Include(season => season.Problems).ThenInclude(problem => problem.Benchmarks)
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

    private async Task<Result<User>> RequireAnswererAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId) return Result<User>.Failure("Unauthorized.");
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(
            user => user.Id == userId && !user.IsDeleted && !user.IsBlacklisted,
            cancellationToken);
        return user?.Role == UserRole.Answerer
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

    private LeaderboardSeasonDto ToDto(LeaderboardSeason season, IReadOnlyDictionary<Guid, int>? currentScores = null) => new()
    {
        Id = season.Id,
        Name = season.Name,
        StartAt = season.StartAt,
        FreezeAt = season.FreezeAt,
        PublicUntil = season.PublicUntil,
        Status = season.Status,
        EffectiveStatus = LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow()),
        IsCurrent = season.IsCurrent,
        ActivatedAt = season.ActivatedAt,
        FrozenAt = season.FrozenAt,
        FinalizedAt = season.FinalizedAt,
        ArchivedAt = season.ArchivedAt,
        ManuallyFrozenAt = season.ManuallyFrozenAt,
        ScoringRules = LeaderboardScoringRulesSerializer.Deserialize(season.ScoringRulesJson),
        Boards = season.Boards.OrderBy(board => board.BoardType).ThenBy(board => board.Challenge?.Title).Select(board => new LeaderboardSeasonBoardDto
        {
            Id = board.Id,
            BoardType = board.BoardType,
            ChallengeId = board.ChallengeId,
            ChallengeTitle = board.Challenge?.Title
        }).ToList(),
        Problems = season.Problems.OrderBy(problem => problem.CreatedAt).Select(problem => new LeaderboardSeasonProblemDto
        {
            Id = problem.Id,
            ProblemId = problem.ProblemId,
            ProblemTitle = problem.Problem?.Title ?? "题目已删除",
            BaseScore = season.Status == LeaderboardSeasonStatus.Scheduled
                && LeaderboardSeasonLifecycle.GetEffectiveStatus(season, timeProvider.GetUtcNow()) == LeaderboardSeasonStatus.Scheduled
                    ? currentScores?.GetValueOrDefault(problem.ProblemId) ?? problem.BaseScore
                    : problem.BaseScore,
            AllowedLanguagesMask = problem.Problem?.AllowedLanguagesMask ?? 0,
            Benchmarks = problem.Benchmarks.OrderBy(item => item.Language).Select(item => new LeaderboardSeasonProblemBenchmarkDto
            {
                Language = item.Language,
                RuntimeBaselineMs = item.RuntimeBaselineMs,
                MemoryBaselineKb = item.MemoryBaselineKb
            }).ToList()
        }).ToList()
    };

    private async Task SynchronizeScheduledProblemScoresAsync(LeaderboardSeason season, CancellationToken cancellationToken)
    {
        var currentScores = await ProblemScoreQuery.GetTotalsAsync(
            dbContext,
            season.Problems.Select(problem => problem.ProblemId),
            cancellationToken);
        foreach (var seasonProblem in season.Problems)
        {
            seasonProblem.BaseScore = currentScores.GetValueOrDefault(seasonProblem.ProblemId);
        }
    }

    private async Task<string?> SynchronizeBoardsAsync(LeaderboardSeason season, bool includeGlobalBoard, IReadOnlyCollection<Guid> challengeIds, CancellationToken cancellationToken)
    {
        var selectedChallengeIds = challengeIds.Distinct().ToHashSet();
        var challenges = await dbContext.Challenges
            .Where(challenge => selectedChallengeIds.Contains(challenge.Id))
            .ToListAsync(cancellationToken);
        if (challenges.Count != selectedChallengeIds.Count) return "Challenge not found.";
        if (challenges.Any(challenge => challenge.StartAt < season.StartAt || challenge.EndAt > season.FreezeAt))
        {
            return "Challenge leaderboard must stay within the season time range.";
        }

        var globalBoard = season.Boards.FirstOrDefault(board => board.BoardType == LeaderboardSeasonBoardType.Global);
        if (includeGlobalBoard && globalBoard is null)
        {
            var board = new LeaderboardSeasonBoard
            {
                Id = Guid.NewGuid(), SeasonId = season.Id, Season = season,
                BoardType = LeaderboardSeasonBoardType.Global, CreatedAt = timeProvider.GetUtcNow()
            };
            season.Boards.Add(board);
            dbContext.LeaderboardSeasonBoards.Add(board);
        }
        else if (!includeGlobalBoard && globalBoard is not null)
        {
            season.Boards.Remove(globalBoard);
            dbContext.LeaderboardSeasonBoards.Remove(globalBoard);
        }

        foreach (var board in season.Boards
                     .Where(board => board.BoardType == LeaderboardSeasonBoardType.Challenge
                         && (!board.ChallengeId.HasValue || !selectedChallengeIds.Contains(board.ChallengeId.Value)))
                     .ToList())
        {
            season.Boards.Remove(board);
            dbContext.LeaderboardSeasonBoards.Remove(board);
        }

        var existingChallengeIds = season.Boards
            .Where(board => board.BoardType == LeaderboardSeasonBoardType.Challenge && board.ChallengeId.HasValue)
            .Select(board => board.ChallengeId!.Value)
            .ToHashSet();
        var newChallengeBoards = challenges.Where(challenge => !existingChallengeIds.Contains(challenge.Id)).Select(challenge => new LeaderboardSeasonBoard
        {
            Id = Guid.NewGuid(), SeasonId = season.Id, Season = season,
            BoardType = LeaderboardSeasonBoardType.Challenge, ChallengeId = challenge.Id,
            Challenge = challenge, CreatedAt = timeProvider.GetUtcNow()
        }).ToList();
        season.Boards.AddRange(newChallengeBoards);
        dbContext.LeaderboardSeasonBoards.AddRange(newChallengeBoards);
        return null;
    }

    private static bool IsLanguageAllowed(int mask, JudgeLanguage language)
    {
        var flag = language switch
        {
            JudgeLanguage.Cpp17 => 1,
            JudgeLanguage.C11 => 2,
            JudgeLanguage.CSharp => 4,
            _ => 0
        };
        return flag != 0 && (mask == 0 || (mask & flag) != 0);
    }

    private sealed record ScoreProblemRow(
        Guid ProblemId,
        string ProblemTitle,
        int BaseScore,
        int EarnedBaseScore,
        int? TimeRank,
        int TimeBonus,
        JudgeLanguage? PerformanceLanguage,
        int? RuntimeMs,
        int? RuntimeBaselineMs,
        int RuntimeBonus,
        int? MemoryKb,
        int? MemoryBaselineKb,
        int MemoryBonus,
        int TotalProblemScore,
        DateTimeOffset FirstFullScoreAt);

    private record ScoreUserRow(
        Guid UserId,
        string UserName,
        string? AvatarUrl,
        bool IsAnonymous,
        int BaseScore,
        int TimeBonus,
        int RuntimeBonus,
        int MemoryBonus,
        int SolvedCount,
        DateTimeOffset LastScoreImprovedAt,
        IReadOnlyList<ScoreProblemRow> Problems)
    {
        public int TotalScore => BaseScore + TimeBonus + RuntimeBonus + MemoryBonus;

        public int PerformanceBonus => RuntimeBonus + MemoryBonus;
    }

    private sealed record RankedScoreUserRow(ScoreUserRow Row, int Rank) : ScoreUserRow(
        Row.UserId,
        Row.UserName,
        Row.AvatarUrl,
        Row.IsAnonymous,
        Row.BaseScore,
        Row.TimeBonus,
        Row.RuntimeBonus,
        Row.MemoryBonus,
        Row.SolvedCount,
        Row.LastScoreImprovedAt,
        Row.Problems);
}
