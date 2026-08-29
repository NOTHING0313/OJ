namespace OnlineJudge.Application.Leaderboards.Requests;

public class CreateLeaderboardSeasonRequest
{
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset FreezeAt { get; set; }

    public DateTimeOffset PublicUntil { get; set; }

    public bool IncludeGlobalBoard { get; set; } = true;

    public List<Guid> ChallengeIds { get; set; } = [];

    public bool FirstCompletionBonusEnabled { get; set; } = true;

    public bool RuntimeBonusEnabled { get; set; } = true;

    public bool MemoryBonusEnabled { get; set; } = true;
}
