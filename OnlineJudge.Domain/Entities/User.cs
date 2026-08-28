using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class User
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Stores the password hash only; never store the raw password.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public string? PhoneNumber { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public UserRole Role { get; set; }

    public bool IsBlacklisted { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Submission> Submissions { get; set; } = [];

    public List<ProblemCollaborator> ProblemCollaborations { get; set; } = [];

    public List<ProblemCollaborator> GrantedProblemCollaborations { get; set; } = [];

    public List<Challenge> CreatedChallenges { get; set; } = [];

    public List<ChallengeParticipant> ChallengeParticipants { get; set; } = [];

    public List<Team> OwnedTeams { get; set; } = [];

    public List<TeamMember> TeamMemberships { get; set; } = [];

    public List<TeamInvitation> TeamInvitations { get; set; } = [];

    public List<TeamInvitation> SentTeamInvitations { get; set; } = [];

    public List<TeamProject> CreatedTeamProjects { get; set; } = [];
}
