using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Models;
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

    [Fact]
    public async Task UpdateAndDeleteStandardTestCase_ChangesOnlyActiveProblemSemantics()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var testCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Hidden, 20, "1 2", "3");
        var remainingTestCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Hidden, 5, "4", "4");
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var updated = await service.UpdateTestCaseAsync(ids.Problem, testCase.Id, new UpdateTestCaseRequest
        {
            Input = "2 3",
            ExpectedOutput = "5",
            Visibility = TestCaseVisibility.Sample,
            Score = 30
        });

        Assert.True(updated.IsSuccess);
        Assert.Equal("2 3", updated.Value!.Input);
        Assert.Equal(30, updated.Value.Score);
        Assert.True(updated.Value.CreatedAt < (await dbContext.TestCases.SingleAsync(item => item.Id == testCase.Id)).UpdatedAt);

        var deleted = await service.DeleteTestCaseAsync(ids.Problem, testCase.Id);
        var repeated = await service.DeleteTestCaseAsync(ids.Problem, testCase.Id);
        var detail = await service.GetProblemAsync(ids.Problem);
        var export = await service.ExportTestCasesAsync(ids.Problem);
        var stored = await dbContext.TestCases.SingleAsync(item => item.Id == testCase.Id);

        Assert.True(deleted.IsSuccess);
        Assert.True(repeated.IsFailure);
        Assert.Equal("Test case not found.", repeated.ErrorMessage);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(remainingTestCase.Id, Assert.Single(detail.Value!.TestCases).Id);
        Assert.Equal(5, detail.Value.TotalScore);
        Assert.Single(export.Value!);
    }

    [Fact]
    public async Task UpdateFunctionTestCase_UsesAuthoritativeModeAndJsonValidation()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.Function);
        var testCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Hidden, 10, argumentsJson: """{"nums":[2,7],"target":9}""", expectedJson: "[0,1]");
        var service = CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter);

        var invalidJson = await service.UpdateTestCaseAsync(ids.Problem, testCase.Id, new UpdateTestCaseRequest
        {
            ArgumentsJson = "not-json",
            ExpectedJson = "[0,1]",
            Visibility = TestCaseVisibility.Hidden,
            Score = 20
        });
        var wrongMode = await service.UpdateTestCaseAsync(ids.Problem, testCase.Id, new UpdateTestCaseRequest
        {
            Input = "1 2",
            ArgumentsJson = """{"nums":[2,7],"target":9}""",
            ExpectedJson = "[0,1]",
            Visibility = TestCaseVisibility.Hidden,
            Score = 20
        });
        var updated = await service.UpdateTestCaseAsync(ids.Problem, testCase.Id, new UpdateTestCaseRequest
        {
            ArgumentsJson = """{"nums":[3,3],"target":6}""",
            ExpectedJson = "[0,1]",
            Visibility = TestCaseVisibility.Sample,
            Score = 20
        });

        Assert.True(invalidJson.IsFailure);
        Assert.True(wrongMode.IsFailure);
        Assert.True(updated.IsSuccess);
        Assert.Equal("""{"nums":[3,3],"target":6}""", updated.Value!.ArgumentsJson);
    }

    [Fact]
    public async Task UpdateAndDeleteTestCase_EnforceRbacAndProblemBoundary()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var testCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 10, "1", "1");
        var ownerCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 10, "1", "1");
        var collaboratorCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 10, "1", "1");
        var deniedCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 10, "1", "1");
        var unrelatedSetterId = Guid.NewGuid();
        dbContext.Users.Add(User(unrelatedSetterId, "unrelated-setter", UserRole.ProblemSetter));
        dbContext.ProblemCollaborators.Add(new ProblemCollaborator
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            UserId = ids.OtherSetter,
            GrantedByUserId = ids.Owner,
            CanManageTestCases = true,
            CreatedAt = BaseTime
        });
        await dbContext.SaveChangesAsync();
        var update = new UpdateTestCaseRequest { Input = "2", ExpectedOutput = "2", Visibility = TestCaseVisibility.Sample, Score = 10 };

        Assert.True((await CreateProblemService(dbContext, ids.Root, UserRole.Root).UpdateTestCaseAsync(ids.Problem, testCase.Id, update)).IsSuccess);
        Assert.True((await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).UpdateTestCaseAsync(ids.Problem, testCase.Id, update)).IsSuccess);
        Assert.True((await CreateProblemService(dbContext, ids.OtherSetter, UserRole.ProblemSetter).UpdateTestCaseAsync(ids.Problem, testCase.Id, update)).IsSuccess);
        Assert.True((await CreateProblemService(dbContext, ids.Answerer, UserRole.Answerer).UpdateTestCaseAsync(ids.Problem, testCase.Id, update)).IsFailure);
        Assert.True((await CreateProblemService(dbContext, unrelatedSetterId, UserRole.ProblemSetter).UpdateTestCaseAsync(ids.Problem, deniedCase.Id, update)).IsFailure);
        Assert.True((await CreateProblemService(dbContext, ids.Answerer, UserRole.Answerer).DeleteTestCaseAsync(ids.Problem, deniedCase.Id)).IsFailure);
        Assert.True((await CreateProblemService(dbContext, unrelatedSetterId, UserRole.ProblemSetter).DeleteTestCaseAsync(ids.Problem, deniedCase.Id)).IsFailure);
        Assert.True((await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).DeleteTestCaseAsync(ids.Problem, ownerCase.Id)).IsSuccess);
        Assert.True((await CreateProblemService(dbContext, ids.OtherSetter, UserRole.ProblemSetter).DeleteTestCaseAsync(ids.Problem, collaboratorCase.Id)).IsSuccess);

        var otherProblem = Guid.NewGuid();
        dbContext.Problems.Add(new Problem
        {
            Id = otherProblem,
            Title = "Other",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            JudgeMode = JudgeMode.StandardInputOutput,
            CreatedByUserId = ids.Owner,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        await dbContext.SaveChangesAsync();

        var crossUpdate = await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).UpdateTestCaseAsync(otherProblem, testCase.Id, update);
        var crossDelete = await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).DeleteTestCaseAsync(otherProblem, testCase.Id);
        Assert.Equal("Test case not found.", crossUpdate.ErrorMessage);
        Assert.Equal("Test case not found.", crossDelete.ErrorMessage);
        Assert.True((await CreateProblemService(dbContext, ids.Root, UserRole.Root).DeleteTestCaseAsync(ids.Problem, testCase.Id)).IsSuccess);
    }

    [Fact]
    public async Task SubmissionDetail_UsesSnapshotsAndLegacyFallbackAfterTestCaseChanges()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var submissionId = SeedSubmissionWithCaseResults(dbContext, ids);
        var results = await dbContext.SubmissionCaseResults.Include(item => item.TestCase).OrderBy(item => item.TestCase!.Visibility).ToListAsync();
        var sample = results[0];
        var hidden = results[1];
        sample.ExpectedOutputSnapshot = "snapshot expected";
        sample.VisibilitySnapshot = TestCaseVisibility.Hidden;
        sample.ScoreSnapshot = 40;
        hidden.ExpectedOutputSnapshot = null;
        hidden.VisibilitySnapshot = null;
        hidden.ScoreSnapshot = null;
        await dbContext.SaveChangesAsync();
        var update = await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).UpdateTestCaseAsync(ids.Problem, sample.TestCaseId, new UpdateTestCaseRequest
        {
            Input = "changed input",
            ExpectedOutput = "changed expected",
            Visibility = TestCaseVisibility.Sample,
            Score = 90
        });
        Assert.True(update.IsSuccess);
        Assert.True((await CreateProblemService(dbContext, ids.Owner, UserRole.ProblemSetter).DeleteTestCaseAsync(ids.Problem, sample.TestCaseId)).IsSuccess);

        var answerer = await CreateSubmissionService(dbContext, ids.Answerer, UserRole.Answerer).GetSubmissionAsync(submissionId);
        var root = await CreateSubmissionService(dbContext, ids.Root, UserRole.Root).GetSubmissionAsync(submissionId);

        var snapshotted = Assert.Single(answerer.Value!.CaseResults, item => item.TestCaseId == sample.TestCaseId);
        Assert.True(snapshotted.IsHidden);
        Assert.True(snapshotted.IsRedacted);
        Assert.Equal("snapshot expected", Assert.Single(root.Value!.CaseResults, item => item.TestCaseId == sample.TestCaseId).ExpectedOutput);
        Assert.Equal(40, (await dbContext.SubmissionCaseResults.SingleAsync(item => item.Id == sample.Id)).ScoreSnapshot);
        Assert.Equal("secret expected", Assert.Single(root.Value.CaseResults, item => item.TestCaseId == hidden.TestCaseId).ExpectedOutput);
        Assert.Equal(50, Assert.Single(root.Value.CaseResults, item => item.TestCaseId == hidden.TestCaseId).Score);
        Assert.Equal(JudgeStatus.WrongAnswer, (await dbContext.Submissions.SingleAsync()).Status);
    }

    [Fact]
    public async Task FunctionSubmissionDetail_UsesExpectedJsonSnapshotAfterTestCaseUpdate()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.Function);
        var testCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 25, argumentsJson: """{"nums":[2,7],"target":9}""", expectedJson: "[0,1]");
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            UserId = ids.Answerer,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "source",
            Status = JudgeStatus.Accepted,
            CreatedAt = BaseTime
        };
        dbContext.Submissions.Add(submission);
        dbContext.SubmissionCaseResults.Add(new SubmissionCaseResult
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.Id,
            TestCaseId = testCase.Id,
            Status = JudgeStatus.Accepted,
            ExpectedJsonSnapshot = "[0,1]",
            VisibilitySnapshot = TestCaseVisibility.Sample,
            ScoreSnapshot = 25
        });
        testCase.ExpectedJson = "[1,0]";
        testCase.Visibility = TestCaseVisibility.Hidden;
        testCase.Score = 80;
        await dbContext.SaveChangesAsync();

        var result = await CreateSubmissionService(dbContext, ids.Answerer, UserRole.Answerer).GetSubmissionAsync(submission.Id);

        var caseResult = Assert.Single(result.Value!.CaseResults);
        Assert.Equal("[0,1]", caseResult.ExpectedOutput);
        Assert.False(caseResult.IsHidden);
        Assert.False(caseResult.IsRedacted);
        Assert.Equal(25, (await dbContext.SubmissionCaseResults.SingleAsync()).ScoreSnapshot);
    }

    [Fact]
    public async Task SubmissionDetail_DistinguishesLegacyUnknownScoreFromRealZeroSnapshot()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblem(dbContext, JudgeMode.StandardInputOutput);
        var legacyCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Hidden, 10, "legacy", "legacy expected");
        var zeroCase = AddTestCase(dbContext, ids.Problem, TestCaseVisibility.Sample, 20, "zero", "zero expected");
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = ids.Problem,
            UserId = ids.Answerer,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "source",
            Status = JudgeStatus.Accepted,
            CreatedAt = BaseTime
        };
        dbContext.Submissions.Add(submission);
        dbContext.SubmissionCaseResults.AddRange(
            new SubmissionCaseResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                TestCaseId = legacyCase.Id,
                Status = JudgeStatus.Accepted,
                ScoreSnapshot = null,
                VisibilitySnapshot = null
            },
            new SubmissionCaseResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                TestCaseId = zeroCase.Id,
                Status = JudgeStatus.Accepted,
                ScoreSnapshot = 0,
                VisibilitySnapshot = TestCaseVisibility.Hidden
            });
        zeroCase.Score = 20;
        zeroCase.Visibility = TestCaseVisibility.Sample;
        await dbContext.SaveChangesAsync();

        var result = await CreateSubmissionService(dbContext, ids.Root, UserRole.Root).GetSubmissionAsync(submission.Id);

        var legacy = Assert.Single(result.Value!.CaseResults, item => item.TestCaseId == legacyCase.Id);
        var snapshottedZero = Assert.Single(result.Value.CaseResults, item => item.TestCaseId == zeroCase.Id);
        Assert.Equal(10, legacy.Score);
        Assert.True(legacy.IsHidden);
        Assert.Equal(0, snapshottedZero.Score);
        Assert.True(snapshottedZero.IsHidden);
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

    private static TestCase AddTestCase(
        OnlineJudgeDbContext dbContext,
        Guid problemId,
        TestCaseVisibility visibility,
        int score,
        string input = "",
        string expectedOutput = "",
        string? argumentsJson = null,
        string? expectedJson = null)
    {
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Input = input,
            ExpectedOutput = expectedOutput,
            ArgumentsJson = argumentsJson,
            ExpectedJson = expectedJson,
            Visibility = visibility,
            Score = score,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
        dbContext.TestCases.Add(testCase);
        dbContext.SaveChanges();
        return testCase;
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
        public Task<bool> TryEnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<JudgeQueueReadResult> TryDequeueSubmissionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(JudgeQueueReadResult.Empty);
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
