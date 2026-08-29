namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengePeerReviewWorkspaceDto
{
    public bool AssignmentReady { get; set; }
    public bool InsufficientTeams { get; set; }
    public bool IsExpired { get; set; }
    public bool CanEdit { get; set; }
    public DateTimeOffset? PeerReviewEndAt { get; set; }
    public string? TargetTeamName { get; set; }
    public string? TargetProjectName { get; set; }
    public string? TargetRepositoryUrl { get; set; }
    public ChallengePeerReviewDto? Review { get; set; }
}
