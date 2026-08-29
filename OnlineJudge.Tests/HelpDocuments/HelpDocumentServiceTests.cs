using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.HelpDocuments.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.HelpDocuments;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.HelpDocuments;

public class HelpDocumentServiceTests
{
    [Fact]
    public async Task Answerer_SeesOnlyPublishedDocumentsInStableOrder()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Answerer);
        AddDocument(dbContext, "later", true, 10, updatedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        AddDocument(dbContext, "newer", true, 5, updatedAt: DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        AddDocument(dbContext, "older", true, 5, updatedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        AddDocument(dbContext, "draft", false, 0);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, user);

        var result = await service.GetPublishedAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["newer", "older", "later"], result.Value!.Select(item => item.Slug));
        Assert.DoesNotContain(result.Value!, item => item.Slug == "draft");
    }

    [Fact]
    public async Task Answerer_CanFetchPublishedDocumentBySlug()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Answerer);
        AddDocument(dbContext, "quick-start", true, 0, "# 快速开始");
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, user).GetPublishedBySlugAsync("quick-start");

        Assert.True(result.IsSuccess);
        Assert.Equal("# 快速开始", result.Value!.MarkdownContent);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("missing")]
    public async Task Answerer_CannotFetchDraftOrUnknownSlug(string slug)
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Answerer);
        AddDocument(dbContext, "draft", false, 0);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, user).GetPublishedBySlugAsync(slug);

        Assert.True(result.IsFailure);
        Assert.Equal("Help document not found.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("publish")]
    public async Task Answerer_CannotMutateDocuments(string operation)
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Answerer);
        var document = AddDocument(dbContext, "existing", false, 0);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, user);

        var error = operation switch
        {
            "create" => (await service.CreateAsync(Request("created"))).ErrorMessage,
            "update" => (await service.UpdateAsync(document.Id, Request("updated"))).ErrorMessage,
            "delete" => (await service.DeleteAsync(document.Id)).ErrorMessage,
            _ => (await service.PublishAsync(document.Id)).ErrorMessage
        };

        Assert.Equal("Forbidden.", error);
    }

    [Fact]
    public async Task ProblemSetter_CanCreateEditPublishUnpublishAndDelete()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, user);

        var created = await service.CreateAsync(Request("quick-start"));
        Assert.True(created.IsSuccess);
        Assert.False(created.Value!.IsPublished);

        var updated = await service.UpdateAsync(created.Value.Id, Request("getting-started", "Updated"));
        Assert.True(updated.IsSuccess);
        Assert.Equal("Updated", updated.Value!.Title);

        Assert.True((await service.PublishAsync(created.Value.Id)).Value!.IsPublished);
        Assert.False((await service.UnpublishAsync(created.Value.Id)).Value!.IsPublished);
        Assert.True((await service.DeleteAsync(created.Value.Id)).IsSuccess);
        Assert.Empty(dbContext.HelpDocuments);
    }

    [Fact]
    public async Task Root_HasAllManagementPermissions()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Root);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, user);

        var created = await service.CreateAsync(Request("root-guide"));
        Assert.True(created.IsSuccess);
        Assert.True((await service.GetAllAsync()).IsSuccess);
        Assert.True((await service.GetByIdAsync(created.Value!.Id)).IsSuccess);
        Assert.True((await service.UpdateAsync(created.Value.Id, Request("root-guide", "Root Guide"))).IsSuccess);
        Assert.True((await service.PublishAsync(created.Value.Id)).IsSuccess);
        Assert.True((await service.UnpublishAsync(created.Value.Id)).IsSuccess);
        Assert.True((await service.DeleteAsync(created.Value.Id)).IsSuccess);
    }

    [Fact]
    public async Task DatabaseRole_IsAuthoritativeForManagement()
    {
        await using var dbContext = CreateDbContext();
        var promotedUser = AddUser(dbContext, UserRole.ProblemSetter);
        var demotedUser = AddUser(dbContext, UserRole.Answerer);
        await dbContext.SaveChangesAsync();

        var promotedResult = await CreateService(dbContext, promotedUser, UserRole.Answerer).CreateAsync(Request("promoted"));
        var demotedResult = await CreateService(dbContext, demotedUser, UserRole.ProblemSetter).CreateAsync(Request("demoted"));

        Assert.True(promotedResult.IsSuccess);
        Assert.Equal("Forbidden.", demotedResult.ErrorMessage);
    }

    [Theory]
    [InlineData("Invalid-Slug")]
    [InlineData("bad_slug")]
    [InlineData("bad slug")]
    [InlineData("-bad")]
    public async Task InvalidSlug_IsRejected(string slug)
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, user).CreateAsync(Request(slug));

        Assert.Equal("Slug must contain only lowercase letters, numbers, and hyphens.", result.ErrorMessage);
    }

    [Fact]
    public async Task DuplicateSlug_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        AddDocument(dbContext, "duplicate", false, 0);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, user).CreateAsync(Request("duplicate"));

        Assert.Equal("Slug already exists.", result.ErrorMessage);
    }

    [Fact]
    public async Task EmptyTitle_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        await dbContext.SaveChangesAsync();
        var request = Request("empty-title");
        request.Title = " ";

        var result = await CreateService(dbContext, user).CreateAsync(request);

        Assert.Equal("Title must be between 1 and 120 characters.", result.ErrorMessage);
    }

    [Fact]
    public async Task EmptyMarkdown_IsAllowedForDraftButRejectedOnPublish()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        await dbContext.SaveChangesAsync();
        var request = Request("empty-content");
        request.MarkdownContent = "";
        var service = CreateService(dbContext, user);

        var created = await service.CreateAsync(request);
        var published = await service.PublishAsync(created.Value!.Id);

        Assert.True(created.IsSuccess);
        Assert.Equal("Markdown content is required before publishing.", published.ErrorMessage);
    }

    [Fact]
    public async Task PublishedDocument_CannotBeUpdatedToEmptyMarkdown()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.ProblemSetter);
        var document = AddDocument(dbContext, "published", true, 0);
        await dbContext.SaveChangesAsync();
        var request = Request("published");
        request.MarkdownContent = "";

        var result = await CreateService(dbContext, user).UpdateAsync(document.Id, request);

        Assert.Equal("Markdown content is required before publishing.", result.ErrorMessage);
        Assert.Equal("# Guide", document.MarkdownContent);
    }

    [Fact]
    public async Task TooLongMarkdown_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Root);
        await dbContext.SaveChangesAsync();
        var request = Request("too-long");
        request.MarkdownContent = new string('x', 200_001);

        var result = await CreateService(dbContext, user).CreateAsync(request);

        Assert.Equal("Markdown content cannot exceed 200000 characters.", result.ErrorMessage);
    }

    [Fact]
    public async Task UnpublishAndDelete_ImmediatelyRemovePublicAccess()
    {
        await using var dbContext = CreateDbContext();
        var manager = AddUser(dbContext, UserRole.ProblemSetter);
        var answerer = AddUser(dbContext, UserRole.Answerer);
        var document = AddDocument(dbContext, "lifecycle", true, 0);
        await dbContext.SaveChangesAsync();

        Assert.True((await CreateService(dbContext, answerer).GetPublishedBySlugAsync("lifecycle")).IsSuccess);
        Assert.True((await CreateService(dbContext, manager).UnpublishAsync(document.Id)).IsSuccess);
        Assert.True((await CreateService(dbContext, answerer).GetPublishedBySlugAsync("lifecycle")).IsFailure);
        Assert.True((await CreateService(dbContext, manager).DeleteAsync(document.Id)).IsSuccess);
        Assert.True((await CreateService(dbContext, answerer).GetPublishedBySlugAsync("lifecycle")).IsFailure);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task DeletedOrBlacklistedUser_CannotRead(bool isDeleted, bool isBlacklisted)
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext, UserRole.Answerer, isDeleted, isBlacklisted);
        AddDocument(dbContext, "guide", true, 0);
        await dbContext.SaveChangesAsync();

        var result = await CreateService(dbContext, user).GetPublishedAsync();

        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    private static HelpDocumentService CreateService(OnlineJudgeDbContext dbContext, User user, UserRole? jwtRole = null) =>
        new(dbContext, new TestCurrentUser(user.Id, jwtRole ?? user.Role));

    private static UpsertHelpDocumentRequest Request(string slug, string title = "Guide") => new()
    {
        Title = title,
        Slug = slug,
        Summary = "Summary",
        MarkdownContent = "# Guide\n\n| A | B |\n|---|---|\n| 1 | 2 |",
        SortOrder = 10
    };

    private static User AddUser(OnlineJudgeDbContext dbContext, UserRole role, bool isDeleted = false, bool isBlacklisted = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = $"user-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            PasswordHash = "hash",
            Role = role,
            IsDeleted = isDeleted,
            IsBlacklisted = isBlacklisted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Users.Add(user);
        return user;
    }

    private static HelpDocument AddDocument(OnlineJudgeDbContext dbContext, string slug, bool isPublished, int sortOrder, string content = "# Guide", DateTimeOffset? updatedAt = null)
    {
        var authorId = dbContext.Users.Local.FirstOrDefault()?.Id ?? Guid.NewGuid();
        var document = new HelpDocument
        {
            Id = Guid.NewGuid(),
            Title = slug,
            Slug = slug,
            MarkdownContent = content,
            IsPublished = isPublished,
            SortOrder = sortOrder,
            CreatedByUserId = authorId,
            UpdatedByUserId = authorId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };
        dbContext.HelpDocuments.Add(document);
        return document;
    }

    private static OnlineJudgeDbContext CreateDbContext() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "test-user";
        public UserRole? Role => role;
    }
}
