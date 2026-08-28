namespace OnlineJudge.Application.Challenges.Dtos;

using OnlineJudge.Domain.Enums;

public class ChallengeLeaderboardDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public int TotalTaskCount { get; set; }

    public ChallengeParticipationMode ParticipationMode { get; set; }

    public IReadOnlyList<ChallengeLeaderboardEntryDto> Entries { get; set; } = [];

    public IReadOnlyList<ChallengeTeamLeaderboardEntryDto> TeamEntries { get; set; } = [];
}
