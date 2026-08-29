using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Application.Leaderboards.Services;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardScoringEngine : ILeaderboardScoringEngine
{
    public int CalculateBonus(int baseScore, int percentage) => checked((int)Math.Round(
        baseScore * (decimal)percentage / 100m,
        MidpointRounding.AwayFromZero));

    public LeaderboardPerformanceScore CalculatePerformance(
        int baseScore,
        LeaderboardScoringRules rules,
        LeaderboardPerformanceCandidate candidate,
        IReadOnlyCollection<LeaderboardProblemBenchmarkFact> benchmarks)
    {
        var benchmark = benchmarks.FirstOrDefault(item => item.Language == candidate.Language);
        if (benchmark is null)
        {
            return new LeaderboardPerformanceScore(candidate, null, null, 0, 0);
        }

        var runtimePercentage = rules.RuntimeBonusEnabled ? MatchTier(candidate.RuntimeMs, benchmark.RuntimeBaselineMs, rules.RuntimeBonusTiers) : 0;
        var memoryPercentage = rules.MemoryBonusEnabled ? MatchTier(candidate.MemoryKb, benchmark.MemoryBaselineKb, rules.MemoryBonusTiers) : 0;
        return new LeaderboardPerformanceScore(
            candidate,
            benchmark.RuntimeBaselineMs,
            benchmark.MemoryBaselineKb,
            CalculateBonus(baseScore, runtimePercentage),
            CalculateBonus(baseScore, memoryPercentage));
    }

    public bool IsBetterPerformance(LeaderboardPerformanceScore candidate, LeaderboardPerformanceScore? current)
    {
        if (current is null) return true;

        return CompareDescending(candidate.PerformanceBonus, current.PerformanceBonus)
            ?? CompareDescending(candidate.RuntimeBonus, current.RuntimeBonus)
            ?? CompareDescending(candidate.MemoryBonus, current.MemoryBonus)
            ?? CompareAscending(candidate.Candidate.RuntimeMs, current.Candidate.RuntimeMs)
            ?? CompareAscending(candidate.Candidate.MemoryKb, current.Candidate.MemoryKb)
            ?? CompareAscending(candidate.Candidate.FinishedAt, current.Candidate.FinishedAt)
            ?? candidate.Candidate.SubmissionId.CompareTo(current.Candidate.SubmissionId) < 0;
    }

    public IReadOnlyList<LeaderboardCalculatedProblemScore> CalculateProblemScores(
        int baseScore,
        LeaderboardScoringRules rules,
        IReadOnlyCollection<LeaderboardProblemScoreFact> facts,
        IReadOnlyCollection<LeaderboardProblemBenchmarkFact> benchmarks)
    {
        var timeRanks = facts.OrderBy(fact => fact.FirstFullScoreAt)
            .ThenBy(fact => fact.FirstFullSubmissionId ?? fact.ScoreId)
            .Select((fact, index) => new { fact.ScoreId, Rank = index + 1 })
            .ToDictionary(item => item.ScoreId, item => item.Rank);

        return facts.Select(fact =>
        {
            var timeRank = timeRanks[fact.ScoreId];
            var timePercentage = rules.FirstCompletionBonusEnabled && timeRank <= rules.TimeBonusPercentages.Count
                ? rules.TimeBonusPercentages[timeRank - 1]
                : 0;
            var performance = fact.BestPerformance is null
                ? null
                : CalculatePerformance(baseScore, rules, fact.BestPerformance, benchmarks);
            return new LeaderboardCalculatedProblemScore(
                fact.ScoreId,
                fact.UserId,
                baseScore,
                fact.EarnedBaseScore,
                rules.FirstCompletionBonusEnabled && timeRank <= rules.TimeBonusPercentages.Count ? timeRank : null,
                CalculateBonus(baseScore, timePercentage),
                performance,
                fact.FirstFullScoreAt,
                fact.LastScoreImprovedAt);
        }).ToList();
    }

    private static int MatchTier(int? actual, int baseline, IReadOnlyList<LeaderboardPerformanceBonusTier> tiers)
    {
        if (!actual.HasValue || actual.Value < 0 || baseline <= 0) return 0;
        var tier = tiers.FirstOrDefault(item => (long)actual.Value * 100 <= (long)baseline * item.MaxRatioPercentage);
        return tier?.BonusPercentage ?? 0;
    }

    private static bool? CompareDescending(int left, int right) => left == right ? null : left > right;

    private static bool? CompareAscending(int? left, int? right)
    {
        var leftValue = left ?? int.MaxValue;
        var rightValue = right ?? int.MaxValue;
        return leftValue == rightValue ? null : leftValue < rightValue;
    }

    private static bool? CompareAscending(DateTimeOffset left, DateTimeOffset right) => left == right ? null : left < right;
}
