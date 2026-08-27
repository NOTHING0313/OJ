using OnlineJudge.Application.Challenges;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Tests.Challenges;

public class ChallengeProgressUpdaterTests
{
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
