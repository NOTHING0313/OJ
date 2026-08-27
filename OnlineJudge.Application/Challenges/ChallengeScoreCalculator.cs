namespace OnlineJudge.Application.Challenges;

public static class ChallengeScoreCalculator
{
    public static int CalculateEarnedScore(int taskScore, int passedTestCaseScore, int totalTestCaseScore)
    {
        if (taskScore <= 0 || totalTestCaseScore <= 0 || passedTestCaseScore <= 0) return 0;

        var boundedPassedScore = Math.Min(passedTestCaseScore, totalTestCaseScore);
        var earned = (decimal)taskScore * boundedPassedScore / totalTestCaseScore;
        return Math.Clamp((int)Math.Round(earned, MidpointRounding.AwayFromZero), 0, taskScore);
    }
}
