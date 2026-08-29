using OnlineJudge.Application.Challenges;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Tests.Challenges;

public class ChallengeProgressUpdaterTests
{
    [Fact]
    public void TryApply_RepeatedCandidates_PreservesHistoricalMaximumAndBestSubmission()
    {
        var completion = new ChallengeTaskCompletion();
        var submissions = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var now = DateTimeOffset.UtcNow;

        Assert.True(ChallengeProgressUpdater.TryApply(completion, 40, false, 100, submissions[0], now));
        Assert.True(ChallengeProgressUpdater.TryApply(completion, 70, false, 100, submissions[1], now.AddMinutes(1)));
        Assert.False(ChallengeProgressUpdater.TryApply(completion, 50, false, 100, submissions[2], now.AddMinutes(2)));
        Assert.True(ChallengeProgressUpdater.TryApply(completion, 100, true, 100, submissions[3], now.AddMinutes(3)));
        var completedAt = completion.CompletedAt;
        var updatedAt = completion.UpdatedAt;
        Assert.False(ChallengeProgressUpdater.TryApply(completion, 80, false, 100, submissions[4], now.AddMinutes(4)));

        Assert.Equal(100, completion.Score);
        Assert.Equal(submissions[3], completion.SubmissionId);
        Assert.Equal(completedAt, completion.CompletedAt);
        Assert.Equal(updatedAt, completion.UpdatedAt);
    }

    [Fact]
    public void TryApply_LowerScore_DoesNotChangeBestResultOrTimestamp()
    {
        var oldSubmissionId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var completion = new ChallengeTaskCompletion { Score = 150, IsCompleted = false, SubmissionId = oldSubmissionId, UpdatedAt = updatedAt };

        var changed = ChallengeProgressUpdater.TryApply(completion, 90, false, 300, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(changed);
        Assert.Equal(150, completion.Score);
        Assert.Equal(oldSubmissionId, completion.SubmissionId);
        Assert.Equal(updatedAt, completion.UpdatedAt);
        Assert.False(completion.IsCompleted);
    }

    [Fact]
    public void TryApply_HigherPartialScore_UpdatesBestResultAndTimestamp()
    {
        var oldUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var now = DateTimeOffset.UtcNow;
        var submissionId = Guid.NewGuid();
        var completion = new ChallengeTaskCompletion { Score = 150, IsCompleted = false, UpdatedAt = oldUpdatedAt };

        var changed = ChallengeProgressUpdater.TryApply(completion, 210, false, 300, submissionId, now);

        Assert.True(changed);
        Assert.Equal(210, completion.Score);
        Assert.Equal(submissionId, completion.SubmissionId);
        Assert.Equal(now, completion.UpdatedAt);
        Assert.False(completion.IsCompleted);
    }

    [Fact]
    public void TryApply_FirstAcceptedResult_CompletesAndUsesTaskFullScore()
    {
        var now = DateTimeOffset.UtcNow;
        var submissionId = Guid.NewGuid();
        var completion = new ChallengeTaskCompletion { Score = 150, IsCompleted = false, UpdatedAt = now.AddMinutes(-10) };

        var changed = ChallengeProgressUpdater.TryApply(completion, 300, true, 300, submissionId, now);

        Assert.True(changed);
        Assert.Equal(300, completion.Score);
        Assert.True(completion.IsCompleted);
        Assert.Equal(now, completion.CompletedAt);
        Assert.Equal(now, completion.UpdatedAt);
        Assert.Equal(submissionId, completion.SubmissionId);
    }

    [Fact]
    public void TryApply_RepeatedAcceptedResult_DoesNotRefreshTimestamp()
    {
        var submissionId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var completion = new ChallengeTaskCompletion { Score = 300, IsCompleted = true, SubmissionId = submissionId, UpdatedAt = updatedAt, CompletedAt = updatedAt };

        var changed = ChallengeProgressUpdater.TryApply(completion, 300, true, 300, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.False(changed);
        Assert.Equal(300, completion.Score);
        Assert.Equal(submissionId, completion.SubmissionId);
        Assert.Equal(updatedAt, completion.UpdatedAt);
        Assert.Equal(updatedAt, completion.CompletedAt);
    }
}
