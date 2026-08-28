using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Application.Leaderboards.Services;

public interface ILeaderboardScoringEngine
{
    int CalculateBonus(int baseScore, int percentage);

    LeaderboardPerformanceScore CalculatePerformance(
        int baseScore,
        LeaderboardScoringRules rules,
        LeaderboardPerformanceCandidate candidate,
        IReadOnlyCollection<LeaderboardProblemBenchmarkFact> benchmarks);

    bool IsBetterPerformance(LeaderboardPerformanceScore candidate, LeaderboardPerformanceScore? current);

    IReadOnlyList<LeaderboardCalculatedProblemScore> CalculateProblemScores(
        int baseScore,
        LeaderboardScoringRules rules,
        IReadOnlyCollection<LeaderboardProblemScoreFact> facts,
        IReadOnlyCollection<LeaderboardProblemBenchmarkFact> benchmarks);
}
