using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Problems;

internal static class ProblemJudgeRevisionPublisher
{
    public const string NoActiveTestCasesMessage = "A problem must contain at least one active test case before it can be published.";

    public static Task AcquireProblemLockAsync(OnlineJudgeDbContext dbContext, Guid problemId, CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? ScoringIdentityTransactionLock.AcquireAsync(dbContext, "problem-judge-revision", [problemId], cancellationToken)
            : Task.CompletedTask;
    }

    public static async Task<Result<ProblemJudgeRevision>> PublishAsync(
        OnlineJudgeDbContext dbContext,
        Problem problem,
        JudgeResourcePolicy resourcePolicy,
        CancellationToken cancellationToken)
    {
        var problemValidation = ProblemJudgeDefinitionValidator.ValidateProblem(
            problem.Title,
            problem.Description,
            problem.InputDescription,
            problem.OutputDescription,
            problem.TimeLimitMs,
            problem.MemoryLimitMb,
            problem.JudgeMode,
            problem.AllowedLanguagesMask,
            problem.FunctionSpecJson,
            problem.StarterCodeJson,
            resourcePolicy);
        if (problemValidation.IsFailure)
        {
            return Result<ProblemJudgeRevision>.Failure(problemValidation.ErrorMessage!);
        }

        var storedTestCases = await dbContext.TestCases
            .AsNoTracking()
            .Where(testCase => testCase.ProblemId == problem.Id && !testCase.IsDeleted)
            .ToListAsync(cancellationToken);
        var testCases = ApplyTrackedChanges(
                storedTestCases,
                dbContext.ChangeTracker.Entries<TestCase>()
                    .Where(entry => entry.Entity.ProblemId == problem.Id)
                    .Select(entry => entry.Entity),
                testCase => testCase.Id)
            .Where(testCase => !testCase.IsDeleted)
            .OrderBy(testCase => testCase.CreatedAt)
            .ThenBy(testCase => testCase.Id)
            .ToList();

        if (testCases.Count == 0)
        {
            return Result<ProblemJudgeRevision>.Failure(NoActiveTestCasesMessage);
        }

        var collectionValidation = ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(
            problem.TimeLimitMs,
            testCases.Select(JudgeTestCasePayload.From).ToList(),
            resourcePolicy,
            requireAtLeastOne: true);
        if (collectionValidation.IsFailure)
        {
            return Result<ProblemJudgeRevision>.Failure(collectionValidation.ErrorMessage!);
        }

        foreach (var testCase in testCases)
        {
            var validation = ProblemJudgeDefinitionValidator.ValidateTestCase(
                problem,
                testCase.Input,
                testCase.ExpectedOutput,
                testCase.ArgumentsJson,
                testCase.ExpectedJson,
                testCase.Visibility,
                testCase.Score,
                resourcePolicy);
            if (validation.IsFailure)
            {
                return Result<ProblemJudgeRevision>.Failure($"Test case {testCase.Id} is invalid: {validation.ErrorMessage}");
            }
        }

        var storedAssets = await dbContext.ProblemJudgeAssets
            .AsNoTracking()
            .Where(asset => asset.ProblemId == problem.Id && !asset.IsDeleted)
            .ToListAsync(cancellationToken);
        var assets = ApplyTrackedChanges(
                storedAssets,
                dbContext.ChangeTracker.Entries<ProblemJudgeAsset>()
                    .Where(entry => entry.Entity.ProblemId == problem.Id)
                    .Select(entry => entry.Entity),
                asset => asset.Id)
            .Where(asset => !asset.IsDeleted)
            .OrderBy(asset => asset.Language)
            .ThenBy(asset => asset.OriginalFileName)
            .ThenBy(asset => asset.Id)
            .ToList();

        var latestRevisionNumber = await dbContext.ProblemJudgeRevisions
            .Where(revision => revision.ProblemId == problem.Id)
            .Select(revision => (int?)revision.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var revision = new ProblemJudgeRevision
        {
            Id = Guid.NewGuid(),
            ProblemId = problem.Id,
            RevisionNumber = checked(latestRevisionNumber + 1),
            JudgeMode = problem.JudgeMode,
            AllowedLanguagesMask = problem.AllowedLanguagesMask,
            FunctionSpecJson = problem.JudgeMode == Domain.Enums.JudgeMode.Function ? problem.FunctionSpecJson : null,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitMb = problem.MemoryLimitMb,
            CreatedAt = DateTimeOffset.UtcNow,
            TestCases = testCases.Select((testCase, index) => new ProblemJudgeRevisionTestCase
            {
                Id = Guid.NewGuid(),
                SourceTestCaseId = testCase.Id,
                Order = index,
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                ArgumentsJson = testCase.ArgumentsJson,
                ExpectedJson = testCase.ExpectedJson,
                Visibility = testCase.Visibility,
                Score = testCase.Score
            }).ToList(),
            Assets = assets.Select((asset, index) => new ProblemJudgeRevisionAsset
            {
                ProblemJudgeAssetId = asset.Id,
                Order = index
            }).ToList()
        };

        dbContext.ProblemJudgeRevisions.Add(revision);
        problem.CurrentJudgeRevisionId = revision.Id;
        problem.CurrentJudgeRevision = revision;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProblemJudgeRevision>.Success(revision);
    }

    private static IReadOnlyCollection<T> ApplyTrackedChanges<T>(IEnumerable<T> stored, IEnumerable<T> tracked, Func<T, Guid> getId)
    {
        var values = stored.ToDictionary(getId);
        foreach (var item in tracked)
        {
            values[getId(item)] = item;
        }

        return values.Values;
    }
}
