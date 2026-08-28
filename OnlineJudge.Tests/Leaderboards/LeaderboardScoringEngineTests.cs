using OnlineJudge.Application.Leaderboards.Models;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Leaderboards;

namespace OnlineJudge.Tests.Leaderboards;

public sealed class LeaderboardScoringEngineTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-28T12:00:00Z");
    private readonly LeaderboardScoringEngine engine = new();
    private readonly LeaderboardScoringRules rules = new();

    [Theory]
    [InlineData(100, 20, 20)]
    [InlineData(250, 20, 50)]
    [InlineData(75, 13, 10)]
    [InlineData(25, 2, 1)]
    public void CalculateBonus_UsesAwayFromZeroRounding(int baseScore, int percentage, int expected) =>
        Assert.Equal(expected, engine.CalculateBonus(baseScore, percentage));

    [Theory]
    [InlineData(50, 6)]
    [InlineData(65, 5)]
    [InlineData(80, 3)]
    [InlineData(100, 1)]
    [InlineData(101, 0)]
    public void RuntimeTiers_UseInclusiveBoundaries(int actual, int expected) =>
        Assert.Equal(expected, Performance(actual, 999).RuntimeBonus);

    [Theory]
    [InlineData(50, 4)]
    [InlineData(70, 3)]
    [InlineData(85, 2)]
    [InlineData(100, 1)]
    [InlineData(101, 0)]
    public void MemoryTiers_UseInclusiveBoundaries(int actual, int expected) =>
        Assert.Equal(expected, Performance(999, actual).MemoryBonus);

    [Theory]
    [InlineData(null, 50, 0, 4)]
    [InlineData(50, null, 6, 0)]
    [InlineData(null, null, 0, 0)]
    public void MissingTelemetry_OnlyRemovesCorrespondingBonus(int? runtime, int? memory, int expectedRuntime, int expectedMemory)
    {
        var score = Performance(runtime, memory);
        Assert.Equal(expectedRuntime, score.RuntimeBonus);
        Assert.Equal(expectedMemory, score.MemoryBonus);
    }

    [Fact]
    public void MissingLanguageBenchmark_ReturnsZeroPerformance()
    {
        var candidate = Candidate(JudgeLanguage.CSharp, 10, 10);
        var score = engine.CalculatePerformance(100, rules, candidate, [Benchmark(JudgeLanguage.Cpp17)]);
        Assert.Equal(0, score.PerformanceBonus);
        Assert.Null(score.RuntimeBaselineMs);
        Assert.Null(score.MemoryBaselineKb);
    }

    [Theory]
    [InlineData(JudgeLanguage.Cpp17)]
    [InlineData(JudgeLanguage.C11)]
    [InlineData(JudgeLanguage.CSharp)]
    public void Performance_UsesMatchingLanguageBenchmark(JudgeLanguage language)
    {
        var score = engine.CalculatePerformance(100, rules, Candidate(language, 50, 50),
        [
            new(JudgeLanguage.Cpp17, language == JudgeLanguage.Cpp17 ? 100 : 10, language == JudgeLanguage.Cpp17 ? 100 : 10),
            new(JudgeLanguage.C11, language == JudgeLanguage.C11 ? 100 : 10, language == JudgeLanguage.C11 ? 100 : 10),
            new(JudgeLanguage.CSharp, language == JudgeLanguage.CSharp ? 100 : 10, language == JudgeLanguage.CSharp ? 100 : 10)
        ]);
        Assert.Equal(10, score.PerformanceBonus);
    }

    [Fact]
    public void BetterPerformance_SelectsOneWholeSubmission()
    {
        var runtimeWinner = engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, 50, 101), [Benchmark(JudgeLanguage.Cpp17)]);
        var combinedWinner = engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, 80, 50), [Benchmark(JudgeLanguage.Cpp17)]);
        Assert.Equal(6, runtimeWinner.PerformanceBonus);
        Assert.Equal(7, combinedWinner.PerformanceBonus);
        Assert.True(engine.IsBetterPerformance(combinedWinner, runtimeWinner));
        Assert.Equal(3, combinedWinner.RuntimeBonus);
        Assert.Equal(4, combinedWinner.MemoryBonus);
    }

    [Fact]
    public void PerformanceTieBreak_PrefersRuntimeThenMemoryThenFacts()
    {
        var first = engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, 65, 100, Now.AddSeconds(1), Guid.Parse("00000000-0000-0000-0000-000000000002")), [Benchmark(JudgeLanguage.Cpp17)]);
        var runtimePreferred = engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, 50, 101), [Benchmark(JudgeLanguage.Cpp17)]);
        Assert.Equal(first.PerformanceBonus, runtimePreferred.PerformanceBonus);
        Assert.True(engine.IsBetterPerformance(runtimePreferred, first));

        var earlierId = engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, 50, 101, Now, Guid.Parse("00000000-0000-0000-0000-000000000001")), [Benchmark(JudgeLanguage.Cpp17)]);
        Assert.True(engine.IsBetterPerformance(earlierId, runtimePreferred));
    }

    [Fact]
    public void CalculateProblemScores_AssignsDynamicTop10AndMaximum130()
    {
        var facts = Enumerable.Range(1, 11).Select(index => new LeaderboardProblemScoreFact(
            Guid.NewGuid(), Guid.NewGuid(), 100, Now.AddSeconds(index), Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
            index == 1 ? Candidate(JudgeLanguage.Cpp17, 50, 50) : null, Now.AddSeconds(index))).ToList();
        var scores = engine.CalculateProblemScores(100, rules, facts, [Benchmark(JudgeLanguage.Cpp17)]).OrderBy(item => item.FirstFullScoreAt).ToList();

        Assert.Equal(130, scores[0].TotalProblemScore);
        Assert.Equal(20, scores[0].TimeBonus);
        Assert.Equal(2, scores[9].TimeBonus);
        Assert.Null(scores[10].TimeRank);
        Assert.Equal(0, scores[10].TimeBonus);
    }

    [Fact]
    public void CalculateProblemScores_UsesSubmissionIdForEqualTimestamp()
    {
        var laterId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var earlierId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var laterScoreId = Guid.NewGuid();
        var earlierScoreId = Guid.NewGuid();
        var scores = engine.CalculateProblemScores(100, rules,
        [
            new(laterScoreId, Guid.NewGuid(), 100, Now, laterId, null, Now),
            new(earlierScoreId, Guid.NewGuid(), 100, Now, earlierId, null, Now)
        ], []);
        Assert.Equal(1, scores.Single(item => item.ScoreId == earlierScoreId).TimeRank);
        Assert.Equal(20, scores.Single(item => item.ScoreId == earlierScoreId).TimeBonus);
        Assert.Equal(2, scores.Single(item => item.ScoreId == laterScoreId).TimeRank);
    }

    private LeaderboardPerformanceScore Performance(int? runtime, int? memory) =>
        engine.CalculatePerformance(100, rules, Candidate(JudgeLanguage.Cpp17, runtime, memory), [Benchmark(JudgeLanguage.Cpp17)]);

    private static LeaderboardProblemBenchmarkFact Benchmark(JudgeLanguage language) => new(language, 100, 100);

    private static LeaderboardPerformanceCandidate Candidate(
        JudgeLanguage language,
        int? runtime,
        int? memory,
        DateTimeOffset? finishedAt = null,
        Guid? id = null) => new(id ?? Guid.NewGuid(), language, runtime, memory, finishedAt ?? Now);
}
