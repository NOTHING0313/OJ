using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Application.Challenges;

public static class ChallengeTeamProgressUpdater
{
    public static bool TryApply(
        ChallengeTeamTaskCompletion completion,
        int earnedScore,
        bool isCompleted,
        int taskScore,
        Guid submissionId,
        Guid contributorUserId,
        DateTimeOffset now)
    {
        var changed = false;

        if (earnedScore > completion.Score)
        {
            completion.Score = earnedScore;
            completion.BestSubmissionId = submissionId;
            completion.ContributorUserId = contributorUserId;
            changed = true;
        }

        if (isCompleted && !completion.IsCompleted)
        {
            completion.IsCompleted = true;
            completion.CompletedAt = now;
            completion.Score = Math.Max(completion.Score, Math.Max(0, taskScore));
            completion.BestSubmissionId = submissionId;
            completion.ContributorUserId = contributorUserId;
            changed = true;
        }

        if (changed) completion.UpdatedAt = now;
        return changed;
    }
}
