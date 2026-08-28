using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Leaderboards.Models;

public sealed record LeaderboardProblemBenchmarkFact(JudgeLanguage Language, int RuntimeBaselineMs, int MemoryBaselineKb);

public sealed record LeaderboardPerformanceCandidate(
    Guid SubmissionId,
    JudgeLanguage Language,
    int? RuntimeMs,
    int? MemoryKb,
    DateTimeOffset FinishedAt);

public sealed record LeaderboardPerformanceScore(
    LeaderboardPerformanceCandidate Candidate,
    int? RuntimeBaselineMs,
    int? MemoryBaselineKb,
    int RuntimeBonus,
    int MemoryBonus)
{
    public int PerformanceBonus => RuntimeBonus + MemoryBonus;
}

public sealed record LeaderboardProblemScoreFact(
    Guid ScoreId,
    Guid UserId,
    int EarnedBaseScore,
    DateTimeOffset FirstFullScoreAt,
    Guid? FirstFullSubmissionId,
    LeaderboardPerformanceCandidate? BestPerformance,
    DateTimeOffset LastScoreImprovedAt);

public sealed record LeaderboardCalculatedProblemScore(
    Guid ScoreId,
    Guid UserId,
    int BaseScore,
    int EarnedBaseScore,
    int? TimeRank,
    int TimeBonus,
    LeaderboardPerformanceScore? Performance,
    DateTimeOffset FirstFullScoreAt,
    DateTimeOffset LastScoreImprovedAt)
{
    public int RuntimeBonus => Performance?.RuntimeBonus ?? 0;

    public int MemoryBonus => Performance?.MemoryBonus ?? 0;

    public int PerformanceBonus => RuntimeBonus + MemoryBonus;

    public int TotalProblemScore => EarnedBaseScore + TimeBonus + PerformanceBonus;
}
