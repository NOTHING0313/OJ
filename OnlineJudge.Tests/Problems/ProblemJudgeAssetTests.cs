using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Submissions.Dtos;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;

namespace OnlineJudge.Tests.Problems;

public class ProblemJudgeAssetTests : IDisposable
{
    private readonly string storageRoot = Path.Combine(Path.GetTempPath(), "onlinejudge-asset-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("../helper.cpp")]
    [InlineData("folder/helper.cpp")]
    [InlineData("folder\\helper.cpp")]
    [InlineData("\u0001helper.cpp")]
    [InlineData("")]
    [InlineData("helper.cpp;echo injected")]
    [InlineData("-helper.cpp")]
    [InlineData("helper$(echo).cpp")]
    [InlineData("helper`echo`.cpp")]
    public void ValidateFileName_RejectsTraversalOrInvalidNames(string fileName)
    {
        Assert.True(ProblemJudgeAssetService.ValidateFileName(JudgeLanguage.Cpp17, fileName).IsFailure);
    }

    [Theory]
    [InlineData(JudgeLanguage.Cpp17, "helper.c")]
    [InlineData(JudgeLanguage.C11, "helper.cpp")]
    [InlineData(JudgeLanguage.CSharp, "helper.csproj")]
    [InlineData(JudgeLanguage.CSharp, "Program.cs")]
    [InlineData(JudgeLanguage.Cpp17, "main.cpp")]
    [InlineData(JudgeLanguage.C11, "main.c")]
    [InlineData(JudgeLanguage.CSharp, "Main.csproj")]
    public void ValidateFileName_RejectsWrongExtensionOrReservedName(JudgeLanguage language, string fileName)
    {
        Assert.True(ProblemJudgeAssetService.ValidateFileName(language, fileName).IsFailure);
    }

    [Fact]
    public async Task CreateAsset_ValidatesSizeUtf8AndDuplicateName()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext);
        var service = CreateService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var oversized = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "helper.cpp", new byte[ProblemJudgeAssetService.MaxFileSizeBytes + 1]));
        var invalidUtf8 = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "helper.cpp", [0xff, 0xfe]));
        var binaryControl = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "helper.cpp", [0x00, 0x01]));
        var first = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "Helper.cpp", Encoding.UTF8.GetBytes("int helper() { return 1; }")));
        var duplicate = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "helper.cpp", Encoding.UTF8.GetBytes("int helper() { return 2; }")));

        Assert.True(oversized.IsFailure);
        Assert.True(invalidUtf8.IsFailure);
        Assert.True(binaryControl.IsFailure);
        Assert.True(first.IsSuccess);
        Assert.True(duplicate.IsFailure);
        Assert.Single(await dbContext.ProblemJudgeAssets.ToListAsync());
    }

    [Fact]
    public async Task Permissions_ApplyOwnerCollaboratorRootAndAnswererRules()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext);
        dbContext.ProblemCollaborators.Add(new ProblemCollaborator
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            UserId = ids.Collaborator,
            GrantedByUserId = ids.Owner,
            CanEditProblem = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var owner = await CreateService(dbContext, ids.Owner, UserRole.ProblemSetter).CreateAssetAsync(ids.Problem, Request(JudgeLanguage.Cpp17, "owner.cpp", Encoding.UTF8.GetBytes("int owner() { return 1; }")));
        var collaborator = await CreateService(dbContext, ids.Collaborator, UserRole.ProblemSetter).CreateAssetAsync(ids.Problem, Request(JudgeLanguage.C11, "collaborator.c", Encoding.UTF8.GetBytes("int collaborator(void) { return 1; }")));
        var noAccess = await CreateService(dbContext, ids.OtherSetter, UserRole.ProblemSetter).CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, "Other.cs", Encoding.UTF8.GetBytes("class Other {}")));
        var rootUpload = await CreateService(dbContext, ids.Root, UserRole.Root).CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, "RootHelper.cs", Encoding.UTF8.GetBytes("class RootHelper {}")));
        var rootList = await CreateService(dbContext, ids.Root, UserRole.Root).GetAssetsAsync(ids.Problem);
        var answererList = await CreateService(dbContext, ids.Answerer, UserRole.Answerer).GetAssetsAsync(ids.Problem);
        var answererUpload = await CreateService(dbContext, ids.Answerer, UserRole.Answerer).CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, "Answerer.cs", Encoding.UTF8.GetBytes("class Answerer {}")));
        var rootDelete = await CreateService(dbContext, ids.Root, UserRole.Root).DeleteAssetAsync(ids.Problem, owner.Value!.Id);
        var answererDelete = await CreateService(dbContext, ids.Answerer, UserRole.Answerer).DeleteAssetAsync(ids.Problem, collaborator.Value!.Id);

        Assert.True(owner.IsSuccess);
        Assert.True(collaborator.IsSuccess);
        Assert.True(rootUpload.IsSuccess);
        Assert.Equal(3, rootList.Value!.Count);
        Assert.Equal("Forbidden.", noAccess.ErrorMessage);
        Assert.Equal("Forbidden.", answererList.ErrorMessage);
        Assert.Equal("Forbidden.", answererUpload.ErrorMessage);
        Assert.True(rootDelete.IsSuccess);
        Assert.Equal("Forbidden.", answererDelete.ErrorMessage);
    }

    [Fact]
    public async Task CreateAsset_EnforcesEightFilesPerLanguage()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext);
        var service = CreateService(dbContext, ids.Owner, UserRole.ProblemSetter);

        for (var index = 0; index < ProblemJudgeAssetService.MaxAssetsPerLanguage; index++)
        {
            var result = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, $"Helper{index}.cs", Encoding.UTF8.GetBytes($"class Helper{index} {{}}")));
            Assert.True(result.IsSuccess);
        }

        var ninth = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, "Helper8.cs", Encoding.UTF8.GetBytes("class Helper8 {}")));

        Assert.True(ninth.IsFailure);
        Assert.Equal(ProblemJudgeAssetService.MaxAssetsPerLanguage, await dbContext.ProblemJudgeAssets.CountAsync());
    }

    [Fact]
    public async Task CreateAsset_WhenDatabaseSaveFails_RemovesStoredFile()
    {
        await using var dbContext = CreateFailingDbContext();
        var ids = SeedProblem(dbContext);
        var service = CreateService(dbContext, ids.Owner, UserRole.ProblemSetter);
        dbContext.FailNextSave = true;

        var result = await service.CreateAssetAsync(ids.Problem, Request(JudgeLanguage.CSharp, "Geometry.cs", Encoding.UTF8.GetBytes("static class Geometry {}")));

        Assert.True(result.IsFailure);
        Assert.False(Directory.Exists(storageRoot) && Directory.EnumerateFiles(storageRoot, "*", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public async Task DeleteAsset_WhenDatabaseSaveFails_RestoresStoredFile()
    {
        await using var dbContext = CreateFailingDbContext();
        var ids = SeedProblem(dbContext);
        var storage = CreateStorage();
        var content = Encoding.UTF8.GetBytes("static class Geometry {}");
        var stored = await storage.WriteAsync(ids.Problem, JudgeLanguage.CSharp, ".cs", content);
        var asset = Asset(ids.Problem, JudgeLanguage.CSharp, "Geometry.cs", stored);
        dbContext.ProblemJudgeAssets.Add(asset);
        await dbContext.SaveChangesAsync();
        dbContext.FailNextSave = true;

        var result = await new ProblemJudgeAssetService(dbContext, CurrentUser(ids.Root, UserRole.Root), storage)
            .DeleteAssetAsync(ids.Problem, asset.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("static class Geometry {}", await storage.ReadTextAsync(stored.StorageRelativePath, stored.FileSizeBytes, stored.Sha256));
    }

    [Fact]
    public async Task CompileAssetLoader_LoadsOnlyRequestedLanguageAndChecksIntegrity()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext);
        var storage = CreateStorage();
        var cppStored = await storage.WriteAsync(ids.Problem, JudgeLanguage.Cpp17, ".cpp", Encoding.UTF8.GetBytes("int helper() { return 7; }"));
        var csharpStored = await storage.WriteAsync(ids.Problem, JudgeLanguage.CSharp, ".cs", Encoding.UTF8.GetBytes("class Helper {}"));
        var cStored = await storage.WriteAsync(ids.Problem, JudgeLanguage.C11, ".c", Encoding.UTF8.GetBytes("int helper(void) { return 3; }"));
        dbContext.ProblemJudgeAssets.AddRange(
            Asset(ids.Problem, JudgeLanguage.Cpp17, "helper.cpp", cppStored),
            Asset(ids.Problem, JudgeLanguage.CSharp, "Helper.cs", csharpStored),
            Asset(ids.Problem, JudgeLanguage.C11, "helper.c", cStored));
        await dbContext.SaveChangesAsync();

        var loader = new JudgeCompileAssetLoader(dbContext, storage);
        var cppAssets = await loader.LoadAsync(ids.Problem, JudgeLanguage.Cpp17);
        var cAssets = await loader.LoadAsync(ids.Problem, JudgeLanguage.C11);
        var csharpAssets = await loader.LoadAsync(ids.Problem, JudgeLanguage.CSharp);

        Assert.Equal("helper.cpp", Assert.Single(cppAssets).FileName);
        Assert.Equal("helper.c", Assert.Single(cAssets).FileName);
        Assert.Equal("Helper.cs", Assert.Single(csharpAssets).FileName);
    }

    [Fact]
    public async Task Storage_RejectsEscapingPathAndHashMismatch()
    {
        var storage = CreateStorage();
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ReadTextAsync("../secret.cs", 1, new string('0', 64)));

        var stored = await storage.WriteAsync(Guid.NewGuid(), JudgeLanguage.CSharp, ".cs", Encoding.UTF8.GetBytes("class A {}"));
        await Assert.ThrowsAsync<InvalidDataException>(() => storage.ReadTextAsync(stored.StorageRelativePath, stored.FileSizeBytes, new string('0', 64)));
    }

    [Fact]
    public void PublicProblemDtoAndManagementDto_DoNotExposeStorageOrContent()
    {
        var publicProperties = typeof(ProblemDetailDto).GetProperties().Select(property => property.Name).ToList();
        var problemListProperties = typeof(ProblemListItemDto).GetProperties().Select(property => property.Name).ToList();
        var submissionProperties = typeof(SubmissionDto).GetProperties().Select(property => property.Name).ToList();
        var submissionListProperties = typeof(SubmissionListItemDto).GetProperties().Select(property => property.Name).ToList();
        var managementProperties = typeof(ProblemJudgeAssetDto).GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain(publicProperties, name => name.Contains("Asset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(problemListProperties, name => name.Contains("Asset", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(submissionProperties, name => name.Contains("Asset", StringComparison.OrdinalIgnoreCase) || name.Contains("Compile", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(submissionListProperties, name => name.Contains("Asset", StringComparison.OrdinalIgnoreCase) || name.Contains("Compile", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(managementProperties, name => name is "StoredFileName" or "StorageRelativePath" or "Content");
    }

    public void Dispose()
    {
        if (Directory.Exists(storageRoot))
        {
            Directory.Delete(storageRoot, recursive: true);
        }
    }

    private ProblemJudgeAssetService CreateService(OnlineJudgeDbContext dbContext, Guid userId, UserRole role)
    {
        return new ProblemJudgeAssetService(dbContext, CurrentUser(userId, role), CreateStorage());
    }

    private ProblemJudgeAssetStorage CreateStorage()
    {
        var configuration = new ConfigurationManager
        {
            ["JudgeAssets:StorageRoot"] = storageRoot
        };
        return new ProblemJudgeAssetStorage(configuration);
    }

    private static CreateProblemJudgeAssetRequest Request(JudgeLanguage language, string fileName, byte[] content)
    {
        return new CreateProblemJudgeAssetRequest
        {
            Language = language,
            OriginalFileName = fileName,
            FileSizeBytes = content.LongLength,
            Content = new MemoryStream(content)
        };
    }

    private static ProblemJudgeAsset Asset(Guid problemId, JudgeLanguage language, string fileName, StoredJudgeAssetFile stored)
    {
        return new ProblemJudgeAsset
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Language = language,
            OriginalFileName = fileName,
            NormalizedFileName = fileName.ToUpperInvariant(),
            StoredFileName = stored.StoredFileName,
            StorageRelativePath = stored.StorageRelativePath,
            Sha256 = stored.Sha256,
            FileSizeBytes = stored.FileSizeBytes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static TestIds SeedProblem(OnlineJudgeDbContext dbContext)
    {
        var ids = new TestIds();
        dbContext.Users.AddRange(
            User(ids.Root, "root", UserRole.Root),
            User(ids.Owner, "owner", UserRole.ProblemSetter),
            User(ids.Collaborator, "collaborator", UserRole.ProblemSetter),
            User(ids.OtherSetter, "other", UserRole.ProblemSetter),
            User(ids.Answerer, "answerer", UserRole.Answerer));
        dbContext.Problems.Add(new Problem
        {
            Id = ids.Problem,
            Title = "Problem",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            CreatedByUserId = ids.Owner,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        dbContext.SaveChanges();
        return ids;
    }

    private static User User(Guid id, string name, UserRole role)
    {
        return new User
        {
            Id = id,
            UserName = name,
            Email = $"{name}@example.com",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static ICurrentUser CurrentUser(Guid userId, UserRole role) => new TestCurrentUser(userId, role);

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static FailingDbContext CreateFailingDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new FailingDbContext(options);
    }

    private sealed class FailingDbContext(DbContextOptions<OnlineJudgeDbContext> options) : OnlineJudgeDbContext(options)
    {
        public bool FailNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new DbUpdateException("Simulated database failure.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "test";
        public UserRole? Role => role;
    }

    private sealed class TestIds
    {
        public Guid Problem { get; } = Guid.NewGuid();
        public Guid Root { get; } = Guid.NewGuid();
        public Guid Owner { get; } = Guid.NewGuid();
        public Guid Collaborator { get; } = Guid.NewGuid();
        public Guid OtherSetter { get; } = Guid.NewGuid();
        public Guid Answerer { get; } = Guid.NewGuid();
    }
}
