namespace OnlineJudge.Application.Uploads;

public sealed class SecureUploadRequest
{
    public UploadPolicy Policy { get; init; }

    public string OriginalFileName { get; init; } = string.Empty;

    public string DeclaredContentType { get; init; } = string.Empty;

    public long DeclaredLength { get; init; }

    public Stream Content { get; init; } = Stream.Null;

    public IReadOnlyCollection<string>? AllowedExtensions { get; init; }
}
