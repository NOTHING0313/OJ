using Microsoft.EntityFrameworkCore;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Persistence;

public class OnlineJudgeDbContext(DbContextOptions<OnlineJudgeDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Problem> Problems => Set<Problem>();

    public DbSet<TestCase> TestCases => Set<TestCase>();

    public DbSet<Submission> Submissions => Set<Submission>();

    public DbSet<SubmissionCaseResult> SubmissionCaseResults => Set<SubmissionCaseResult>();

    public DbSet<ProblemCollaborator> ProblemCollaborators => Set<ProblemCollaborator>();

    public DbSet<ProblemJudgeAsset> ProblemJudgeAssets => Set<ProblemJudgeAsset>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OnlineJudgeDbContext).Assembly);
    }
}
