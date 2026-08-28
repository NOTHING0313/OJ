using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class SeasonScoreService(OnlineJudgeDbContext dbContext, TimeProvider timeProvider) : ISeasonScoreService
{
    public async Task ApplySubmissionResultAsync(SeasonSubmissionResult submission, CancellationToken cancellationToken = default)
    {
        if (submission.Status != JudgeStatus.Accepted) return;

        var user = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == submission.UserId)
            .Select(user => new { user.Role, user.IsBlacklisted, user.IsDeleted })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || user.Role != UserRole.Answerer || user.IsBlacklisted || user.IsDeleted) return;

        var seasonProblem = await dbContext.LeaderboardSeasonProblems
            .Include(item => item.Season)
            .FirstOrDefaultAsync(item => item.ProblemId == submission.ProblemId && item.Season!.IsCurrent, cancellationToken);

        var now = timeProvider.GetUtcNow();
        if (seasonProblem?.Season is null
            || LeaderboardSeasonLifecycle.GetEffectiveStatus(seasonProblem.Season, now) != LeaderboardSeasonStatus.Active)
        {
            return;
        }

        if (seasonProblem.Season.Status == LeaderboardSeasonStatus.Scheduled)
        {
            seasonProblem.Season.Status = LeaderboardSeasonStatus.Active;
            seasonProblem.Season.UpdatedAt = now;
        }

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
                FirstFullScoreAt = now,
                BestPerformanceSubmissionId = submission.SubmissionId,
                BestRuntimeMs = submission.RuntimeMs,
                BestMemoryKb = submission.MemoryKb,
                LastScoreImprovedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
            return;
        }

        var changed = false;
        if (score.BestBaseScore < seasonProblem.BaseScore)
        {
            score.BestBaseScore = seasonProblem.BaseScore;
            score.IsFullScore = true;
            score.LastScoreImprovedAt = now;
            changed = true;
        }

        if (!score.FirstFullScoreAt.HasValue)
        {
            score.FirstFullScoreAt = now;
            score.IsFullScore = true;
            changed = true;
        }

        if (IsBetterPerformance(submission.RuntimeMs, submission.MemoryKb, score.BestRuntimeMs, score.BestMemoryKb))
        {
            score.BestPerformanceSubmissionId = submission.SubmissionId;
            score.BestRuntimeMs = submission.RuntimeMs;
            score.BestMemoryKb = submission.MemoryKb;
            changed = true;
        }

        if (changed) score.UpdatedAt = now;
    }

    private static bool IsBetterPerformance(int? candidateRuntime, int? candidateMemory, int? currentRuntime, int? currentMemory)
    {
        var candidateRuntimeValue = candidateRuntime ?? int.MaxValue;
        var currentRuntimeValue = currentRuntime ?? int.MaxValue;
        if (candidateRuntimeValue != currentRuntimeValue) return candidateRuntimeValue < currentRuntimeValue;

        var candidateMemoryValue = candidateMemory ?? int.MaxValue;
        var currentMemoryValue = currentMemory ?? int.MaxValue;
        return candidateMemoryValue < currentMemoryValue;
    }
}
