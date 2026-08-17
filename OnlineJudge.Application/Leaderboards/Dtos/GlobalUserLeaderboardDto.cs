namespace OnlineJudge.Application.Leaderboards.Dtos;

public class GlobalUserLeaderboardDto
{
    public IReadOnlyList<GlobalUserLeaderboardEntryDto> Entries { get; set; } = [];
}
