using OnlineJudge.Application.Challenges;

namespace OnlineJudge.Tests.Challenges;

public class ChallengeScoreCalculatorTests
{
    [Theory]
    [InlineData(300, 65, 100, 195)]
    [InlineData(400, 73, 100, 292)]
    [InlineData(300, 100, 100, 300)]
    [InlineData(300, 0, 100, 0)]
    [InlineData(300, 120, 100, 300)]
    [InlineData(300, 1, 6, 50)]
    public void CalculateEarnedScore_ReturnsExpectedScore(int taskScore, int passedScore, int totalScore, int expected)
    {
        Assert.Equal(expected, ChallengeScoreCalculator.CalculateEarnedScore(taskScore, passedScore, totalScore));
    }

    [Theory]
    [InlineData(300, 10, 0)]
    [InlineData(0, 10, 100)]
    [InlineData(-1, 10, 100)]
    public void CalculateEarnedScore_InvalidTotals_ReturnsZero(int taskScore, int passedScore, int totalScore)
    {
        Assert.Equal(0, ChallengeScoreCalculator.CalculateEarnedScore(taskScore, passedScore, totalScore));
    }
}
