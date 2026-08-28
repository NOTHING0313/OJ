using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeCompileAssetLoader
{
    Task<IReadOnlyList<JudgeCompileAsset>> LoadAsync(Guid problemId, JudgeLanguage language, CancellationToken cancellationToken = default);
}
