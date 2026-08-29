using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.HelpDocuments.Dtos;
using OnlineJudge.Application.HelpDocuments.Requests;
using OnlineJudge.Application.HelpDocuments.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.HelpDocuments;

public sealed partial class HelpDocumentService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser) : IHelpDocumentService
{
    private const int MaxMarkdownLength = 200_000;
    private const int MinSortOrder = -100_000;
    private const int MaxSortOrder = 100_000;

    public async Task<Result<IReadOnlyList<HelpDocumentListItemDto>>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireActiveUserAsync(cancellationToken);
        if (userResult.IsFailure) return Result<IReadOnlyList<HelpDocumentListItemDto>>.Failure(userResult.ErrorMessage!);

        var documents = await dbContext.HelpDocuments.AsNoTracking()
            .Where(document => document.IsPublished)
            .OrderBy(document => document.SortOrder)
            .ThenByDescending(document => document.UpdatedAt)
            .Select(document => new HelpDocumentListItemDto
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
                Summary = document.Summary,
                IsPublished = document.IsPublished,
                SortOrder = document.SortOrder,
                UpdatedAt = document.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<HelpDocumentListItemDto>>.Success(documents);
    }

    public async Task<Result<HelpDocumentDto>> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireActiveUserAsync(cancellationToken);
        if (userResult.IsFailure) return Result<HelpDocumentDto>.Failure(userResult.ErrorMessage!);

