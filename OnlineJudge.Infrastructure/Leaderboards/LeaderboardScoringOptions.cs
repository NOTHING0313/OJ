using Microsoft.Extensions.Configuration;
using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Infrastructure.Leaderboards;

public sealed class LeaderboardScoringOptions
{
    public const string SectionName = "LeaderboardScoring";

    public List<int> TimeBonusPercentages { get; set; } = [20, 16, 13, 10, 8, 6, 5, 4, 3, 2];

    public List<LeaderboardPerformanceBonusTier> RuntimeBonusTiers { get; set; } =
    [
        new(50, 6), new(65, 5), new(80, 3), new(100, 1)
    ];

    public List<LeaderboardPerformanceBonusTier> MemoryBonusTiers { get; set; } =
    [
        new(50, 4), new(70, 3), new(85, 2), new(100, 1)
    ];

    public LeaderboardScoringRules CreateSnapshot() => new()
    {
        TimeBonusPercentages = [.. TimeBonusPercentages],
        RuntimeBonusTiers = RuntimeBonusTiers.Select(tier => new LeaderboardPerformanceBonusTier(tier.MaxRatioPercentage, tier.BonusPercentage)).ToList(),
        MemoryBonusTiers = MemoryBonusTiers.Select(tier => new LeaderboardPerformanceBonusTier(tier.MaxRatioPercentage, tier.BonusPercentage)).ToList()
    };

    public static LeaderboardScoringOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new LeaderboardScoringOptions();
        var section = configuration.GetSection(SectionName);
        var timePercentages = section.GetSection(nameof(TimeBonusPercentages)).GetChildren()
            .Select(item => int.TryParse(item.Value, out var value) ? value : -1)
            .ToList();
        var runtimeTiers = ReadTiers(section.GetSection(nameof(RuntimeBonusTiers)));
        var memoryTiers = ReadTiers(section.GetSection(nameof(MemoryBonusTiers)));

        if (timePercentages.Count > 0) options.TimeBonusPercentages = timePercentages;
        if (runtimeTiers.Count > 0) options.RuntimeBonusTiers = runtimeTiers;
        if (memoryTiers.Count > 0) options.MemoryBonusTiers = memoryTiers;
        LeaderboardScoringRulesSerializer.Validate(options.CreateSnapshot());
        return options;
    }

    private static List<LeaderboardPerformanceBonusTier> ReadTiers(IConfigurationSection section) => section.GetChildren()
        .Select(item => new LeaderboardPerformanceBonusTier(
            int.TryParse(item[nameof(LeaderboardPerformanceBonusTier.MaxRatioPercentage)], out var ratio) ? ratio : -1,
            int.TryParse(item[nameof(LeaderboardPerformanceBonusTier.BonusPercentage)], out var bonus) ? bonus : -1))
        .ToList();
}
