using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Api.Controllers;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Storage;
using OnlineJudge.Infrastructure.Uploads;

namespace OnlineJudge.Tests.Storage;

public sealed class RuntimeStorageTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-29T00:00:00Z");
    private readonly string root = Path.Combine(Path.GetTempPath(), "onlinejudge-storage-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CustomAndDefaultRoots_AreResolvedWithoutExposingPhysicalPaths()
    {
        var customImages = Path.Combine(root, "persistent", "images");
        var customChallenge = Path.Combine(root, "persistent", "challenge");
        var custom = new RuntimeStoragePathProvider(root, customImages, customChallenge);
        var defaults = new RuntimeStoragePathProvider(root);

        Assert.Equal(Path.GetFullPath(customImages), custom.UploadImagesRoot);
        Assert.Equal(Path.GetFullPath(customChallenge), custom.ChallengeFilesRoot);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "wwwroot", "uploads", "images"), defaults.UploadImagesRoot);
        Assert.Equal(Path.Combine(Path.GetFullPath(root), "App_Data", "challenge-file-submissions"), defaults.ChallengeFilesRoot);
    }

    [Fact]
    public void StoredFileNames_CannotEscapeConfiguredRoots()
    {
        var paths = new RuntimeStoragePathProvider(root, Path.Combine(root, "images"), Path.Combine(root, "challenge"));

        Assert.Throws<InvalidDataException>(() => paths.ResolveUploadImagePath("../avatar.png"));
        Assert.Throws<InvalidDataException>(() => paths.ResolveChallengeFilePath("..\\answer.zip"));
        Assert.Throws<InvalidDataException>(() => paths.ResolveChallengeFilePath(Path.Combine(root, "absolute.zip")));
        Assert.StartsWith(paths.ChallengeFilesRoot, paths.ResolveChallengeFilePath("answer.zip"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadController_UsesCustomRootCreatesDirectoryAndKeepsUrlContract()
    {
        var imageRoot = Path.Combine(root, "external", "images");
        var paths = new RuntimeStoragePathProvider(root, imageRoot, Path.Combine(root, "challenge"));
        var options = new SecureUploadOptions();
        var controller = new UploadsController(paths, new SecureUploadValidator(options), options);
        await using var input = new MemoryStream([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0]);
        var file = new FormFile(input, 0, input.Length, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await controller.UploadImage(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("/uploads/images/", JsonSerializer.Serialize(ok.Value), StringComparison.Ordinal);
        Assert.True(Directory.Exists(imageRoot));
        Assert.Single(Directory.GetFiles(imageRoot));
    }

    [Fact]
    public async Task ConfiguredRootFailure_DoesNotFallbackToDevelopmentDirectory()
    {
        Directory.CreateDirectory(root);
        var blockedRoot = Path.Combine(root, "not-a-directory");
        File.WriteAllText(blockedRoot, "blocked");
        var paths = new RuntimeStoragePathProvider(root, blockedRoot, Path.Combine(root, "challenge"));

        await using var content = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAnyAsync<IOException>(() => paths.WriteUploadImageAsync("avatar.png", content, 1024));
        Assert.False(File.Exists(Path.Combine(root, "wwwroot", "uploads", "images", "avatar.png")));
    }

    [Fact]
    public async Task ChallengeWriteAndDownload_UseConfiguredRootAndPreserveRbac()
    {
        var challengeRoot = Path.Combine(root, "external", "challenge-files");
        var paths = new RuntimeStoragePathProvider(root, Path.Combine(root, "images"), challengeRoot);
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes("zip"));
        await paths.WriteChallengeFileAsync("answer.zip", input, 1024);

        await using var db = CreateDb();
        var setter = User("setter", UserRole.ProblemSetter);
        var owner = User("owner", UserRole.Answerer);
        var other = User("other", UserRole.Answerer);
        var challenge = Challenge(setter.Id);
        var task = FileTask(challenge.Id);
        var submission = new ChallengeTaskFileSubmission
        {
            Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = owner.Id,
            OriginalFileName = "answer.zip", StoredFileName = "answer.zip", FilePath = "obsolete-release-path.zip",
            FileSizeBytes = 3, ContentType = "application/zip", CreatedAt = Now, UpdatedAt = Now,
            Challenge = challenge, ChallengeTask = task, User = owner
        };
        db.AddRange(setter, owner, other, challenge, task, submission);
        await db.SaveChangesAsync();

        var ownerResult = await Service(db, owner, paths).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);
        var setterResult = await Service(db, setter, paths).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);
        var forbidden = await Service(db, other, paths).GetFileSubmissionDownloadAsync(challenge.Id, submission.Id);

        Assert.Equal(paths.ResolveChallengeFilePath("answer.zip"), ownerResult.Value!.FilePath);
        Assert.Equal(paths.ResolveChallengeFilePath("answer.zip"), setterResult.Value!.FilePath);
        Assert.Equal("Forbidden.", forbidden.ErrorMessage);
    }

    [Fact]
    public async Task FileReviewAndResubmissionCandidate_NeverDowngradeCompletionBestScore()
    {
        var paths = new RuntimeStoragePathProvider(root, Path.Combine(root, "images"), Path.Combine(root, "challenge"));
        await using var db = CreateDb();
        var setter = User("setter", UserRole.ProblemSetter);
        var owner = User("owner", UserRole.Answerer);
        var challenge = Challenge(setter.Id);
        var task = FileTask(challenge.Id);
        var submission = new ChallengeTaskFileSubmission
        {
            Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTaskId = task.Id, UserId = owner.Id,
            OriginalFileName = "answer.zip", StoredFileName = "answer.zip", FilePath = paths.ResolveChallengeFilePath("answer.zip"),
            FileSizeBytes = 3, ContentType = "application/zip", CreatedAt = Now, UpdatedAt = Now,
            Challenge = challenge, ChallengeTask = task, User = owner
        };
        db.AddRange(setter, owner, challenge, task, submission);
        await db.SaveChangesAsync();
        var service = Service(db, setter, paths);

        Assert.True((await service.ReviewFileSubmissionAsync(challenge.Id, submission.Id, new ReviewChallengeFileSubmissionRequest { Score = 70 })).IsSuccess);
        var completion = await db.ChallengeTaskCompletions.SingleAsync();
        var completedAt = completion.CompletedAt;
        var updatedAt = completion.UpdatedAt;
        Assert.True((await service.ReviewFileSubmissionAsync(challenge.Id, submission.Id, new ReviewChallengeFileSubmissionRequest { Score = 50 })).IsSuccess);
        await ChallengeBestScoreStore.UpsertFileIndividualAsync(db, challenge.Id, task.Id, owner.Id, 0, false, Now.AddMinutes(1), CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(70, completion.Score);
        Assert.Equal(completedAt, completion.CompletedAt);
        Assert.Equal(updatedAt, completion.UpdatedAt);
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static ChallengeService Service(OnlineJudgeDbContext db, User user, IRuntimeStoragePathProvider paths)
    {
        return new ChallengeService(db, new TestCurrentUser(user), new ContentVisibilityPolicy(new FixedTimeProvider(Now)), paths);
    }

    private static User User(string name, UserRole role) => new()
    {
        Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.test", PasswordHash = "hash",
        Role = role, CreatedAt = Now, UpdatedAt = Now
    };

    private static Challenge Challenge(Guid setterId) => new()
    {
        Id = Guid.NewGuid(), Title = "challenge", Description = "test", StartAt = Now.AddHours(-1), EndAt = Now.AddHours(1),
        CreatedByUserId = setterId, IsPublished = true, CreatedAt = Now, UpdatedAt = Now
    };

    private static ChallengeTask FileTask(Guid challengeId) => new()
    {
        Id = Guid.NewGuid(), ChallengeId = challengeId, Title = "file", Description = "test",
        TaskType = ChallengeTaskType.FileUpload, Difficulty = ChallengeTaskDifficulty.Pawn, Score = 100,
        IsPublished = true, CreatedAt = Now, UpdatedAt = Now
    };

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class TestCurrentUser(User user) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => user.Id;
        public string? UserName => user.UserName;
        public UserRole? Role => user.Role;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
