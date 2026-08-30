using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Api.RateLimiting;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Tests.Security;

public sealed class SecureUploadFoundationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "onlinejudge-secure-upload-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [MemberData(nameof(ValidImages))]
    public async Task ImagePolicy_AcceptsOnlyMatchingPngJpegAndWebP(string fileName, string contentType, byte[] content, string canonicalExtension)
    {
        var result = await ValidateAsync(UploadPolicy.Image, fileName, contentType, content);

        Assert.True(result.IsValid);
        Assert.Equal(canonicalExtension, result.CanonicalExtension);
    }

    [Fact]
    public async Task ImagePolicy_RejectsForgedExtensionMimeMismatchSvgAndOversize()
    {
        var forged = await ValidateAsync(UploadPolicy.Image, "evil.png", "image/png", Encoding.UTF8.GetBytes("MZ executable"));
        var mismatch = await ValidateAsync(UploadPolicy.Image, "photo.png", "image/png", Jpeg());
        var svg = await ValidateAsync(UploadPolicy.Image, "active.svg", "image/svg+xml", Encoding.UTF8.GetBytes("<svg><script/></svg>"));
        var oversizedOptions = new SecureUploadOptions { ImageMaxBytes = 4 };
        var oversized = await ValidateAsync(UploadPolicy.Image, "photo.png", "image/png", Png(), oversizedOptions);

        Assert.Equal(SecureUploadErrorCodes.InvalidType, forged.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.TypeMismatch, mismatch.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.InvalidType, svg.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.TooLarge, oversized.ErrorCode);
    }

    [Theory]
    [InlineData("../avatar.png")]
    [InlineData("..\\avatar.png")]
    [InlineData("C:\\avatar.png")]
    [InlineData("\\\\server\\share.png")]
    [InlineData("..%2favatar.png")]
    [InlineData("%252e%252e%255cavatar.png")]
    public async Task FileNameValidation_RejectsTraversalAbsoluteAndEncodedPaths(string fileName)
    {
        var result = await ValidateAsync(UploadPolicy.Image, fileName, "image/png", Png());

        Assert.Equal(SecureUploadErrorCodes.InvalidFileName, result.ErrorCode);
    }

    [Fact]
    public async Task ThemeImage_UsesSameGenericImagePolicyWithoutChangingUi()
    {
        var result = await ValidateAsync(UploadPolicy.ThemeImage, "background.webp", "image/webp", WebP());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ArchivePolicy_AcceptsSafeArchive()
    {
        var archive = Archive(("src/main.cpp", RandomBytes(2048), null));

        var result = await ValidateAsync(UploadPolicy.ChallengeArchive, "answer.zip", "application/zip", archive);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/absolute.txt")]
    [InlineData("%2e%2e%2fescape.txt")]
    public async Task ArchivePolicy_RejectsZipSlipAndAbsoluteEntries(string entryName)
    {
        var result = await ValidateAsync(
            UploadPolicy.ChallengeArchive,
            "answer.zip",
            "application/zip",
            Archive((entryName, Encoding.UTF8.GetBytes("unsafe"), null)));

        Assert.Equal(SecureUploadErrorCodes.ArchiveUnsafe, result.ErrorCode);
    }

    [Fact]
    public async Task ArchivePolicy_RejectsSymbolicLink()
    {
        var result = await ValidateAsync(
            UploadPolicy.ChallengeArchive,
            "answer.zip",
            "application/zip",
            Archive(("link", Encoding.UTF8.GetBytes("target"), 0xa000 << 16)));

        Assert.Equal(SecureUploadErrorCodes.ArchiveUnsafe, result.ErrorCode);
    }

    [Fact]
    public async Task ArchivePolicy_EnforcesEntryCountSingleTotalAndCompressionRatio()
    {
        var tooManyOptions = new SecureUploadOptions { ArchiveMaxEntryCount = 1 };
        var tooMany = await ValidateAsync(UploadPolicy.ChallengeArchive, "a.zip", "application/zip",
            Archive(("a.txt", RandomBytes(128), null), ("b.txt", RandomBytes(128), null)), tooManyOptions);

        var singleOptions = new SecureUploadOptions { ArchiveMaxSingleEntryBytes = 4 };
        var single = await ValidateAsync(UploadPolicy.ChallengeArchive, "a.zip", "application/zip",
            Archive(("a.txt", RandomBytes(32), null)), singleOptions);

        var totalOptions = new SecureUploadOptions { ArchiveMaxExpandedBytes = 16, ArchiveMaxSingleEntryBytes = 16 };
        var total = await ValidateAsync(UploadPolicy.ChallengeArchive, "a.zip", "application/zip",
            Archive(("a.txt", RandomBytes(12), null), ("b.txt", RandomBytes(12), null)), totalOptions);

        var ratioOptions = new SecureUploadOptions { ArchiveMaxCompressionRatio = 2 };
        var ratio = await ValidateAsync(UploadPolicy.ChallengeArchive, "a.zip", "application/zip",
            Archive(("zeros.bin", new byte[4096], null)), ratioOptions);

        Assert.Equal(SecureUploadErrorCodes.ArchiveTooComplex, tooMany.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.ArchiveTooLarge, single.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.ArchiveTooLarge, total.ErrorCode);
        Assert.Equal(SecureUploadErrorCodes.ArchiveTooLarge, ratio.ErrorCode);
    }

    [Fact]
    public async Task AtomicStorage_UsesGeneratedFinalNameAndCleansFailedPartialWrite()
    {
        var paths = new RuntimeStoragePathProvider(root, Path.Combine(root, "images"), Path.Combine(root, "challenge"));
        var finalName = $"{Guid.NewGuid():N}.png";
        await using var valid = new MemoryStream(Png());
        await paths.WriteUploadImageAsync(finalName, valid, 1024);

        await using var oversized = new MemoryStream(new byte[32]);
        await Assert.ThrowsAsync<InvalidDataException>(() => paths.WriteUploadImageAsync($"{Guid.NewGuid():N}.png", oversized, 4));

        Assert.Equal(finalName, Path.GetFileName(Assert.Single(Directory.GetFiles(paths.UploadImagesRoot))));
        Assert.DoesNotContain(Directory.GetFiles(paths.UploadImagesRoot), path => path.EndsWith(".uploading", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UploadController_RejectsInvalidImageBeforeFinalStorage()
    {
        var paths = new RuntimeStoragePathProvider(root, Path.Combine(root, "images"), Path.Combine(root, "challenge"));
        var options = new SecureUploadOptions();
        var controller = new UploadsController(paths, new SecureUploadValidator(options), options);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("not an image"));
        var file = new FormFile(content, 0, content.Length, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var response = await controller.UploadImage(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response);
        Assert.False(Directory.Exists(paths.UploadImagesRoot) && Directory.EnumerateFiles(paths.UploadImagesRoot).Any());
    }

    [Fact]
    public void UploadEndpoints_RetainSecurity10BRateLimitBeforeStorageActions()
    {
        var methods = new[]
        {
            typeof(UploadsController).GetMethod(nameof(UploadsController.UploadImage))!,
            typeof(ChallengesController).GetMethod(nameof(ChallengesController.SubmitFileAnswer))!,
            typeof(ProblemsController).GetMethod(nameof(ProblemsController.CreateJudgeAsset))!
        };

        Assert.All(methods, method => Assert.Contains(method.GetCustomAttributes(typeof(RiskRateLimitAttribute), true)
            .Cast<RiskRateLimitAttribute>(), attribute => attribute.PolicyName == RateLimitPolicies.Upload));
    }

    public static IEnumerable<object[]> ValidImages()
    {
        yield return ["image.png", "image/png", Png(), ".png"];
        yield return ["image.jpeg", "image/jpeg", Jpeg(), ".jpg"];
        yield return ["image.webp", "image/webp", WebP(), ".webp"];
    }

    private static Task<SecureUploadValidationResult> ValidateAsync(
        UploadPolicy policy,
        string fileName,
        string contentType,
        byte[] content,
        SecureUploadOptions? options = null)
    {
        var stream = new MemoryStream(content);
        return new SecureUploadValidator(options ?? new SecureUploadOptions()).ValidateAsync(new SecureUploadRequest
        {
            Policy = policy,
            OriginalFileName = fileName,
            DeclaredContentType = contentType,
            DeclaredLength = content.LongLength,
            Content = stream
        });
    }

    private static byte[] Png() => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0];

    private static byte[] Jpeg() => [0xff, 0xd8, 0xff, 0xe0, 0, 0, 0, 0, 0, 0, 0xff, 0xd9];

    private static byte[] WebP() => [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray()];

    private static byte[] RandomBytes(int size)
    {
        var content = new byte[size];
        new Random(17).NextBytes(content);
        return content;
    }

    private static byte[] Archive(params (string Name, byte[] Content, int? ExternalAttributes)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Name, CompressionLevel.SmallestSize);
                if (item.ExternalAttributes.HasValue) entry.ExternalAttributes = item.ExternalAttributes.Value;
                using var output = entry.Open();
                output.Write(item.Content);
            }
        }

        return stream.ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
