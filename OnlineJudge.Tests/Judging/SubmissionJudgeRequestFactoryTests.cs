using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging;

namespace OnlineJudge.Tests.Judging;

public class SubmissionJudgeRequestFactoryTests
{
    [Fact]
    public void Create_UsesBoundRevisionFieldsAndStableTestCaseOrder()
    {
        var problemId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var firstSourceId = Guid.NewGuid();
        var secondSourceId = Guid.NewGuid();
        var revision = new ProblemJudgeRevision
        {
            Id = revisionId,
            ProblemId = problemId,
            JudgeMode = JudgeMode.Function,
            FunctionSpecJson = "{\"functionName\":\"solve\"}",
            TimeLimitMs = 2100,
            MemoryLimitMb = 384,
            TestCases =
            [
                RevisionCase(secondSourceId, order: 1, score: 60),
                RevisionCase(firstSourceId, order: 0, score: 40)
            ]
        };
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            ProblemJudgeRevisionId = revisionId,
            ProblemJudgeRevision = revision,
            Language = JudgeLanguage.CSharp,
            SourceCode = "public class Solution {}",
            ChallengeTaskId = Guid.NewGuid(),
            Problem = new Problem
            {
                Id = problemId,
                JudgeMode = JudgeMode.StandardInputOutput,
                TimeLimitMs = 1,
                MemoryLimitMb = 1
            }
        };
        var assets = new[] { new JudgeCompileAsset { FileName = "Helper.cs", Content = "class Helper {}" } };

        var result = SubmissionJudgeRequestFactory.Create(submission, assets);

        Assert.True(result.IsSuccess);
        var request = result.Value!;
        Assert.Equal(JudgeMode.Function, request.JudgeMode);
        Assert.Equal(2100, request.TimeLimitMs);
        Assert.Equal(384, request.MemoryLimitMb);
        Assert.True(request.CollectAllCaseResults);
        Assert.Same(assets, request.CompileAssets);
        Assert.Equal([firstSourceId, secondSourceId], request.TestCases.Select(testCase => testCase.TestCaseId));
        Assert.Equal([40, 60], request.TestCases.Select(testCase => testCase.Score));
    }

    [Fact]
    public void Create_RejectsMissingMismatchedOrEmptyRevision()
    {
        var problemId = Guid.NewGuid();
        var submission = new Submission { ProblemId = problemId };
        Assert.Equal("Problem judge revision not found for submission.", SubmissionJudgeRequestFactory.Create(submission, []).ErrorMessage);

        submission.ProblemJudgeRevisionId = Guid.NewGuid();
        submission.ProblemJudgeRevision = new ProblemJudgeRevision { Id = submission.ProblemJudgeRevisionId.Value, ProblemId = Guid.NewGuid() };
        Assert.Equal("Problem judge revision not found for submission.", SubmissionJudgeRequestFactory.Create(submission, []).ErrorMessage);

        submission.ProblemJudgeRevision.ProblemId = problemId;
        Assert.Equal("Problem judge revision contains no test cases.", SubmissionJudgeRequestFactory.Create(submission, []).ErrorMessage);
    }

    private static ProblemJudgeRevisionTestCase RevisionCase(Guid sourceTestCaseId, int order, int score) => new()
    {
        Id = Guid.NewGuid(),
        SourceTestCaseId = sourceTestCaseId,
        Order = order,
        Input = order.ToString(),
        ExpectedOutput = order.ToString(),
        Visibility = TestCaseVisibility.Hidden,
        Score = score
    };
}
