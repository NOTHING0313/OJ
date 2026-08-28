using System.Text.Json;
using OnlineJudge.Application.Leaderboards.Models;

namespace OnlineJudge.Infrastructure.Leaderboards;

public static class LeaderboardScoringRulesSerializer
{
    public const string DefaultRulesJson = "{\"timeBonusPercentages\":[20,16,13,10,8,6,5,4,3,2],\"runtimeBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":6},{\"maxRatioPercentage\":65,\"bonusPercentage\":5},{\"maxRatioPercentage\":80,\"bonusPercentage\":3},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}],\"memoryBonusTiers\":[{\"maxRatioPercentage\":50,\"bonusPercentage\":4},{\"maxRatioPercentage\":70,\"bonusPercentage\":3},{\"maxRatioPercentage\":85,\"bonusPercentage\":2},{\"maxRatioPercentage\":100,\"bonusPercentage\":1}]}";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(LeaderboardScoringRules rules)
    {
        Validate(rules);
        return JsonSerializer.Serialize(rules, JsonOptions);
    }

    public static LeaderboardScoringRules Deserialize(string? json)
    {
        var rules = string.IsNullOrWhiteSpace(json)
            ? JsonSerializer.Deserialize<LeaderboardScoringRules>(DefaultRulesJson, JsonOptions)!
            : JsonSerializer.Deserialize<LeaderboardScoringRules>(json, JsonOptions)
                ?? throw new InvalidOperationException("Leaderboard scoring rules are invalid.");
        Validate(rules);
        return rules;
    }

    public static void Validate(LeaderboardScoringRules rules)
    {
        if (rules.TimeBonusPercentages.Count != 10 || rules.TimeBonusPercentages.Any(value => value < 0))
        {
            throw new InvalidOperationException("Leaderboard time bonus rules must contain 10 non-negative percentages.");
        }

        ValidateTiers(rules.RuntimeBonusTiers, "runtime");
        ValidateTiers(rules.MemoryBonusTiers, "memory");
    }

    private static void ValidateTiers(IReadOnlyList<LeaderboardPerformanceBonusTier> tiers, string name)
    {
        if (tiers.Count == 0
            || tiers.Any(tier => tier.MaxRatioPercentage <= 0 || tier.BonusPercentage < 0)
            || tiers.Zip(tiers.Skip(1), (left, right) => left.MaxRatioPercentage >= right.MaxRatioPercentage).Any(invalid => invalid))
        {
            throw new InvalidOperationException($"Leaderboard {name} bonus tiers must use ascending positive ratios and non-negative percentages.");
        }
    }
}
