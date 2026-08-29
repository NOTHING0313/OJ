namespace OnlineJudge.Infrastructure.Storage;

public sealed class RuntimeStorageOptions
{
    public const string SectionName = "Storage";

    public string? UploadImagesRoot { get; set; }

    public string? ChallengeFilesRoot { get; set; }
}
