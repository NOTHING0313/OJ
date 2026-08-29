namespace OnlineJudge.Application.Leaderboards.Models;

public sealed class LeaderboardScoringRules
{
    public bool FirstCompletionBonusEnabled { get; set; } = true;

    public bool RuntimeBonusEnabled { get; set; } = true;

    public bool MemoryBonusEnabled { get; set; } = true;

    public List<int> TimeBonusPercentages { get; set; } = [20, 16, 13, 10, 8, 6, 5, 4, 3, 2];

    public List<LeaderboardPerformanceBonusTier> RuntimeBonusTiers { get; set; } =
    [
        new(50, 6),
        new(65, 5),
        new(80, 3),
        new(100, 1)
    ];

    public List<LeaderboardPerformanceBonusTier> MemoryBonusTiers { get; set; } =
    [
        new(50, 4),
        new(70, 3),
        new(85, 2),
        new(100, 1)
    ];
}

public sealed record LeaderboardPerformanceBonusTier(int MaxRatioPercentage, int BonusPercentage);
