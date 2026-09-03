using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeJobStore
{
    Task<JudgeJobLease?> TryClaimAsync(Guid? preferredSubmissionId, string workerId, CancellationToken cancellationToken = default);

    Task<JudgeLeaseRenewalResult> RenewLeaseAsync(JudgeJobLease lease, CancellationToken cancellationToken = default);

    Task<JudgeJobTransitionResult> RequeueAsync(
        JudgeJobLease lease,
        JudgeFailureKind failureKind,
        string error,
        TimeSpan delay,
        CancellationToken cancellationToken = default);

    Task<JudgeJobTransitionResult> DeadLetterAsync(
        JudgeJobLease lease,
        JudgeFailureKind failureKind,
        string internalError,
        string userError,
        CancellationToken cancellationToken = default);
}
