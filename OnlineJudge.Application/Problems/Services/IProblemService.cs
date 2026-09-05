using OnlineJudge.Application.Common;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;

namespace OnlineJudge.Application.Problems.Services;

public interface IProblemService
{
    Task<Result<PagedResult<ProblemListItemDto>>> QueryProblemsAsync(ProblemQueryRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ProblemListItemDto>>> GetProblemsAsync(CancellationToken cancellationToken = default);

    Task<Result<ProblemDetailDto>> GetProblemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProblemDetailDto>> GetProblemAuthoringAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ProblemDetailDto>> CreateProblemAsync(CreateProblemRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProblemDetailDto>> UpdateProblemAsync(Guid id, UpdateProblemRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteProblemAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<TestCaseDto>> AddTestCaseAsync(Guid problemId, CreateTestCaseRequest request, CancellationToken cancellationToken = default);

    Task<Result<TestCaseDto>> UpdateTestCaseAsync(Guid problemId, Guid testCaseId, UpdateTestCaseRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteTestCaseAsync(Guid problemId, Guid testCaseId, CancellationToken cancellationToken = default);

    Task<Result<ImportTestCasesResultDto>> ImportTestCasesAsync(Guid problemId, ImportTestCasesRequest request, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TestCaseExportItemDto>>> ExportTestCasesAsync(Guid problemId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ProblemCollaboratorDto>>> GetCollaboratorsAsync(Guid problemId, CancellationToken cancellationToken = default);

    Task<Result<ProblemCollaboratorDto>> GrantCollaboratorAsync(Guid problemId, GrantProblemCollaboratorRequest request, CancellationToken cancellationToken = default);

    Task<Result> RemoveCollaboratorAsync(Guid problemId, Guid userId, CancellationToken cancellationToken = default);
}
