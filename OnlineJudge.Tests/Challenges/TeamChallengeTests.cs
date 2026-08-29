using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Challenges;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Challenges;

public class TeamChallengeTests
{
    [Fact]
    public void TeamProgress_MemberCompetition_PreservesHistoricalMaximumAndContributor()
    {
        var completion = new ChallengeTeamTaskCompletion();
        var submissions = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var contributors = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var now = DateTimeOffset.UtcNow;

        Assert.True(ChallengeTeamProgressUpdater.TryApply(completion, 40, false, 100, submissions[0], contributors[0], now));
        Assert.True(ChallengeTeamProgressUpdater.TryApply(completion, 70, false, 100, submissions[1], contributors[1], now.AddMinutes(1)));
        Assert.False(ChallengeTeamProgressUpdater.TryApply(completion, 50, false, 100, submissions[2], contributors[2], now.AddMinutes(2)));
        Assert.True(ChallengeTeamProgressUpdater.TryApply(completion, 100, true, 100, submissions[3], contributors[0], now.AddMinutes(3)));
        var completedAt = completion.CompletedAt;
        var updatedAt = completion.UpdatedAt;
        Assert.False(ChallengeTeamProgressUpdater.TryApply(completion, 80, false, 100, submissions[4], contributors[2], now.AddMinutes(4)));

        Assert.Equal(100, completion.Score);
        Assert.Equal(submissions[3], completion.BestSubmissionId);
        Assert.Equal(contributors[0], completion.ContributorUserId);
        Assert.Equal(completedAt, completion.CompletedAt);
        Assert.Equal(updatedAt, completion.UpdatedAt);
    }

