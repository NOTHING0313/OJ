using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController(
    IRuntimeStoragePathProvider storagePaths,
    ISecureUploadValidator uploadValidator,
    SecureUploadOptions uploadOptions) : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const long MaxRequestSize = MaxFileSize + 1024 * 1024;

    [RiskRateLimit(RateLimitPolicies.Upload)]
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSize)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        await using var content = file.OpenReadStream();
        var validation = await uploadValidator.ValidateAsync(new SecureUploadRequest
        {
            Policy = UploadPolicy.Image,
            OriginalFileName = file.FileName,
            DeclaredContentType = file.ContentType,
            DeclaredLength = file.Length,
            Content = content
        }, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest($"{validation.ErrorCode}: {validation.ErrorMessage}");
        }

        var fileName = $"{Guid.NewGuid():N}{validation.CanonicalExtension}";
        try
        {
            await storagePaths.WriteUploadImageAsync(fileName, content, uploadOptions.ImageMaxBytes, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return BadRequest($"{SecureUploadErrorCodes.TooLarge}: The image exceeds the configured size limit.");
        }

        return Ok(new
        {
            url = $"/uploads/images/{fileName}"
        });
    }
}
