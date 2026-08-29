namespace OnlineJudge.Application.Teams.Dtos;

public class TeamChallengeAnnouncementDto
{
    public Guid ChallengeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
}
