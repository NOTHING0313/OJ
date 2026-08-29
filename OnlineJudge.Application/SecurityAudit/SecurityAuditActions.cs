namespace OnlineJudge.Application.SecurityAudit;

public static class SecurityAuditActions
{
    public const string UserRoleChanged = "User.RoleChanged";
    public const string UserBlacklisted = "User.Blacklisted";
    public const string UserUnblacklisted = "User.Unblacklisted";
    public const string UserDeleted = "User.Deleted";
    public const string UserPasswordReset = "User.PasswordReset";
    public const string ProblemCreated = "Problem.Created";
    public const string ProblemUpdated = "Problem.Updated";
    public const string ProblemDeleted = "Problem.Deleted";
    public const string ProblemTestCasesChanged = "Problem.TestCasesChanged";
    public const string ChallengeCreated = "Challenge.Created";
    public const string ChallengeUpdated = "Challenge.Updated";
    public const string ChallengeDeleted = "Challenge.Deleted";
    public const string SeasonCreated = "Season.Created";
    public const string SeasonUpdated = "Season.Updated";
    public const string SeasonActivated = "Season.Activated";
    public const string SeasonFrozen = "Season.Frozen";
    public const string SeasonPublished = "Season.Published";
    public const string SeasonArchived = "Season.Archived";
    public const string HelpCreated = "Help.Created";
    public const string HelpUpdated = "Help.Updated";
    public const string HelpPublished = "Help.Published";
    public const string HelpUnpublished = "Help.Unpublished";
    public const string HelpDeleted = "Help.Deleted";
    public const string TeamGitSyncRequested = "TeamGit.SyncRequested";
    public const string TeamGitSyncSucceeded = "TeamGit.SyncSucceeded";
    public const string TeamGitSyncFailed = "TeamGit.SyncFailed";
    public const string SiteAppearanceUpdated = "SiteAppearance.Updated";
}
