namespace OnlineJudge.Application.Uploads;

public static class SecureUploadErrorCodes
{
    public const string InvalidType = "UPLOAD_INVALID_TYPE";
    public const string TypeMismatch = "UPLOAD_TYPE_MISMATCH";
    public const string TooLarge = "UPLOAD_TOO_LARGE";
    public const string InvalidFileName = "UPLOAD_INVALID_FILENAME";
    public const string ArchiveUnsafe = "UPLOAD_ARCHIVE_UNSAFE";
    public const string ArchiveTooLarge = "UPLOAD_ARCHIVE_TOO_LARGE";
    public const string ArchiveTooComplex = "UPLOAD_ARCHIVE_TOO_COMPLEX";
}
