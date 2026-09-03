namespace OnlineJudge.Application.Judging.Models;

public sealed record JudgeLeaseRenewalResult(
    JudgeJobTransitionResult Transition,
    DateTimeOffset? LeaseExpiresAt);
