using System.Linq.Expressions;

namespace OnlineJudge.Domain.Entities;

/// <summary>
/// Defines the canonical score source for a problem.
/// </summary>
public static class ProblemScoreCalculator
{
    public static Expression<Func<TestCase, bool>> ActiveTestCasePredicate { get; } = testCase => !testCase.IsDeleted;

    public static Expression<Func<TestCase, int>> ScoreSelector { get; } = testCase => testCase.Score;

    public static int Calculate(IEnumerable<TestCase> testCases) => testCases
        .AsQueryable()
        .Where(ActiveTestCasePredicate)
        .Sum(ScoreSelector);
}
