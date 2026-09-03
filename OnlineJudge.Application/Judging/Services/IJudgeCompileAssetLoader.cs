using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeCompileAssetLoader
{
    Task<IReadOnlyList<JudgeCompileAsset>> LoadAsync(Guid problemId, JudgeLanguage language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads exactly the assets captured by an immutable problem judge revision, including retained soft-deleted assets.
    /// </summary>
    Task<IReadOnlyList<JudgeCompileAsset>> LoadRevisionAsync(Guid problemJudgeRevisionId, JudgeLanguage language, CancellationToken cancellationToken = default);
}
