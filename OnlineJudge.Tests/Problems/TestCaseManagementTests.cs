using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Problems;

public class TestCaseManagementTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ImportStandardTestCases_Succeeds()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var result = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest
        {
            Items =
            [
                new ImportTestCaseItemRequest
                {
                    Input = "1 2",
                    ExpectedOutput = "3",
                    Visibility = TestCaseVisibility.Sample
                },
                new ImportTestCaseItemRequest
                {
                    Input = "10 20",
                    ExpectedOutput = "30"
                }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Errors);
        Assert.Equal(2, result.Value.ImportedCount);
        Assert.Equal(2, await dbContext.TestCases.CountAsync());
        Assert.Contains(await dbContext.TestCases.ToListAsync(), testCase => testCase.Visibility == TestCaseVisibility.Hidden);
    }

    [Fact]
    public async Task ImportFunctionTestCases_Succeeds()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.Function);
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var result = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest
        {
            Items =
            [
                new ImportTestCaseItemRequest
                {
                    ArgumentsJson = Json("""{"nums":[2,7,11,15],"target":9}"""),
                    ExpectedJson = Json("[0,1]"),
                    Visibility = TestCaseVisibility.Sample,
                    Score = 100
                }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Errors);
        var testCase = Assert.Single(await dbContext.TestCases.ToListAsync());
        Assert.Equal(string.Empty, testCase.Input);
        Assert.Equal("""{"nums":[2,7,11,15],"target":9}""", testCase.ArgumentsJson);
    }

    [Fact]
    public async Task ImportFunctionTreeNodeInvalidJson_ReturnsError()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.Function, TreeFunctionSpecJson());
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var result = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest
        {
            Items =
            [
                new ImportTestCaseItemRequest
                {
                    ArgumentsJson = Json("""{"root":[1,"x"]}"""),
                    ExpectedJson = Json("[]")
                }
            ]
        });

        Assert.True(result.IsSuccess);
        var error = Assert.Single(result.Value!.Errors);
        Assert.Equal(1, error.Index);
        Assert.Contains("TreeNode<int> expects a level-order integer array JSON value.", error.Message);
        Assert.Equal(0, await dbContext.TestCases.CountAsync());
    }

    [Fact]
    public async Task ImportTransaction_RollsBack_WhenAnyItemInvalid()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var result = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest
        {
            Items =
            [
                new ImportTestCaseItemRequest { Input = "1 2", ExpectedOutput = "3" },
                new ImportTestCaseItemRequest { Input = "bad", ArgumentsJson = Json("""{"x":1}""") }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.Errors);
        Assert.Equal(0, await dbContext.TestCases.CountAsync());
    }

    [Fact]
    public async Task ExportTestCases_CanBeImportedAgain()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.Function);
        dbContext.TestCases.Add(new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            Input = string.Empty,
            ExpectedOutput = string.Empty,
            ArgumentsJson = """{"nums":[2,7,11,15],"target":9}""",
            ExpectedJson = "[0,1]",
            Score = 100,
            Visibility = TestCaseVisibility.Sample,
            CreatedAt = BaseTime
        });
        await dbContext.SaveChangesAsync();

        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);
        var export = await service.ExportTestCasesAsync(ids.Problem);

        Assert.True(export.IsSuccess);
        var item = Assert.Single(export.Value!);
        Assert.NotNull(item.ArgumentsJson);
        Assert.Equal(JsonValueKind.Object, item.ArgumentsJson!.Value.ValueKind);
        Assert.Equal(TestCaseVisibility.Sample, item.Visibility);
        Assert.Equal("""{"nums":[2,7,11,15],"target":9}""", item.ArgumentsJson.Value.GetRawText());

        var secondProblemId = Guid.NewGuid();
        dbContext.Problems.Add(new Problem
        {
            Id = secondProblemId,
            Title = "Imported Problem",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = JudgeMode.Function,
            FunctionSpecJson = TwoSumFunctionSpecJson(),
            StarterCodeJson = """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}""",
            CreatedByUserId = ids.Owner,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        await dbContext.SaveChangesAsync();

        var reimport = await service.ImportTestCasesAsync(secondProblemId, new ImportTestCasesRequest
        {
            Items = export.Value!
                .Select(exported => new ImportTestCaseItemRequest
                {
                    ArgumentsJson = exported.ArgumentsJson,
                    ExpectedJson = exported.ExpectedJson,
                    Score = exported.Score,
                    Visibility = exported.Visibility
                })
                .ToList()
        });

        Assert.True(reimport.IsSuccess);
        Assert.Empty(reimport.Value!.Errors);
        Assert.Equal(2, await dbContext.TestCases.CountAsync());
    }

    [Fact]
    public async Task Answerer_CannotImportOrExportTestCases()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var service = CreateProblemService(dbContext, ids.Answerer, UserRole.Answerer);

        var import = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest());
        var export = await service.ExportTestCasesAsync(ids.Problem);

        Assert.True(import.IsFailure);
        Assert.Equal("Forbidden.", import.ErrorMessage);
        Assert.True(export.IsFailure);
        Assert.Equal("Forbidden.", export.ErrorMessage);
    }

    [Fact]
    public async Task ProblemSetter_CannotManageOthersProblemTestCases()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var service = CreateProblemService(dbContext, ids.OtherSetter, UserRole.ProblemSetter);

        var result = await service.ImportTestCasesAsync(ids.Problem, new ImportTestCasesRequest());

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    [Fact]
    public async Task ProblemDetail_ForAnonymousOrAnswerer_OnlyReturnsSampleCases()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        SeedSampleAndHiddenTestCases(dbContext, ids.Problem);

        var anonymousService = new ProblemService(dbContext, new TestCurrentUser(null, null, false));
        var answererService = CreateProblemService(dbContext, ids.Answerer, UserRole.Answerer);
        var rootService = CreateProblemService(dbContext, ids.Root, UserRole.Root);

        var anonymous = await anonymousService.GetProblemAsync(ids.Problem);
        var answerer = await answererService.GetProblemAsync(ids.Problem);
        var root = await rootService.GetProblemAsync(ids.Problem);

        Assert.Single(anonymous.Value!.TestCases);
        Assert.Single(answerer.Value!.TestCases);
        Assert.Equal(TestCaseVisibility.Sample, anonymous.Value.TestCases[0].Visibility);
        Assert.Equal(2, root.Value!.TestCases.Count);
    }

    [Fact]
    public async Task SubmissionDetail_ForHiddenCase_DoesNotExposeExpectedOrActualOutput()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var submissionId = SeedSubmissionWithCaseResults(dbContext, ids);
        var service = CreateSubmissionService(dbContext, ids.Answerer, UserRole.Answerer);

        var result = await service.GetSubmissionAsync(submissionId);

        Assert.True(result.IsSuccess);
        var hidden = Assert.Single(result.Value!.CaseResults, item => item.IsHidden);
        Assert.True(hidden.IsRedacted);
        Assert.Null(hidden.ActualOutput);
        Assert.Null(hidden.ExpectedOutput);
        Assert.DoesNotContain("secret", hidden.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RootSubmissionDetail_CanSeeHiddenCaseOutput()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var submissionId = SeedSubmissionWithCaseResults(dbContext, ids);
        var service = CreateSubmissionService(dbContext, ids.Root, UserRole.Root);

        var result = await service.GetSubmissionAsync(submissionId);

        Assert.True(result.IsSuccess);
        var hidden = Assert.Single(result.Value!.CaseResults, item => item.IsHidden);
        Assert.False(hidden.IsRedacted);
        Assert.Equal("secret actual", hidden.ActualOutput);
        Assert.Equal("secret expected", hidden.ExpectedOutput);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static ProblemService CreateProblemService(OnlineJudgeDbContext dbContext, Guid currentUserId, UserRole role)
    {
        return new ProblemService(dbContext, new TestCurrentUser(currentUserId, role));
    }

    private static SubmissionService CreateSubmissionService(OnlineJudgeDbContext dbContext, Guid currentUserId, UserRole role)
    {
        return new SubmissionService(dbContext, new NoopJudgeQueue(), new TestCurrentUser(currentUserId, role));
    }

    private static TestIds SeedUsersAndProblem(OnlineJudgeDbContext dbContext, JudgeMode judgeMode, string? functionSpecJson = null)
    {
        var ids = new TestIds();
        dbContext.Users.AddRange(
            User(ids.Root, "root", UserRole.Root),
            User(ids.Owner, "owner", UserRole.ProblemSetter),
            User(ids.OtherSetter, "other-setter", UserRole.ProblemSetter),
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
            IsPublished = true,
            JudgeMode = judgeMode,
            FunctionSpecJson = judgeMode == JudgeMode.Function ? functionSpecJson ?? TwoSumFunctionSpecJson() : null,
            StarterCodeJson = judgeMode == JudgeMode.Function ? """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}""" : null,
            CreatedByUserId = ids.Owner,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        dbContext.SaveChanges();
        return ids;
    }

    private static void SeedSampleAndHiddenTestCases(OnlineJudgeDbContext dbContext, Guid problemId)
    {
        dbContext.TestCases.AddRange(
            new TestCase
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                Input = "1 2",
                ExpectedOutput = "3",
                Visibility = TestCaseVisibility.Sample,
                Score = 50,
                CreatedAt = BaseTime
            },
            new TestCase
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                Input = "secret input",
                ExpectedOutput = "secret expected",
                Visibility = TestCaseVisibility.Hidden,
                Score = 50,
                CreatedAt = BaseTime.AddMinutes(1)
            });
        dbContext.SaveChanges();
    }

    private static Guid SeedSubmissionWithCaseResults(OnlineJudgeDbContext dbContext, TestIds ids)
    {
        var sampleCase = new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            Input = "1 2",
            ExpectedOutput = "3",
            Visibility = TestCaseVisibility.Sample,
            Score = 50,
            CreatedAt = BaseTime
        };
        var hiddenCase = new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            Input = "secret input",
            ExpectedOutput = "secret expected",
            Visibility = TestCaseVisibility.Hidden,
            Score = 50,
            CreatedAt = BaseTime.AddMinutes(1)
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            UserId = ids.Answerer,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "source",
            Status = JudgeStatus.WrongAnswer,
            CreatedAt = BaseTime,
            Problem = dbContext.Problems.Local.First(problem => problem.Id == ids.Problem),
            User = dbContext.Users.Local.First(user => user.Id == ids.Answerer)
        };

        dbContext.TestCases.AddRange(sampleCase, hiddenCase);
        dbContext.Submissions.Add(submission);
        dbContext.SubmissionCaseResults.AddRange(
            new SubmissionCaseResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                TestCaseId = sampleCase.Id,
                Status = JudgeStatus.Accepted,
                ActualOutput = "3",
                TestCase = sampleCase,
                Submission = submission
            },
            new SubmissionCaseResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                TestCaseId = hiddenCase.Id,
                Status = JudgeStatus.WrongAnswer,
                ActualOutput = "secret actual",
                ErrorMessage = "secret error with secret input",
                TestCase = hiddenCase,
                Submission = submission
            });
        dbContext.SaveChanges();
        return submission.Id;
    }

    private static User User(Guid id, string userName, UserRole role)
    {
        return new User
        {
            Id = id,
            UserName = userName,
            Email = $"{userName}@example.test",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static string TwoSumFunctionSpecJson()
    {
        return """
               {
                 "functionName": "twoSum",
                 "returnType": "int[]",
                 "parameters": [
                   { "name": "nums", "type": "int[]" },
                   { "name": "target", "type": "int" }
                 ],
                 "supportedLanguages": ["cpp17", "csharp", "c11"]
               }
               """;
    }

    private static string TreeFunctionSpecJson()
    {
        return """
               {
                 "functionName": "invertTree",
                 "returnType": "TreeNode<int>",
                 "parameters": [
                   { "name": "root", "type": "TreeNode<int>" }
                 ],
                 "supportedLanguages": ["cpp17", "csharp"]
               }
               """;
    }

    private sealed class TestCurrentUser(Guid? userId, UserRole? role, bool isAuthenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => isAuthenticated;

        public Guid? UserId => userId;

        public string? UserName => "test-user";

        public UserRole? Role => role;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestIds
    {
        public Guid Root { get; } = Guid.NewGuid();

        public Guid Owner { get; } = Guid.NewGuid();

        public Guid OtherSetter { get; } = Guid.NewGuid();

        public Guid Answerer { get; } = Guid.NewGuid();

        public Guid Problem { get; } = Guid.NewGuid();
    }
}
