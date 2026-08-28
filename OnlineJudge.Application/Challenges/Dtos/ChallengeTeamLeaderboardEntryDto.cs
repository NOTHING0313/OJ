namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTeamLeaderboardEntryDto
{
    public int Rank { get; set; }
    public Guid TeamParticipantId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int CompletedTaskCount { get; set; }
    public int TotalScore { get; set; }
    public DateTimeOffset? LastImprovedAt { get; set; }
}
