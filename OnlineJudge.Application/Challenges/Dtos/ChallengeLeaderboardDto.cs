namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeLeaderboardDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public int TotalTaskCount { get; set; }

    public IReadOnlyList<ChallengeLeaderboardEntryDto> Entries { get; set; } = [];
}
