namespace OnlineJudge.Application.Uploads;

public interface ISecureUploadValidator
{
    Task<SecureUploadValidationResult> ValidateAsync(SecureUploadRequest request, CancellationToken cancellationToken = default);
}
