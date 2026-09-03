namespace OnlineJudge.Application.Judging.Models;

public sealed record JudgeJobLease(
    Guid SubmissionId,
    Guid LeaseToken,
    int AttemptNumber,
    DateTimeOffset LeaseExpiresAt);
