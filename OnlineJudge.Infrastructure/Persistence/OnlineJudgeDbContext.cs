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

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public DbSet<ChallengeTask> ChallengeTasks => Set<ChallengeTask>();

    public DbSet<ChallengeParticipant> ChallengeParticipants => Set<ChallengeParticipant>();

    public DbSet<ChallengeTaskCompletion> ChallengeTaskCompletions => Set<ChallengeTaskCompletion>();

    public DbSet<ChallengeTaskAnswer> ChallengeTaskAnswers => Set<ChallengeTaskAnswer>();

    public DbSet<ChallengeTaskFileSubmission> ChallengeTaskFileSubmissions => Set<ChallengeTaskFileSubmission>();

    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    public DbSet<UserAppearanceSetting> UserAppearanceSettings => Set<UserAppearanceSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OnlineJudgeDbContext).Assembly);
    }
}
