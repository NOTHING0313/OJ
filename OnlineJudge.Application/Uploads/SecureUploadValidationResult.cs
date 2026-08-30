namespace OnlineJudge.Application.Uploads;

public sealed record SecureUploadValidationResult(
    bool IsValid,
    string? ErrorCode,
    string? ErrorMessage,
    string? CanonicalExtension)
{
    public static SecureUploadValidationResult Success(string extension) => new(true, null, null, extension);

    public static SecureUploadValidationResult Failure(string code, string message) => new(false, code, message, null);
}
