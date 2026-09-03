using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Judging;

public class JudgeCompileAssetLoader(OnlineJudgeDbContext dbContext, IProblemJudgeAssetStorage storage) : IJudgeCompileAssetLoader
{
    public async Task<IReadOnlyList<JudgeCompileAsset>> LoadAsync(Guid problemId, JudgeLanguage language, CancellationToken cancellationToken = default)
    {
        var records = await dbContext.ProblemJudgeAssets
            .AsNoTracking()
            .Where(asset => asset.ProblemId == problemId && asset.Language == language && !asset.IsDeleted)
            .OrderBy(asset => asset.OriginalFileName)
            .ToListAsync(cancellationToken);

        var assets = new List<JudgeCompileAsset>(records.Count);
        foreach (var record in records)
        {
            assets.Add(new JudgeCompileAsset
            {
                FileName = record.OriginalFileName,
                Content = await storage.ReadTextAsync(record.StorageRelativePath, record.FileSizeBytes, record.Sha256, cancellationToken)
            });
        }

        return assets;
    }

    public async Task<IReadOnlyList<JudgeCompileAsset>> LoadRevisionAsync(
        Guid problemJudgeRevisionId,
        JudgeLanguage language,
        CancellationToken cancellationToken = default)
    {
        var records = await dbContext.ProblemJudgeRevisionAssets
            .AsNoTracking()
            .Where(link => link.ProblemJudgeRevisionId == problemJudgeRevisionId
                && link.ProblemJudgeAsset != null
                && link.ProblemJudgeAsset.Language == language)
            .OrderBy(link => link.Order)
            .Select(link => new
            {
                link.ProblemJudgeAsset!.OriginalFileName,
                link.ProblemJudgeAsset.StorageRelativePath,
                link.ProblemJudgeAsset.FileSizeBytes,
                link.ProblemJudgeAsset.Sha256
            })
            .ToListAsync(cancellationToken);

        var assets = new List<JudgeCompileAsset>(records.Count);
        foreach (var record in records)
        {
            assets.Add(new JudgeCompileAsset
            {
                FileName = record.OriginalFileName,
                Content = await storage.ReadTextAsync(record.StorageRelativePath, record.FileSizeBytes, record.Sha256, cancellationToken)
            });
        }

        return assets;
    }
}
