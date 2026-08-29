using OnlineJudge.Application.Common;
using OnlineJudge.Application.HelpDocuments.Dtos;
using OnlineJudge.Application.HelpDocuments.Requests;

namespace OnlineJudge.Application.HelpDocuments.Services;

public interface IHelpDocumentService
{
    Task<Result<IReadOnlyList<HelpDocumentListItemDto>>> GetPublishedAsync(CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HelpDocumentListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> CreateAsync(UpsertHelpDocumentRequest request, CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> UpdateAsync(Guid id, UpsertHelpDocumentRequest request, CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> PublishAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<HelpDocumentDto>> UnpublishAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
