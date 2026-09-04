using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Judging;

namespace OnlineJudge.Tests.Judging;

public class JudgeResourcePolicyTests
{
    private static readonly JudgeResourcePolicy Policy = new()
    {
        MaxSourceCodeBytes = 4,
        MaxProblemTitleCharacters = 3,
        MaxProblemContentBytes = 4,
        MinTimeLimitMs = 100,
        MaxTimeLimitMs = 200,
        MaxDeclaredTestTimeBudgetMs = 300,
        MinMemoryLimitMb = 16,
        MaxMemoryLimitMb = 32,
        MaxTestCases = 2,
        MaxTestCaseFieldBytes = 4,
        MaxAggregateTestDataBytes = 8,
        MaxImportTestCases = 2,
        MaxImportPayloadBytes = 8,
        SubmissionJudgeWallTimeSeconds = 1
    };

    [Fact]
    public void SourceCode_UsesUtf8ByteCount()
    {
        Assert.True(ProblemJudgeDefinitionValidator.ValidateSourceCode("abcd", Policy).IsSuccess);
        Assert.True(ProblemJudgeDefinitionValidator.ValidateSourceCode("你好", Policy).IsFailure);
    }

    [Theory]
    [InlineData(99, 16, false)]
    [InlineData(100, 16, true)]
    [InlineData(200, 32, true)]
    [InlineData(201, 32, false)]
    [InlineData(100, 15, false)]
    [InlineData(100, 33, false)]
    public void RunLimits_EnforceInclusiveBounds(int timeLimitMs, int memoryLimitMb, bool succeeds)
    {
        Assert.Equal(succeeds, ProblemJudgeDefinitionValidator.ValidateRunLimits(timeLimitMs, memoryLimitMb, Policy).IsSuccess);
    }

    [Fact]
    public void TestCasePayload_RejectsMultibyteFieldAboveByteLimit()
    {
        var result = ProblemJudgeDefinitionValidator.ValidateTestCasePayload(
            new JudgeTestCasePayload("你好", string.Empty, null, null),
            Policy);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void TestCaseCollection_EnforcesCountTimeProductAndAggregateBytes()
    {
        var one = new JudgeTestCasePayload("aa", "bb", null, null);
        var two = new JudgeTestCasePayload("cc", "dd", null, null);

        Assert.True(ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(100, [one, two], Policy, true).IsSuccess);
        Assert.True(ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(200, [one, two], Policy, true).IsFailure);
        Assert.True(ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(100, [one, two, one], Policy, true).IsFailure);
        Assert.True(ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(100, [one, new JudgeTestCasePayload("ccc", "dd", null, null)], Policy, true).IsFailure);
    }

    [Fact]
    public void ProblemDefinition_EnforcesTitleAndContentBoundaries()
    {
        var valid = ProblemJudgeDefinitionValidator.ValidateProblem(
            "abc", "abcd", string.Empty, string.Empty, 100, 16,
            JudgeMode.StandardInputOutput, 0b001, null, null, Policy);
        var longTitle = ProblemJudgeDefinitionValidator.ValidateProblem(
            "abcd", string.Empty, string.Empty, string.Empty, 100, 16,
            JudgeMode.StandardInputOutput, 0b001, null, null, Policy);
        var multibyteContent = ProblemJudgeDefinitionValidator.ValidateProblem(
            "abc", "你好", string.Empty, string.Empty, 100, 16,
            JudgeMode.StandardInputOutput, 0b001, null, null, Policy);

        Assert.True(valid.IsSuccess);
        Assert.True(longTitle.IsFailure);
        Assert.True(multibyteContent.IsFailure);
    }

    [Fact]
    public void Configuration_RejectsInvalidPositiveValueAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{JudgeResourcePolicy.SectionName}:MaxTestCases"] = "0"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => JudgeResourcePolicyConfiguration.FromConfiguration(configuration));
    }

    [Fact]
    public void Configuration_RejectsInconsistentRangesAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{JudgeResourcePolicy.SectionName}:MinTimeLimitMs"] = "500",
                [$"{JudgeResourcePolicy.SectionName}:MaxTimeLimitMs"] = "100"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => JudgeResourcePolicyConfiguration.FromConfiguration(configuration));
    }
}
