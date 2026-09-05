using System.Text;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Problems.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.Uploads;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Infrastructure.Problems;

public class ProblemJudgeAssetService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    IProblemJudgeAssetStorage storage,
    ISecureUploadValidator uploadValidator,
    JudgeResourcePolicy? resourcePolicy = null) : IProblemJudgeAssetService
{
    private JudgeResourcePolicy ResourcePolicy { get; } = resourcePolicy ?? JudgeResourcePolicy.Default;

    public ProblemJudgeAssetService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, IProblemJudgeAssetStorage storage)
        : this(dbContext, currentUser, storage, new SecureUploadValidator(new SecureUploadOptions()))
    {
    }

    internal const int MaxAssetsPerLanguage = 8;
    internal const int MaxFileSizeBytes = 512 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly IReadOnlyDictionary<JudgeLanguage, HashSet<string>> AllowedExtensions =
        new Dictionary<JudgeLanguage, HashSet<string>>
        {
            [JudgeLanguage.Cpp17] = new(StringComparer.OrdinalIgnoreCase) { ".cpp", ".cc", ".cxx", ".h", ".hpp" },
            [JudgeLanguage.C11] = new(StringComparer.OrdinalIgnoreCase) { ".c", ".h" },
            [JudgeLanguage.CSharp] = new(StringComparer.OrdinalIgnoreCase) { ".cs" }
        };
    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "main.cpp",
        "main.c",
        "Program.cs",
        "Main.csproj"
    };

    public async Task<Result<IReadOnlyList<ProblemJudgeAssetDto>>> GetAssetsAsync(Guid problemId, CancellationToken cancellationToken = default)
    {
        var access = await GetEditableProblemAsync(problemId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<IReadOnlyList<ProblemJudgeAssetDto>>.Failure(access.ErrorMessage!);
        }

        if (access.Value!.ProblemKind != ProblemKind.Programming)
        {
            return Result<IReadOnlyList<ProblemJudgeAssetDto>>.Failure("Choice problems do not use judge assets.");
        }

        var assets = await dbContext.ProblemJudgeAssets
            .AsNoTracking()
            .Where(asset => asset.ProblemId == problemId && !asset.IsDeleted)
            .OrderBy(asset => asset.Language)
            .ThenBy(asset => asset.OriginalFileName)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ProblemJudgeAssetDto>>.Success(assets.Select(ToDto).ToList());
    }

    public async Task<Result<ProblemJudgeAssetDto>> CreateAssetAsync(Guid problemId, CreateProblemJudgeAssetRequest request, CancellationToken cancellationToken = default)
    {
        var access = await GetEditableProblemAsync(problemId, cancellationToken);
        if (access.IsFailure)
        {
            return Result<ProblemJudgeAssetDto>.Failure(access.ErrorMessage!);
        }

        var allowedExtensions = AllowedExtensions.TryGetValue(request.Language, out var languageExtensions)
            ? languageExtensions
            : [];
        var secureValidation = await uploadValidator.ValidateAsync(new SecureUploadRequest
        {
            Policy = UploadPolicy.JudgeSource,
            OriginalFileName = request.OriginalFileName,
            DeclaredContentType = request.ContentType,
            DeclaredLength = request.FileSizeBytes,
            Content = request.Content,
            AllowedExtensions = allowedExtensions
        }, cancellationToken);
        if (!secureValidation.IsValid)
        {
            return Result<ProblemJudgeAssetDto>.Failure($"{secureValidation.ErrorCode}: {secureValidation.ErrorMessage}");
        }

        var fileNameValidation = ValidateFileName(request.Language, request.OriginalFileName);
        if (fileNameValidation.IsFailure)
        {
            return Result<ProblemJudgeAssetDto>.Failure(fileNameValidation.ErrorMessage!);
        }

        if (request.FileSizeBytes < 0 || request.FileSizeBytes > MaxFileSizeBytes)
        {
            return Result<ProblemJudgeAssetDto>.Failure("File size must be 512 KB or less.");
        }

        byte[] content;
        try
        {
            content = await ReadBoundedAsync(request.Content, MaxFileSizeBytes, cancellationToken);
            if (request.FileSizeBytes != content.LongLength)
            {
                return Result<ProblemJudgeAssetDto>.Failure("Uploaded file size does not match the request.");
            }

            var text = StrictUtf8.GetString(content);
            if (text.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            {
                return Result<ProblemJudgeAssetDto>.Failure("Judge asset must be UTF-8 text without binary control characters.");
            }
        }
        catch (DecoderFallbackException)
        {
            return Result<ProblemJudgeAssetDto>.Failure("Judge asset must be valid UTF-8 text.");
        }
        catch (InvalidDataException ex)
        {
            return Result<ProblemJudgeAssetDto>.Failure(ex.Message);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);
        if (problem is null)
        {
            return Result<ProblemJudgeAssetDto>.Failure("Problem not found.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result<ProblemJudgeAssetDto>.Failure("Choice problems do not use judge assets.");
        }

        var normalizedFileName = request.OriginalFileName.ToUpperInvariant();
        var existingAssets = await dbContext.ProblemJudgeAssets
            .Where(asset => asset.ProblemId == problemId && asset.Language == request.Language && !asset.IsDeleted)
            .ToListAsync(cancellationToken);

        if (existingAssets.Count >= MaxAssetsPerLanguage)
        {
            return Result<ProblemJudgeAssetDto>.Failure("A problem can have at most 8 judge assets per language.");
        }

        if (existingAssets.Any(asset => asset.NormalizedFileName == normalizedFileName))
        {
            return Result<ProblemJudgeAssetDto>.Failure("A judge asset with the same file name already exists for this language.");
        }

        var extension = Path.GetExtension(request.OriginalFileName).ToLowerInvariant();
        StoredJudgeAssetFile storedFile;
        try
        {
            storedFile = await storage.WriteAsync(problemId, request.Language, extension, content, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Result<ProblemJudgeAssetDto>.Failure("Judge asset could not be stored.");
        }

        var now = DateTimeOffset.UtcNow;
        var asset = new ProblemJudgeAsset
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Language = request.Language,
            OriginalFileName = request.OriginalFileName,
            NormalizedFileName = normalizedFileName,
            StoredFileName = storedFile.StoredFileName,
            StorageRelativePath = storedFile.StorageRelativePath,
            Sha256 = storedFile.Sha256,
            FileSizeBytes = storedFile.FileSizeBytes,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ProblemJudgeAssets.Add(asset);
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;
        try
        {
            if (problem.IsPublished)
            {
                var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
                if (revisionResult.IsFailure)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    await storage.DeleteIfExistsAsync(storedFile.StorageRelativePath, CancellationToken.None);
                    return Result<ProblemJudgeAssetDto>.Failure(revisionResult.ErrorMessage!);
                }
            }
            else
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            await storage.DeleteIfExistsAsync(storedFile.StorageRelativePath, CancellationToken.None);
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            await storage.DeleteIfExistsAsync(storedFile.StorageRelativePath, CancellationToken.None);
            return Result<ProblemJudgeAssetDto>.Failure("Judge asset metadata could not be saved.");
        }

        return Result<ProblemJudgeAssetDto>.Success(ToDto(asset));
    }

    public async Task<Result> DeleteAssetAsync(Guid problemId, Guid assetId, CancellationToken cancellationToken = default)
    {
        var access = await GetEditableProblemAsync(problemId, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure(access.ErrorMessage!);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);
        if (problem is null)
        {
            return Result.Failure("Problem not found.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result.Failure("Choice problems do not use judge assets.");
        }

        var asset = await dbContext.ProblemJudgeAssets
            .FirstOrDefaultAsync(asset => asset.Id == assetId && asset.ProblemId == problemId && !asset.IsDeleted, cancellationToken);
        if (asset is null)
        {
            return Result.Failure("Judge asset not found.");
        }

        var now = DateTimeOffset.UtcNow;
        asset.IsDeleted = true;
        asset.DeletedAt = now;
        asset.UpdatedAt = now;
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;
        try
        {
            if (problem.IsPublished)
            {
                var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
                if (revisionResult.IsFailure)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return Result.Failure(revisionResult.ErrorMessage!);
                }
            }
            else
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return Result.Failure("Judge asset metadata could not be deleted.");
        }

        return Result.Success();
    }

    internal static Result ValidateFileName(JudgeLanguage language, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.Length > 255
            || Path.IsPathRooted(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.Any(char.IsControl)
            || !HasSafeFileNameCharacters(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            return Result.Failure("Judge asset file name is invalid.");
        }

        if (ReservedFileNames.Contains(fileName))
        {
            return Result.Failure("Judge asset file name is reserved by the judge platform.");
        }

        if (!AllowedExtensions.TryGetValue(language, out var extensions)
            || !extensions.Contains(Path.GetExtension(fileName)))
        {
            return Result.Failure("Judge asset extension is not supported for the selected language.");
        }

        return Result.Success();
    }

    private static bool HasSafeFileNameCharacters(string fileName)
    {
        if (!char.IsLetterOrDigit(fileName[0]) && fileName[0] != '_')
        {
            return false;
        }

        return fileName.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.' or ' ');
    }

    private async Task<Result<Problem>> GetEditableProblemAsync(Guid problemId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<Problem>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<Problem>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<Problem>.Failure("Account is blacklisted.");
        }

        var problem = await dbContext.Problems.AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);
        if (problem is null)
        {
            return Result<Problem>.Failure("Problem not found.");
        }

        if (user.Role == UserRole.Root)
        {
            return Result<Problem>.Success(problem);
        }

        if (user.Role != UserRole.ProblemSetter)
        {
            return Result<Problem>.Failure("Forbidden.");
        }

        if (problem.CreatedByUserId == user.Id)
        {
            return Result<Problem>.Success(problem);
        }

        var canEdit = await dbContext.ProblemCollaborators.AsNoTracking()
            .AnyAsync(collaborator => collaborator.ProblemId == problemId
                && collaborator.UserId == user.Id
                && collaborator.CanEditProblem,
                cancellationToken);

        return canEdit ? Result<Problem>.Success(problem) : Result<Problem>.Failure("Forbidden.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return output.ToArray();
            }

            if (output.Length + read > maxBytes)
            {
                throw new InvalidDataException("File size must be 512 KB or less.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static ProblemJudgeAssetDto ToDto(ProblemJudgeAsset asset)
    {
        return new ProblemJudgeAssetDto
        {
            Id = asset.Id,
            Language = asset.Language,
            OriginalFileName = asset.OriginalFileName,
            FileSizeBytes = asset.FileSizeBytes,
            Sha256 = asset.Sha256,
            CreatedAt = asset.CreatedAt
        };
    }
}
