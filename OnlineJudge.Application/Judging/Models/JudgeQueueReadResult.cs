namespace OnlineJudge.Application.Judging.Models;

public sealed record JudgeQueueReadResult(bool IsAvailable, Guid? SubmissionId)
{
    public static JudgeQueueReadResult Empty { get; } = new(true, null);

    public static JudgeQueueReadResult Unavailable { get; } = new(false, null);
}
