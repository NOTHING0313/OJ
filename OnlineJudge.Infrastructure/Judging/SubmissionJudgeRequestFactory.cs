using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Problems;

namespace OnlineJudge.Infrastructure.Judging;

/// <summary>
/// Builds the canonical runner request from the immutable revision bound to a submission.
/// </summary>
public static class SubmissionJudgeRequestFactory
{
    public static Result<JudgeRequest> Create(Submission submission, IReadOnlyList<JudgeCompileAsset> compileAssets)
    {
        return Create(submission, compileAssets, JudgeResourcePolicy.Default);
    }

    public static Result<JudgeRequest> Create(
        Submission submission,
        IReadOnlyList<JudgeCompileAsset> compileAssets,
        JudgeResourcePolicy resourcePolicy)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(compileAssets);
        ArgumentNullException.ThrowIfNull(resourcePolicy);

        var revision = submission.ProblemJudgeRevision;
        if (revision is null
            || submission.ProblemJudgeRevisionId is null
            || revision.Id != submission.ProblemJudgeRevisionId.Value
            || revision.ProblemId != submission.ProblemId)
        {
            return Result<JudgeRequest>.Failure("Problem judge revision not found for submission.");
        }

        if (revision.TestCases.Count == 0)
        {
            return Result<JudgeRequest>.Failure("Problem judge revision contains no test cases.");
        }

        if (submission.SubmissionKind != Domain.Enums.SubmissionKind.Code
            || revision.ProblemKind != Domain.Enums.ProblemKind.Programming
            || submission.Language is null
            || string.IsNullOrWhiteSpace(submission.SourceCode)
            || revision.JudgeMode is null
            || revision.TimeLimitMs is null
            || revision.MemoryLimitMb is null)
        {
            return Result<JudgeRequest>.Failure("Programming judge configuration is incomplete.");
        }

        var sourceValidation = ProblemJudgeDefinitionValidator.ValidateSourceCode(submission.SourceCode, resourcePolicy);
        if (sourceValidation.IsFailure)
        {
            return Result<JudgeRequest>.Failure(sourceValidation.ErrorMessage!);
        }

        var resourceValidation = ProblemJudgeDefinitionValidator.ValidateRunLimits(revision.TimeLimitMs.Value, revision.MemoryLimitMb.Value, resourcePolicy);
        if (resourceValidation.IsFailure)
        {
            return Result<JudgeRequest>.Failure(resourceValidation.ErrorMessage!);
        }

        var payloads = revision.TestCases
            .Select(testCase => new JudgeTestCasePayload(testCase.Input, testCase.ExpectedOutput, testCase.ArgumentsJson, testCase.ExpectedJson))
            .ToList();
        foreach (var payload in payloads)
        {
            var payloadValidation = ProblemJudgeDefinitionValidator.ValidateTestCasePayload(payload, resourcePolicy);
            if (payloadValidation.IsFailure)
            {
                return Result<JudgeRequest>.Failure(payloadValidation.ErrorMessage!);
            }
        }

        var collectionValidation = ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(
            revision.TimeLimitMs.Value,
            payloads,
            resourcePolicy,
            requireAtLeastOne: true);
        if (collectionValidation.IsFailure)
        {
            return Result<JudgeRequest>.Failure(collectionValidation.ErrorMessage!);
        }

        return Result<JudgeRequest>.Success(new JudgeRequest
        {
            SubmissionId = submission.Id,
            ProblemId = submission.ProblemId,
            Language = submission.Language.Value,
            JudgeMode = revision.JudgeMode.Value,
            SourceCode = submission.SourceCode,
            FunctionSpecJson = revision.FunctionSpecJson,
            TimeLimitMs = revision.TimeLimitMs.Value,
            MemoryLimitMb = revision.MemoryLimitMb.Value,
            CollectAllCaseResults = submission.ChallengeTaskId.HasValue,
            CompileAssets = compileAssets,
            TestCases = revision.TestCases
                .OrderBy(testCase => testCase.Order)
                .Select(testCase => new JudgeCaseRequest
                {
                    TestCaseId = testCase.SourceTestCaseId,
                    Input = testCase.Input,
                    ExpectedOutput = testCase.ExpectedOutput,
                    ArgumentsJson = testCase.ArgumentsJson,
                    ExpectedJson = testCase.ExpectedJson,
                    Visibility = testCase.Visibility,
                    Score = testCase.Score
                })
                .ToList()
        });
    }
}