    [Fact]
    public void ProductionScoreStores_UseAtomicMaxAndSeasonTransactionLock()
    {
        var root = FindRepositoryRoot();
        var store = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengeBestScoreStore.cs"));
        var season = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Leaderboards", "SeasonScoreService.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "OnlineJudge.JudgeWorker", "Worker.cs"));

        Assert.Contains("ON CONFLICT", store, StringComparison.Ordinal);
        Assert.Contains("GREATEST", store, StringComparison.Ordinal);
        Assert.Contains("ScoringIdentityTransactionLock.AcquireAsync", season, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void Challenge_DefaultMode_IsIndividual()
    {
        Assert.Equal(ChallengeParticipationMode.Individual, new Challenge().ParticipationMode);
        Assert.Null(new Submission().ChallengeTeamParticipantId);
    }

    [Fact]
    public void TeamProgress_HigherMemberScore_ReplacesBestSubmissionAndContributor()
    {
        var oldTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var completion = new ChallengeTeamTaskCompletion { Score = 40, UpdatedAt = oldTime };
        var submissionId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();

        var changed = ChallengeTeamProgressUpdater.TryApply(completion, 70, false, 100, submissionId, contributorId, DateTimeOffset.UtcNow);

        Assert.True(changed);
        Assert.Equal(70, completion.Score);
        Assert.Equal(submissionId, completion.BestSubmissionId);
        Assert.Equal(contributorId, completion.ContributorUserId);
    }

    [Fact]
    public void TeamProgress_LowerMemberScore_DoesNotDowngradeOrChangeContributor()
    {
        var bestSubmissionId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var completion = new ChallengeTeamTaskCompletion
        {
            Score = 70, BestSubmissionId = bestSubmissionId, ContributorUserId = contributorId, UpdatedAt = updatedAt
        };

        Assert.False(ChallengeTeamProgressUpdater.TryApply(completion, 50, false, 100, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(70, completion.Score);
        Assert.Equal(bestSubmissionId, completion.BestSubmissionId);
        Assert.Equal(contributorId, completion.ContributorUserId);
        Assert.Equal(updatedAt, completion.UpdatedAt);
    }

    [Fact]
    public void TeamProgress_AcceptedSubmission_CompletesAtFullTaskScore()
    {
        var now = DateTimeOffset.UtcNow;
        var completion = new ChallengeTeamTaskCompletion { Score = 70 };

        Assert.True(ChallengeTeamProgressUpdater.TryApply(completion, 100, true, 100, Guid.NewGuid(), Guid.NewGuid(), now));
        Assert.True(completion.IsCompleted);
        Assert.Equal(100, completion.Score);
        Assert.Equal(now, completion.CompletedAt);
    }

    [Fact]
    public void TeamProgress_WorkerRetry_IsIdempotent()
    {
        var submissionId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var completion = new ChallengeTeamTaskCompletion
        {
            Score = 100, IsCompleted = true, BestSubmissionId = submissionId, ContributorUserId = contributorId,
            CompletedAt = updatedAt, UpdatedAt = updatedAt
        };

        Assert.False(ChallengeTeamProgressUpdater.TryApply(completion, 100, true, 100, submissionId, contributorId, DateTimeOffset.UtcNow));
        Assert.Equal(updatedAt, completion.UpdatedAt);
        Assert.Equal(updatedAt, completion.CompletedAt);
    }

    [Fact]
    public void EfModel_EnforcesTeamAndRosterRegistrationUniqueness()
    {
        using var db = CreateDb();
        var participant = db.Model.FindEntityType(typeof(ChallengeTeamParticipant))!;
        var roster = db.Model.FindEntityType(typeof(ChallengeTeamRosterMember))!;

        Assert.Contains(participant.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeId,TeamId");
        Assert.Contains(roster.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeId,UserId");
        Assert.Contains(roster.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeTeamParticipantId,UserId");
    }

    [Fact]
    public void EfModel_EnforcesOneCompletionPerTeamAndTask()
    {
        using var db = CreateDb();
        var completion = db.Model.FindEntityType(typeof(ChallengeTeamTaskCompletion))!;
        Assert.Contains(completion.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeTeamParticipantId,ChallengeTaskId");
    }

    [Fact]
    public void BackendSource_FreezesRosterSubmissionIdentityAndKeepsSeasonSubmissionPath()
    {
        var root = FindRepositoryRoot();
        var challengeService = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengeService.cs"));
        var submissionService = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Submissions", "SubmissionService.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "OnlineJudge.JudgeWorker", "Worker.cs"));
        var scoreStore = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengeBestScoreStore.cs"));

        Assert.Contains("member.IsActive", challengeService);
        Assert.Contains("ChallengeTeamRosterMembers", submissionService);
        Assert.Contains("ChallengeTeamParticipantId = challengeTeamParticipantId", submissionService);
        Assert.Contains("if (submission.ChallengeTeamParticipantId is", worker);
        Assert.Contains("ChallengeTeamTaskCompletions", scoreStore);
        Assert.Contains("seasonScoreService.ApplySubmissionResultAsync", worker);
    }

    [Fact]
    public void BackendSource_TeamOnlyRejectsIndividualJoinAndFileTasks()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengeService.cs"));
        Assert.Contains("Team registration is required.", source);
        Assert.Contains("Team-only challenges support algorithm tasks only.", source);
        Assert.Contains("Participation mode is locked.", source);
    }

    [Fact]
    public void PublicTeamLeaderboard_DoesNotExposeRosterOrContributor()
    {
        var properties = typeof(OnlineJudge.Application.Challenges.Dtos.ChallengeTeamLeaderboardEntryDto).GetProperties().Select(property => property.Name).ToList();
        Assert.Contains("TeamName", properties);
        Assert.DoesNotContain("ContributorUserId", properties);
        Assert.DoesNotContain("Roster", properties);
    }

    [Fact]
    public void Frontend_HasExplicitModeRegistrationTeamLeaderboardAndFileGate()
    {
        var root = FindRepositoryRoot();
        var api = File.ReadAllText(Path.Combine(root, "frontend", "src", "api", "challengesApi.ts"));
        var detail = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "ChallengeDetailPage.tsx"));
        var leaderboard = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "ChallengeLeaderboardPage.tsx"));
        var editor = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "AdminChallengeEditorPage.tsx"));
        var taskEditor = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "AdminChallengeTaskEditorPage.tsx"));

        Assert.Contains("participationMode", api);
        Assert.Contains("team-registration", api);
        Assert.Contains("等待队长报名", detail);
        Assert.Contains("teamEntries", leaderboard);
        Assert.Contains("战队挑战（仅算法题）", editor);
        Assert.Contains("战队赛暂不支持", taskEditor);
    }

    [Fact]
    public void Migration_DefaultsExistingChallengesToIndividualAndAddsRequiredTables()
    {
        var root = FindRepositoryRoot();
        var migration = Directory.GetFiles(Path.Combine(root, "OnlineJudge.Infrastructure", "Persistence", "Migrations"), "*_AddTeamChallenges.cs").Single();
        var source = File.ReadAllText(migration);
        Assert.Contains("ParticipationMode", source);
        Assert.Contains("defaultValue: 1", source);
        Assert.Contains("ChallengeTeamParticipants", source);
        Assert.Contains("ChallengeTeamRosterMembers", source);
        Assert.Contains("ChallengeTeamTaskCompletions", source);
    }

    private static string Names(IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties) => string.Join(',', properties.Select(property => property.Name));

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new OnlineJudgeDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
