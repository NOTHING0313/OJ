using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Challenges;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Challenges;

public static class ChallengeBestScoreStore
{
    public static Task UpsertAlgorithmIndividualAsync(
        OnlineJudgeDbContext dbContext,
        Guid challengeId,
        Guid challengeTaskId,
        Guid userId,
        int earnedScore,
        bool isCompleted,
        int taskScore,
        Guid submissionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateScore = isCompleted ? Math.Max(earnedScore, Math.Max(0, taskScore)) : Math.Max(0, earnedScore);
        return UpsertIndividualCoreAsync(
            dbContext, challengeId, challengeTaskId, userId, candidateScore, isCompleted, submissionId, now,
            completion => ChallengeProgressUpdater.TryApply(completion, earnedScore, isCompleted, taskScore, submissionId, now),
            createZeroRecord: false,
            cancellationToken);
    }

    public static Task UpsertFileIndividualAsync(
        OnlineJudgeDbContext dbContext,
        Guid challengeId,
        Guid challengeTaskId,
        Guid userId,
        int score,
        bool isCompleted,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateScore = Math.Max(0, score);
        return UpsertIndividualCoreAsync(
            dbContext, challengeId, challengeTaskId, userId, candidateScore, isCompleted, null, now,
            completion => ApplyFileCandidate(completion, candidateScore, isCompleted, now),
            createZeroRecord: true,
            cancellationToken);
    }

    public static async Task UpsertTeamAsync(
        OnlineJudgeDbContext dbContext,
        Guid challengeId,
        Guid challengeTaskId,
        Guid participantId,
        int earnedScore,
        bool isCompleted,
        int taskScore,
        Guid submissionId,
        Guid contributorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidateScore = isCompleted ? Math.Max(earnedScore, Math.Max(0, taskScore)) : Math.Max(0, earnedScore);
        if (!dbContext.Database.IsRelational())
        {
            var completion = await dbContext.ChallengeTeamTaskCompletions.FirstOrDefaultAsync(
                item => item.ChallengeTeamParticipantId == participantId && item.ChallengeTaskId == challengeTaskId,
                cancellationToken);
            if (completion is null)
            {
                if (candidateScore <= 0 && !isCompleted) return;
                dbContext.ChallengeTeamTaskCompletions.Add(new ChallengeTeamTaskCompletion
                {
                    Id = Guid.NewGuid(), ChallengeId = challengeId, ChallengeTaskId = challengeTaskId,
                    ChallengeTeamParticipantId = participantId, BestSubmissionId = submissionId,
                    ContributorUserId = contributorUserId, Score = candidateScore, IsCompleted = isCompleted,
                    CompletedAt = now, UpdatedAt = now
                });
                return;
            }

            ChallengeTeamProgressUpdater.TryApply(completion, earnedScore, isCompleted, taskScore, submissionId, contributorUserId, now);
            return;
        }

        if (candidateScore <= 0 && !isCompleted) return;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ChallengeTeamTaskCompletions" AS existing
                 ("Id", "ChallengeId", "ChallengeTaskId", "ChallengeTeamParticipantId", "BestSubmissionId", "ContributorUserId", "Score", "IsCompleted", "CompletedAt", "UpdatedAt")
             VALUES
                 ({Guid.NewGuid()}, {challengeId}, {challengeTaskId}, {participantId}, {submissionId}, {contributorUserId}, {candidateScore}, {isCompleted}, {now}, {now})
             ON CONFLICT ("ChallengeTeamParticipantId", "ChallengeTaskId") DO UPDATE SET
                 "Score" = GREATEST(existing."Score", EXCLUDED."Score"),
                 "IsCompleted" = existing."IsCompleted" OR EXCLUDED."IsCompleted",
                 "CompletedAt" = CASE WHEN EXCLUDED."IsCompleted" AND NOT existing."IsCompleted" THEN EXCLUDED."CompletedAt" ELSE existing."CompletedAt" END,
                 "BestSubmissionId" = EXCLUDED."BestSubmissionId",
                 "ContributorUserId" = EXCLUDED."ContributorUserId",
                 "UpdatedAt" = EXCLUDED."UpdatedAt"
             WHERE EXCLUDED."Score" > existing."Score"
                 OR (EXCLUDED."IsCompleted" AND NOT existing."IsCompleted");
             """,
            cancellationToken);
    }

    private static async Task UpsertIndividualCoreAsync(
        OnlineJudgeDbContext dbContext,
        Guid challengeId,
        Guid challengeTaskId,
        Guid userId,
        int candidateScore,
        bool isCompleted,
        Guid? submissionId,
        DateTimeOffset now,
        Func<ChallengeTaskCompletion, bool> applyInMemory,
        bool createZeroRecord,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var completion = await dbContext.ChallengeTaskCompletions.FirstOrDefaultAsync(
                item => item.UserId == userId && item.ChallengeTaskId == challengeTaskId,
                cancellationToken);
            if (completion is null)
            {
                if (candidateScore <= 0 && !isCompleted && !createZeroRecord) return;
                dbContext.ChallengeTaskCompletions.Add(new ChallengeTaskCompletion
                {
                    Id = Guid.NewGuid(), ChallengeId = challengeId, ChallengeTaskId = challengeTaskId,
                    UserId = userId, SubmissionId = submissionId, Score = candidateScore,
                    IsCompleted = isCompleted, CompletedAt = now, UpdatedAt = now
                });
                return;
            }

            applyInMemory(completion);
            return;
        }

        if (candidateScore <= 0 && !isCompleted && !createZeroRecord) return;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ChallengeTaskCompletions" AS existing
                 ("Id", "ChallengeId", "ChallengeTaskId", "UserId", "SubmissionId", "CompletedAt", "UpdatedAt", "IsCompleted", "Score")
             VALUES
                 ({Guid.NewGuid()}, {challengeId}, {challengeTaskId}, {userId}, {submissionId}, {now}, {now}, {isCompleted}, {candidateScore})
             ON CONFLICT ("UserId", "ChallengeTaskId") DO UPDATE SET
                 "Score" = GREATEST(existing."Score", EXCLUDED."Score"),
                 "IsCompleted" = existing."IsCompleted" OR EXCLUDED."IsCompleted",
                 "CompletedAt" = CASE WHEN EXCLUDED."IsCompleted" AND NOT existing."IsCompleted" THEN EXCLUDED."CompletedAt" ELSE existing."CompletedAt" END,
                 "SubmissionId" = EXCLUDED."SubmissionId",
                 "UpdatedAt" = EXCLUDED."UpdatedAt"
             WHERE EXCLUDED."Score" > existing."Score"
                 OR (EXCLUDED."IsCompleted" AND NOT existing."IsCompleted");
             """,
            cancellationToken);
    }

    private static bool ApplyFileCandidate(ChallengeTaskCompletion completion, int candidateScore, bool isCompleted, DateTimeOffset now)
    {
        if (candidateScore <= completion.Score && (!isCompleted || completion.IsCompleted)) return false;
        if (candidateScore > completion.Score) completion.Score = candidateScore;
        if (isCompleted && !completion.IsCompleted)
        {
            completion.IsCompleted = true;
            completion.CompletedAt = now;
        }
        completion.UpdatedAt = now;
        return true;
    }
}