        var normalizedSlug = slug.Trim();
        var document = await dbContext.HelpDocuments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Slug == normalizedSlug && item.IsPublished, cancellationToken);
        return document is null
            ? Result<HelpDocumentDto>.Failure("Help document not found.")
            : Result<HelpDocumentDto>.Success(ToDto(document));
    }

    public async Task<Result<IReadOnlyList<HelpDocumentListItemDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result<IReadOnlyList<HelpDocumentListItemDto>>.Failure(userResult.ErrorMessage!);

        var documents = await dbContext.HelpDocuments.AsNoTracking()
            .OrderBy(document => document.SortOrder)
            .ThenBy(document => document.Title)
            .ThenByDescending(document => document.UpdatedAt)
            .Select(document => new HelpDocumentListItemDto
            {
                Id = document.Id,
                Title = document.Title,
                Slug = document.Slug,
                Summary = document.Summary,
                IsPublished = document.IsPublished,
                SortOrder = document.SortOrder,
                UpdatedAt = document.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<HelpDocumentListItemDto>>.Success(documents);
    }

    public async Task<Result<HelpDocumentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result<HelpDocumentDto>.Failure(userResult.ErrorMessage!);

        var document = await dbContext.HelpDocuments.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return document is null
            ? Result<HelpDocumentDto>.Failure("Help document not found.")
            : Result<HelpDocumentDto>.Success(ToDto(document));
    }

    public async Task<Result<HelpDocumentDto>> CreateAsync(UpsertHelpDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result<HelpDocumentDto>.Failure(userResult.ErrorMessage!);
        var validationError = Validate(request, requireContent: false);
        if (validationError is not null) return Result<HelpDocumentDto>.Failure(validationError);

        var slug = request.Slug.Trim();
        if (await dbContext.HelpDocuments.AnyAsync(document => document.Slug == slug, cancellationToken))
        {
            return Result<HelpDocumentDto>.Failure("Slug already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var document = new HelpDocument
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Slug = slug,
            Summary = NormalizeSummary(request.Summary),
            MarkdownContent = request.MarkdownContent,
            IsPublished = false,
            SortOrder = request.SortOrder,
            CreatedByUserId = userResult.Value!.Id,
            UpdatedByUserId = userResult.Value.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.HelpDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<HelpDocumentDto>.Success(ToDto(document));
    }

    public async Task<Result<HelpDocumentDto>> UpdateAsync(Guid id, UpsertHelpDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result<HelpDocumentDto>.Failure(userResult.ErrorMessage!);
        var document = await dbContext.HelpDocuments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return Result<HelpDocumentDto>.Failure("Help document not found.");
        var validationError = Validate(request, requireContent: document.IsPublished);
        if (validationError is not null) return Result<HelpDocumentDto>.Failure(validationError);
        var slug = request.Slug.Trim();
        if (await dbContext.HelpDocuments.AnyAsync(item => item.Id != id && item.Slug == slug, cancellationToken))
        {
            return Result<HelpDocumentDto>.Failure("Slug already exists.");
        }

        document.Title = request.Title.Trim();
        document.Slug = slug;
        document.Summary = NormalizeSummary(request.Summary);
        document.MarkdownContent = request.MarkdownContent;
        document.SortOrder = request.SortOrder;
        document.UpdatedByUserId = userResult.Value!.Id;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<HelpDocumentDto>.Success(ToDto(document));
    }

    public Task<Result<HelpDocumentDto>> PublishAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetPublishedAsync(id, true, cancellationToken);

    public Task<Result<HelpDocumentDto>> UnpublishAsync(Guid id, CancellationToken cancellationToken = default) =>
        SetPublishedAsync(id, false, cancellationToken);

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result.Failure(userResult.ErrorMessage!);
        var document = await dbContext.HelpDocuments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return Result.Failure("Help document not found.");
        dbContext.HelpDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result<HelpDocumentDto>> SetPublishedAsync(Guid id, bool isPublished, CancellationToken cancellationToken)
    {
        var userResult = await RequireManagerAsync(cancellationToken);
        if (userResult.IsFailure) return Result<HelpDocumentDto>.Failure(userResult.ErrorMessage!);
        var document = await dbContext.HelpDocuments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return Result<HelpDocumentDto>.Failure("Help document not found.");
        if (isPublished)
        {
            var validationError = ValidateDocumentForPublish(document);
            if (validationError is not null) return Result<HelpDocumentDto>.Failure(validationError);
        }
        document.IsPublished = isPublished;
        document.UpdatedByUserId = userResult.Value!.Id;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<HelpDocumentDto>.Success(ToDto(document));
    }

    private async Task<Result<User>> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }
        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted && !item.IsBlacklisted, cancellationToken);
        return user is null ? Result<User>.Failure("Forbidden.") : Result<User>.Success(user);
    }

    private async Task<Result<User>> RequireManagerAsync(CancellationToken cancellationToken)
    {
        var result = await RequireActiveUserAsync(cancellationToken);
        return result.IsSuccess && result.Value!.Role is (UserRole.ProblemSetter or UserRole.Root)
            ? result
            : Result<User>.Failure(result.ErrorMessage == "Unauthorized." ? "Unauthorized." : "Forbidden.");
    }

    private static string? Validate(UpsertHelpDocumentRequest request, bool requireContent)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 120) return "Title must be between 1 and 120 characters.";
        var slug = request.Slug.Trim();
        if (slug.Length is < 1 or > 120 || !SlugPattern().IsMatch(slug)) return "Slug must contain only lowercase letters, numbers, and hyphens.";
        if (request.Summary?.Trim().Length > 300) return "Summary cannot exceed 300 characters.";
        if (request.MarkdownContent.Length > MaxMarkdownLength) return $"Markdown content cannot exceed {MaxMarkdownLength} characters.";
        if (requireContent && string.IsNullOrWhiteSpace(request.MarkdownContent)) return "Markdown content is required before publishing.";
        if (request.SortOrder is < MinSortOrder or > MaxSortOrder) return $"Sort order must be between {MinSortOrder} and {MaxSortOrder}.";
        return null;
    }

    private static string? ValidateDocumentForPublish(HelpDocument document) => Validate(new UpsertHelpDocumentRequest
    {
        Title = document.Title,
        Slug = document.Slug,
        Summary = document.Summary,
        MarkdownContent = document.MarkdownContent,
        SortOrder = document.SortOrder
    }, requireContent: true);

    private static string? NormalizeSummary(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

    private static HelpDocumentDto ToDto(HelpDocument document) => new()
    {
        Id = document.Id,
        Title = document.Title,
        Slug = document.Slug,
        Summary = document.Summary,
        MarkdownContent = document.MarkdownContent,
        IsPublished = document.IsPublished,
        SortOrder = document.SortOrder,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt
    };

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
