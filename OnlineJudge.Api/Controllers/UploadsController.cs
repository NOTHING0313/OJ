using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OnlineJudge.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/uploads")]
public class UploadsController(IWebHostEnvironment environment) : ControllerBase
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

        var webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(webRootPath, "uploads", "images");
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.GetFullPath(Path.Combine(uploadDirectory, fileName));
        var uploadRoot = Path.GetFullPath(uploadDirectory);

        if (!filePath.StartsWith(uploadRoot, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid file path.");
        }

        await using var stream = System.IO.File.Create(filePath);
        await file.CopyToAsync(stream, cancellationToken);

        var url = $"{Request.Scheme}://{Request.Host}/uploads/images/{fileName}";
        return Ok(new { url });
    }
}
