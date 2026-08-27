using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Application.Challenges;

public static class ChallengeProgressUpdater
{
    public static bool TryApply(ChallengeTaskCompletion completion, int earnedScore, bool isCompleted, int taskScore, Guid submissionId, DateTimeOffset now)
    {
        var changed = false;

        if (earnedScore > completion.Score)
        {
            completion.Score = earnedScore;
            completion.SubmissionId = submissionId;
            changed = true;
        }

        if (isCompleted && !completion.IsCompleted)
        {
            completion.IsCompleted = true;
            completion.CompletedAt = now;
            completion.Score = Math.Max(completion.Score, Math.Max(0, taskScore));
            completion.SubmissionId = submissionId;
            changed = true;
        }

        if (changed) completion.UpdatedAt = now;
        return changed;
    }
}
