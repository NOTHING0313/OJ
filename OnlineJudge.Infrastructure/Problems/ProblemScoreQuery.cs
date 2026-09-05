using Microsoft.EntityFrameworkCore;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Problems;

internal static class ProblemScoreQuery
{
    public static async Task<IReadOnlyDictionary<Guid, int>> GetTotalsAsync(
        OnlineJudgeDbContext dbContext,
        IEnumerable<Guid> problemIds,
        CancellationToken cancellationToken)
    {
        var ids = problemIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var programmingScores = await dbContext.TestCases.AsNoTracking()
            .Where(ProblemScoreCalculator.ActiveTestCasePredicate)
            .Where(testCase => ids.Contains(testCase.ProblemId))
            .GroupBy(testCase => testCase.ProblemId)
            .Select(group => new
            {
                ProblemId = group.Key,
                TotalScore = group.AsQueryable().Sum(ProblemScoreCalculator.ScoreSelector)
            })
            .ToDictionaryAsync(item => item.ProblemId, item => item.TotalScore, cancellationToken);
        var choiceScores = await dbContext.ProblemChoiceQuestions.AsNoTracking()
            .Where(question => !question.IsDeleted && ids.Contains(question.ProblemId))
            .GroupBy(question => question.ProblemId)
            .Select(group => new { ProblemId = group.Key, TotalScore = group.Sum(question => question.Score) })
            .ToDictionaryAsync(item => item.ProblemId, item => item.TotalScore, cancellationToken);
        foreach (var score in choiceScores) programmingScores[score.Key] = score.Value;
        return programmingScores;
    }
}
