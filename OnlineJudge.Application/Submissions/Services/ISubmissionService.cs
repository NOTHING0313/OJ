using OnlineJudge.Application.Common;
using OnlineJudge.Application.Submissions.Dtos;
using OnlineJudge.Application.Submissions.Requests;

namespace OnlineJudge.Application.Submissions.Services;

public interface ISubmissionService
{
    Task<Result<SubmissionDto>> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default);

    Task<Result<SubmissionDto>> GetSubmissionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SubmissionListItemDto>>> QuerySubmissionsAsync(SubmissionQueryRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SubmissionListItemDto>>> GetProblemSubmissionsAsync(Guid problemId, CancellationToken cancellationToken = default);
}
