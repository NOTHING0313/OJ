using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IProblemJudgeAssetStorage
{
    Task<StoredJudgeAssetFile> WriteAsync(Guid problemId, JudgeLanguage language, string extension, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task<string> ReadTextAsync(string storageRelativePath, long expectedFileSizeBytes, string expectedSha256, CancellationToken cancellationToken = default);

    Task<byte[]?> DeleteWithBackupAsync(string storageRelativePath, CancellationToken cancellationToken = default);

    Task RestoreAsync(string storageRelativePath, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(string storageRelativePath, CancellationToken cancellationToken = default);
}
