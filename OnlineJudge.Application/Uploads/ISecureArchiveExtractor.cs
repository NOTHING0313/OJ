using OnlineJudge.Application.Common;

namespace OnlineJudge.Application.Uploads;

public interface ISecureArchiveExtractor
{
    Task<Result<IReadOnlyDictionary<string, byte[]>>> ExtractThemePackAsync(Stream content, CancellationToken cancellationToken = default);
}
