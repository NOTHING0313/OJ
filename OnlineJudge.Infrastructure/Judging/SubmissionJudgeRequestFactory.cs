using OnlineJudge.Application.Common;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Judging;

/// <summary>
/// Builds the canonical runner request from the immutable revision bound to a submission.
/// </summary>
public static class SubmissionJudgeRequestFactory
{
    public static Result<JudgeRequest> Create(Submission submission, IReadOnlyList<JudgeCompileAsset> compileAssets)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(compileAssets);

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

        return Result<JudgeRequest>.Success(new JudgeRequest
        {
            SubmissionId = submission.Id,
            ProblemId = submission.ProblemId,
            Language = submission.Language,
            JudgeMode = revision.JudgeMode,
            SourceCode = submission.SourceCode,
            FunctionSpecJson = revision.FunctionSpecJson,
            TimeLimitMs = revision.TimeLimitMs,
            MemoryLimitMb = revision.MemoryLimitMb,
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
