using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Infrastructure.Storage;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController(IRuntimeStoragePathProvider storagePaths) : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const long MaxRequestSize = MaxFileSize + 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".gif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif"
    };

    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSize)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest("File size must be 5MB or less.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return BadRequest("Unsupported image extension.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest("Unsupported image content type.");
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = storagePaths.CreateUploadImageWriteStream(fileName);
        await file.CopyToAsync(stream, cancellationToken);

        return Ok(new
        {
            url = $"/uploads/images/{fileName}"
        });
    }
}
