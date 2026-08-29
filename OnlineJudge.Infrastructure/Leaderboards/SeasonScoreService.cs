using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class SeasonScoreService(
    OnlineJudgeDbContext dbContext,
    TimeProvider timeProvider,
    ILeaderboardScoringEngine scoringEngine) : ISeasonScoreService
{
    public SeasonScoreService(OnlineJudgeDbContext dbContext, TimeProvider timeProvider)
        : this(dbContext, timeProvider, new LeaderboardScoringEngine())
    {
    }

    public async Task<SeasonScoreApplyResult> ApplySubmissionResultAsync(SeasonSubmissionResult submission, CancellationToken cancellationToken = default)
    {
        if (submission.Status != JudgeStatus.Accepted) return SeasonScoreApplyResult.Ignored;

        var user = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == submission.UserId)
            .Select(user => new { user.Role, user.IsBlacklisted, user.IsDeleted })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || user.Role != UserRole.Answerer || user.IsBlacklisted || user.IsDeleted) return SeasonScoreApplyResult.Ignored;

        var seasonProblem = await dbContext.LeaderboardSeasonProblems
            .Include(item => item.Season)
            .Include(item => item.Benchmarks)
            .FirstOrDefaultAsync(item => item.ProblemId == submission.ProblemId && item.Season!.IsCurrent, cancellationToken);

        var now = timeProvider.GetUtcNow();
        if (seasonProblem?.Season is null
            || seasonProblem.Season.Status == LeaderboardSeasonStatus.Archived
            || submission.CreatedAt < seasonProblem.Season.StartAt
            || submission.CreatedAt >= LeaderboardSeasonLifecycle.GetSubmissionCutoff(seasonProblem.Season))
        {
            return SeasonScoreApplyResult.Ignored;
        }

        if (seasonProblem.Season.Status == LeaderboardSeasonStatus.Scheduled)
        {
            seasonProblem.Season.Status = LeaderboardSeasonStatus.Active;
            seasonProblem.Season.ActivatedAt ??= now;
            seasonProblem.Season.UpdatedAt = now;
        }

        await ScoringIdentityTransactionLock.AcquireAsync(
            dbContext,
            "season-problem-user",
            [seasonProblem.SeasonId, submission.ProblemId, submission.UserId],
            cancellationToken);

        var score = await dbContext.LeaderboardUserProblemScores.FirstOrDefaultAsync(
            item => item.SeasonId == seasonProblem.SeasonId
                && item.ProblemId == submission.ProblemId
                && item.UserId == submission.UserId,
            cancellationToken);

        if (score is null)
        {
            dbContext.LeaderboardUserProblemScores.Add(new LeaderboardUserProblemScore
            {
                Id = Guid.NewGuid(),
                SeasonId = seasonProblem.SeasonId,
                SeasonProblemId = seasonProblem.Id,
                ProblemId = seasonProblem.ProblemId,
                UserId = submission.UserId,
                BestBaseScore = seasonProblem.BaseScore,
                IsFullScore = true,
                FirstFullScoreAt = submission.CreatedAt,
                FirstFullSubmissionId = submission.SubmissionId,
                BestPerformanceSubmissionId = submission.SubmissionId,
                BestPerformanceLanguage = submission.Language,
                BestRuntimeMs = submission.RuntimeMs,
                BestMemoryKb = submission.MemoryKb,
                BestPerformanceFinishedAt = submission.FinishedAt,
                LastScoreImprovedAt = submission.CreatedAt,
                CreatedAt = now,
                UpdatedAt = now
            });
            return new SeasonScoreApplyResult(true, seasonProblem.SeasonId, seasonProblem.Season.Status is LeaderboardSeasonStatus.Frozen or LeaderboardSeasonStatus.Public);
        }

        var changed = false;
        var scoreIncreased = false;
        if (score.BestBaseScore < seasonProblem.BaseScore)
        {
            score.BestBaseScore = seasonProblem.BaseScore;
            score.IsFullScore = true;
            changed = true;
            scoreIncreased = true;
        }

        if (!score.FirstFullScoreAt.HasValue || submission.CreatedAt < score.FirstFullScoreAt.Value)
        {
            score.FirstFullScoreAt = submission.CreatedAt;
            score.FirstFullSubmissionId = submission.SubmissionId;
            score.IsFullScore = true;
            changed = true;
            scoreIncreased = true;
        }

        var rules = LeaderboardScoringRulesSerializer.Deserialize(seasonProblem.Season.ScoringRulesJson);
        var benchmarks = seasonProblem.Benchmarks
            .Select(item => new LeaderboardProblemBenchmarkFact(item.Language, item.RuntimeBaselineMs, item.MemoryBaselineKb))
            .ToList();
        var candidate = scoringEngine.CalculatePerformance(
            seasonProblem.BaseScore,
            rules,
            new LeaderboardPerformanceCandidate(
                submission.SubmissionId,
                submission.Language,
                submission.RuntimeMs,
                submission.MemoryKb,
                submission.FinishedAt),
            benchmarks);
        var current = CreateCurrentPerformance(score, seasonProblem.BaseScore, rules, benchmarks);
        if (scoringEngine.IsBetterPerformance(candidate, current))
        {
            score.BestPerformanceSubmissionId = submission.SubmissionId;
            score.BestPerformanceLanguage = submission.Language;
            score.BestRuntimeMs = submission.RuntimeMs;
            score.BestMemoryKb = submission.MemoryKb;
            score.BestPerformanceFinishedAt = submission.FinishedAt;
            changed = true;
            scoreIncreased |= candidate.PerformanceBonus > (current?.PerformanceBonus ?? 0);
        }

        if (scoreIncreased) score.LastScoreImprovedAt = submission.CreatedAt;
        if (changed) score.UpdatedAt = now;
        return changed
            ? new SeasonScoreApplyResult(true, seasonProblem.SeasonId, seasonProblem.Season.Status is LeaderboardSeasonStatus.Frozen or LeaderboardSeasonStatus.Public)
            : SeasonScoreApplyResult.Ignored;
    }

    private LeaderboardPerformanceScore? CreateCurrentPerformance(
        LeaderboardUserProblemScore score,
        int baseScore,
        LeaderboardScoringRules rules,
        IReadOnlyCollection<LeaderboardProblemBenchmarkFact> benchmarks)
    {
        if (!score.BestPerformanceSubmissionId.HasValue
            || !score.BestPerformanceLanguage.HasValue
            || !score.BestPerformanceFinishedAt.HasValue)
        {
            return null;
        }

        return scoringEngine.CalculatePerformance(
            baseScore,
            rules,
            new LeaderboardPerformanceCandidate(
                score.BestPerformanceSubmissionId.Value,
                score.BestPerformanceLanguage.Value,
                score.BestRuntimeMs,
                score.BestMemoryKb,
                score.BestPerformanceFinishedAt.Value),
            benchmarks);
    }
}
