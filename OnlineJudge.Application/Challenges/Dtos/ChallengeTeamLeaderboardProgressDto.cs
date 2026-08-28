namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTeamLeaderboardProgressDto
{
    public Guid TeamParticipantId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public int CompletedTaskCount { get; set; }
    public int TotalScore { get; set; }
    public DateTimeOffset? LastImprovedAt { get; set; }
    public IReadOnlyList<Guid> CompletedTaskIds { get; set; } = [];
    public IReadOnlyDictionary<Guid, int> TaskScores { get; set; } = new Dictionary<Guid, int>();
}
