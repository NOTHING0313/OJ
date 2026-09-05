using Microsoft.EntityFrameworkCore;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence;

public class OnlineJudgeDbContext(DbContextOptions<OnlineJudgeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Problem> Problems => Set<Problem>();

    public DbSet<TestCase> TestCases => Set<TestCase>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<JudgeJob> JudgeJobs => Set<JudgeJob>();

    public DbSet<SubmissionCaseResult> SubmissionCaseResults => Set<SubmissionCaseResult>();

    public DbSet<ProblemCollaborator> ProblemCollaborators => Set<ProblemCollaborator>();

    public DbSet<ProblemJudgeAsset> ProblemJudgeAssets => Set<ProblemJudgeAsset>();

    public DbSet<ProblemJudgeRevision> ProblemJudgeRevisions => Set<ProblemJudgeRevision>();

    public DbSet<ProblemJudgeRevisionTestCase> ProblemJudgeRevisionTestCases => Set<ProblemJudgeRevisionTestCase>();

    public DbSet<ProblemJudgeRevisionAsset> ProblemJudgeRevisionAssets => Set<ProblemJudgeRevisionAsset>();

    public DbSet<ProblemChoiceQuestion> ProblemChoiceQuestions => Set<ProblemChoiceQuestion>();

    public DbSet<ProblemChoiceOption> ProblemChoiceOptions => Set<ProblemChoiceOption>();

    public DbSet<ProblemJudgeRevisionChoiceQuestion> ProblemJudgeRevisionChoiceQuestions => Set<ProblemJudgeRevisionChoiceQuestion>();

    public DbSet<ProblemJudgeRevisionChoiceOption> ProblemJudgeRevisionChoiceOptions => Set<ProblemJudgeRevisionChoiceOption>();

    public DbSet<SubmissionChoiceQuestionResult> SubmissionChoiceQuestionResults => Set<SubmissionChoiceQuestionResult>();

    public DbSet<SubmissionChoiceSelection> SubmissionChoiceSelections => Set<SubmissionChoiceSelection>();

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public DbSet<ChallengeTask> ChallengeTasks => Set<ChallengeTask>();

    public DbSet<ChallengeParticipant> ChallengeParticipants => Set<ChallengeParticipant>();

    public DbSet<ChallengeTeamParticipant> ChallengeTeamParticipants => Set<ChallengeTeamParticipant>();

    public DbSet<ChallengeTeamRosterMember> ChallengeTeamRosterMembers => Set<ChallengeTeamRosterMember>();

    public DbSet<ChallengeTeamTaskCompletion> ChallengeTeamTaskCompletions => Set<ChallengeTeamTaskCompletion>();

    public DbSet<ChallengePeerReviewAssignment> ChallengePeerReviewAssignments => Set<ChallengePeerReviewAssignment>();

    public DbSet<ChallengePeerReview> ChallengePeerReviews => Set<ChallengePeerReview>();

    public DbSet<ChallengeTaskCompletion> ChallengeTaskCompletions => Set<ChallengeTaskCompletion>();

    public DbSet<ChallengeTaskAnswer> ChallengeTaskAnswers => Set<ChallengeTaskAnswer>();

    public DbSet<ChallengeTaskFileSubmission> ChallengeTaskFileSubmissions => Set<ChallengeTaskFileSubmission>();

    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    public DbSet<HelpDocument> HelpDocuments => Set<HelpDocument>();

    public DbSet<UserAppearanceSetting> UserAppearanceSettings => Set<UserAppearanceSetting>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<TeamInvitation> TeamInvitations => Set<TeamInvitation>();

    public DbSet<TeamProject> TeamProjects => Set<TeamProject>();

    public DbSet<TeamChatMessage> TeamChatMessages => Set<TeamChatMessage>();

    public DbSet<LeaderboardSeason> LeaderboardSeasons => Set<LeaderboardSeason>();

    public DbSet<LeaderboardSeasonProblem> LeaderboardSeasonProblems => Set<LeaderboardSeasonProblem>();

    public DbSet<LeaderboardSeasonBoard> LeaderboardSeasonBoards => Set<LeaderboardSeasonBoard>();

    public DbSet<LeaderboardSeasonProblemBenchmark> LeaderboardSeasonProblemBenchmarks => Set<LeaderboardSeasonProblemBenchmark>();

    public DbSet<LeaderboardUserProblemScore> LeaderboardUserProblemScores => Set<LeaderboardUserProblemScore>();

    public DbSet<LeaderboardSeasonAlias> LeaderboardSeasonAliases => Set<LeaderboardSeasonAlias>();

    public DbSet<LeaderboardSeasonArchiveEntry> LeaderboardSeasonArchiveEntries => Set<LeaderboardSeasonArchiveEntry>();

    public DbSet<LeaderboardSeasonArchiveProblemScore> LeaderboardSeasonArchiveProblemScores => Set<LeaderboardSeasonArchiveProblemScore>();

    public DbSet<LeaderboardSeasonRankSnapshot> LeaderboardSeasonRankSnapshots => Set<LeaderboardSeasonRankSnapshot>();

    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureSecurityAuditLogsAreAppendOnly();
        EnsureJudgeRevisionsAreImmutable();
        EnsureSubmissionJudgeRevisionBindingsAreImmutable();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureSecurityAuditLogsAreAppendOnly();
        EnsureJudgeRevisionsAreImmutable();
        EnsureSubmissionJudgeRevisionBindingsAreImmutable();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OnlineJudgeDbContext).Assembly);
    }

    private void EnsureSecurityAuditLogsAreAppendOnly()
    {
        if (ChangeTracker.Entries<SecurityAuditLog>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Security audit logs are append-only.");
        }
    }

    private void EnsureJudgeRevisionsAreImmutable()
    {
        var revisionChanged = ChangeTracker.Entries<ProblemJudgeRevision>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        var testCaseChanged = ChangeTracker.Entries<ProblemJudgeRevisionTestCase>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        var assetChanged = ChangeTracker.Entries<ProblemJudgeRevisionAsset>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        var choiceQuestionChanged = ChangeTracker.Entries<ProblemJudgeRevisionChoiceQuestion>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        var choiceOptionChanged = ChangeTracker.Entries<ProblemJudgeRevisionChoiceOption>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (revisionChanged || testCaseChanged || assetChanged || choiceQuestionChanged || choiceOptionChanged)
        {
            throw new InvalidOperationException("Problem judge revisions are immutable.");
        }
    }

    private void EnsureSubmissionJudgeRevisionBindingsAreImmutable()
    {
        var bindingChanged = ChangeTracker.Entries<Submission>()
            .Where(entry => entry.State == EntityState.Modified)
            .Select(entry => entry.Property(submission => submission.ProblemJudgeRevisionId))
            .Any(property => property.IsModified && !Equals(property.OriginalValue, property.CurrentValue));

        if (bindingChanged)
        {
            throw new InvalidOperationException("A submission's problem judge revision binding is immutable.");
        }
    }
}
