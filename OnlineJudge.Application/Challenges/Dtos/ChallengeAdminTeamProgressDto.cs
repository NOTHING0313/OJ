namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeAdminTeamProgressDto
{
    public Guid TeamParticipantId { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid RegisteredByUserId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public int TotalScore { get; set; }
    public int CompletedTaskCount { get; set; }
    public IReadOnlyList<ChallengeAdminTeamRosterMemberDto> Roster { get; set; } = [];
    public IReadOnlyList<ChallengeAdminTeamTaskStatusDto> Tasks { get; set; } = [];
}

public class ChallengeAdminTeamRosterMemberDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Role { get; set; }
}

public class ChallengeAdminTeamTaskStatusDto
{
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int Score { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? BestSubmissionId { get; set; }
    public Guid? ContributorUserId { get; set; }
    public string? ContributorUserName { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
