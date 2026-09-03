using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;

namespace OnlineJudge.Tests.Problems;

public class ProblemJudgeRevisionTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreatePublishedProblem_WithoutTestCases_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var service = CreateService(dbContext, ownerId);

        var result = await service.CreateProblemAsync(StandardCreateProblemRequest(isPublished: true));

        Assert.True(result.IsFailure);
        Assert.Contains("at least one active test case", result.ErrorMessage);
        Assert.Empty(await dbContext.Problems.ToListAsync());
    }

    [Fact]
    public async Task PublishingDraft_CreatesImmutableSnapshotAndCurrentPointer()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var problem = SeedProblem(dbContext, ownerId, isPublished: false);
        var testCase = SeedTestCase(dbContext, problem.Id, "1 2", "3", 40);
        var service = CreateService(dbContext, ownerId);

        var result = await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true));

        Assert.True(result.IsSuccess);
        var storedProblem = await dbContext.Problems.AsNoTracking().SingleAsync();
        var revision = await dbContext.ProblemJudgeRevisions.AsNoTracking()
            .Include(item => item.TestCases)
            .SingleAsync();
        Assert.Equal(revision.Id, storedProblem.CurrentJudgeRevisionId);
        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal(testCase.Id, Assert.Single(revision.TestCases).SourceTestCaseId);
        Assert.Equal("3", revision.TestCases[0].ExpectedOutput);
        Assert.Equal(40, revision.TestCases[0].Score);

        dbContext.ChangeTracker.Clear();
        revision.MemoryLimitMb++;
        dbContext.Update(revision);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task EditingPublishedTestCase_CreatesNewRevisionAndPreservesPreviousSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var problem = SeedProblem(dbContext, ownerId, isPublished: false);
        var testCase = SeedTestCase(dbContext, problem.Id, "1", "1", 10);
        var service = CreateService(dbContext, ownerId);
        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true))).IsSuccess);

        var update = await service.UpdateTestCaseAsync(problem.Id, testCase.Id, new UpdateTestCaseRequest
        {
            Input = "2",
            ExpectedOutput = "4",
            Visibility = TestCaseVisibility.Hidden,
            Score = 25
        });

        Assert.True(update.IsSuccess);
        var revisions = await dbContext.ProblemJudgeRevisions.AsNoTracking()
            .Include(item => item.TestCases)
            .OrderBy(item => item.RevisionNumber)
            .ToListAsync();
        Assert.Equal(2, revisions.Count);
        Assert.Equal("1", Assert.Single(revisions[0].TestCases).ExpectedOutput);
        Assert.Equal(10, revisions[0].TestCases[0].Score);
        Assert.Equal("4", Assert.Single(revisions[1].TestCases).ExpectedOutput);
        Assert.Equal(25, revisions[1].TestCases[0].Score);
        Assert.Equal(revisions[1].Id, (await dbContext.Problems.AsNoTracking().SingleAsync()).CurrentJudgeRevisionId);
    }

    [Fact]
    public async Task PublishedProblem_CannotDeleteItsLastActiveTestCase()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var problem = SeedProblem(dbContext, ownerId, isPublished: false);
        var testCase = SeedTestCase(dbContext, problem.Id, "1", "1", 10);
        var service = CreateService(dbContext, ownerId);
        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true))).IsSuccess);

        var result = await service.DeleteTestCaseAsync(problem.Id, testCase.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("at least one active test case", result.ErrorMessage);
        Assert.False((await dbContext.TestCases.AsNoTracking().SingleAsync()).IsDeleted);
        Assert.Single(await dbContext.ProblemJudgeRevisions.ToListAsync());
    }

    [Fact]
    public async Task SwitchingPublishedJudgeMode_RejectsIncompatibleExistingTestCasesWithoutPersistingChange()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var problem = SeedProblem(dbContext, ownerId, isPublished: false);
        SeedTestCase(dbContext, problem.Id, "1", "1", 10);
        var service = CreateService(dbContext, ownerId);
        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true))).IsSuccess);

        var result = await service.UpdateProblemAsync(problem.Id, FunctionProblemRequest());

        Assert.True(result.IsFailure);
        Assert.Contains("Function test cases cannot use standard input/output fields", result.ErrorMessage);
        var storedProblem = await dbContext.Problems.AsNoTracking().SingleAsync();
        Assert.Equal(JudgeMode.StandardInputOutput, storedProblem.JudgeMode);
        Assert.Single(await dbContext.ProblemJudgeRevisions.ToListAsync());
    }

    [Fact]
    public async Task PresentationOnlyUpdate_ReusesRevisionButRepublishCreatesANewRevision()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = SeedOwner(dbContext);
        var problem = SeedProblem(dbContext, ownerId, isPublished: false);
        SeedTestCase(dbContext, problem.Id, "1", "1", 10);
        var service = CreateService(dbContext, ownerId);
        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true))).IsSuccess);
        var firstRevisionId = (await dbContext.Problems.AsNoTracking().SingleAsync()).CurrentJudgeRevisionId;

        var presentationUpdate = StandardUpdateProblemRequest(isPublished: true);
        presentationUpdate.Title = "Renamed";
        Assert.True((await service.UpdateProblemAsync(problem.Id, presentationUpdate)).IsSuccess);
        Assert.Single(await dbContext.ProblemJudgeRevisions.ToListAsync());
        Assert.Equal(firstRevisionId, (await dbContext.Problems.AsNoTracking().SingleAsync()).CurrentJudgeRevisionId);

        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: false))).IsSuccess);
        Assert.True((await service.UpdateProblemAsync(problem.Id, StandardUpdateProblemRequest(isPublished: true))).IsSuccess);
        Assert.Equal(2, await dbContext.ProblemJudgeRevisions.CountAsync());
        Assert.NotEqual(firstRevisionId, (await dbContext.Problems.AsNoTracking().SingleAsync()).CurrentJudgeRevisionId);
    }

    private static ProblemService CreateService(OnlineJudgeDbContext dbContext, Guid ownerId)
        => new(dbContext, new TestCurrentUser(ownerId));

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static Guid SeedOwner(OnlineJudgeDbContext dbContext)
    {
        var ownerId = Guid.NewGuid();
        dbContext.Users.Add(new User
        {
            Id = ownerId,
            UserName = "owner",
            Email = "owner@example.test",
            PasswordHash = "hash",
            Role = UserRole.ProblemSetter,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        dbContext.SaveChanges();
        return ownerId;
    }

    private static Problem SeedProblem(OnlineJudgeDbContext dbContext, Guid ownerId, bool isPublished)
    {
        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Title = "Problem",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = isPublished,
            JudgeMode = JudgeMode.StandardInputOutput,
            CreatedByUserId = ownerId,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
        dbContext.Problems.Add(problem);
        dbContext.SaveChanges();
        return problem;
    }

    private static TestCase SeedTestCase(OnlineJudgeDbContext dbContext, Guid problemId, string input, string expectedOutput, int score)
    {
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Input = input,
            ExpectedOutput = expectedOutput,
            Visibility = TestCaseVisibility.Hidden,
            Score = score,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
        dbContext.TestCases.Add(testCase);
        dbContext.SaveChanges();
        return testCase;
    }

    private static CreateProblemRequest StandardCreateProblemRequest(bool isPublished) => new()
    {
        Title = "Problem",
        Description = "Description",
        InputDescription = "Input",
        OutputDescription = "Output",
        TimeLimitMs = 1000,
        MemoryLimitMb = 128,
        IsPublished = isPublished,
        JudgeMode = JudgeMode.StandardInputOutput
    };

    private static UpdateProblemRequest StandardUpdateProblemRequest(bool isPublished) => new()
    {
        Title = "Problem",
        Description = "Description",
        InputDescription = "Input",
        OutputDescription = "Output",
        TimeLimitMs = 1000,
        MemoryLimitMb = 128,
        IsPublished = isPublished,
        JudgeMode = JudgeMode.StandardInputOutput
    };

    private static UpdateProblemRequest FunctionProblemRequest() => new()
    {
        Title = "Problem",
        Description = "Description",
        InputDescription = string.Empty,
        OutputDescription = string.Empty,
        TimeLimitMs = 1000,
        MemoryLimitMb = 128,
        IsPublished = true,
        JudgeMode = JudgeMode.Function,
        FunctionSpecJson = """
            {"functionName":"solve","returnType":"int","parameters":[{"name":"value","type":"int"}],"supportedLanguages":["cpp17","csharp","c11"]}
            """,
        StarterCodeJson = """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(int value){return value;}"}"""
    };

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "owner";
        public UserRole? Role => UserRole.ProblemSetter;
    }
}
