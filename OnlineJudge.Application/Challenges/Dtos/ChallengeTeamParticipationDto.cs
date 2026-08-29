namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeTeamParticipationDto
{
    public Guid? Id { get; set; }
    public Guid? TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTimeOffset RegisteredAt { get; set; }
    public int RosterMemberCount { get; set; }
    public bool IsRosterMember { get; set; }
    public bool CanRegisterTeam { get; set; }
    public Guid? SelectedTeamProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? RepositoryUrl { get; set; }
}
