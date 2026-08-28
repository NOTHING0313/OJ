using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Challenges;

public class TeamChallengeIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OwnerRegistration_FreezesAllAndOnlyActiveMembers()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db);
        var inactive = AddUser(db, "inactive");
        db.TeamMembers.Add(Member(seed.Team.Id, inactive.Id, TeamMemberRole.Member, false));
        await db.SaveChangesAsync();

        var result = await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id);

        Assert.True(result.IsSuccess);
        var roster = await db.ChallengeTeamRosterMembers.OrderBy(member => member.UserNameSnapshot).ToListAsync();
        Assert.Equal(["member", "owner"], roster.Select(member => member.UserNameSnapshot));
        Assert.DoesNotContain(roster, member => member.UserId == inactive.Id);
        Assert.Equal(seed.Team.Name, (await db.ChallengeTeamParticipants.SingleAsync()).TeamNameSnapshot);
    }

    [Fact]
    public async Task Registration_RequiresOwnerActiveTeamAndTeamOnlyOpenPublishedChallenge()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db);
        var noTeam = AddUser(db, "no-team");
        await db.SaveChangesAsync();

        Assert.Equal("Forbidden.", (await ChallengeService(db, seed.Member).RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);
        Assert.Equal("Active team membership is required.", (await ChallengeService(db, noTeam).RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);

        seed.Challenge.ParticipationMode = ChallengeParticipationMode.Individual;
        await db.SaveChangesAsync();
        Assert.Equal("Challenge uses individual participation.", (await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);

        seed.Challenge.ParticipationMode = ChallengeParticipationMode.TeamOnly;
        seed.Challenge.StartAt = Now.AddMinutes(1);
        await db.SaveChangesAsync();
        Assert.Equal("Challenge not found.", (await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);

        seed.Challenge.StartAt = Now.AddHours(-2);
        seed.Challenge.EndAt = Now.AddTicks(-1);
        await db.SaveChangesAsync();
        Assert.Equal("Challenge is not open.", (await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Registration_IsImmutableSnapshotAndDuplicateTeamIsRejected()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db);
        var service = ChallengeService(db, seed.Owner);
        Assert.True((await service.RegisterTeamAsync(seed.Challenge.Id)).IsSuccess);

        seed.Team.Name = "Renamed";
        var late = AddUser(db, "late");
        db.TeamMembers.Add(Member(seed.Team.Id, late.Id, TeamMemberRole.Member, true));
        var original = await db.TeamMembers.SingleAsync(member => member.UserId == seed.Member.Id);
        original.IsActive = false;
        await db.SaveChangesAsync();

        var participant = await db.ChallengeTeamParticipants.Include(item => item.RosterMembers).SingleAsync();
        Assert.Equal("Alpha", participant.TeamNameSnapshot);
        Assert.Contains(participant.RosterMembers, member => member.UserId == seed.Member.Id);
        Assert.DoesNotContain(participant.RosterMembers, member => member.UserId == late.Id);
        Assert.Equal("Team is already registered.", (await service.RegisterTeamAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task TeamOnlyJoinNeverCreatesIndividualParticipant()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db);
        var result = await ChallengeService(db, seed.Member).JoinChallengeAsync(seed.Challenge.Id);
        Assert.Equal("Team registration is required.", result.ErrorMessage);
        Assert.Empty(db.ChallengeParticipants);
    }

    [Fact]
    public async Task RosterSubmission_FreezesParticipant_WhileLateMemberIsRejected()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db, withProblem: true);
        Assert.True((await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id)).IsSuccess);
        var late = AddUser(db, "late");
        db.TeamMembers.Add(Member(seed.Team.Id, late.Id, TeamMemberRole.Member, true));
        await db.SaveChangesAsync();

        var ownerResult = await SubmissionService(db, seed.Owner).CreateSubmissionAsync(Request(seed));
        var lateResult = await SubmissionService(db, late).CreateSubmissionAsync(Request(seed));

        Assert.True(ownerResult.IsSuccess);
        Assert.NotNull((await db.Submissions.FindAsync(ownerResult.Value!.Id))!.ChallengeTeamParticipantId);
        Assert.Equal("User is not registered on a team for this challenge.", lateResult.ErrorMessage);
        Assert.Single(db.Submissions);
    }

    [Fact]
    public async Task IndividualAndDirectProblemSubmissions_DoNotFreezeTeamIdentity()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db, withProblem: true);
        var directRequest = Request(seed);
        directRequest.ChallengeTaskId = null;
        var direct = await SubmissionService(db, seed.Owner).CreateSubmissionAsync(directRequest);

        Assert.True(direct.IsSuccess);
        Assert.Null((await db.Submissions.FindAsync(direct.Value!.Id))!.ChallengeTeamParticipantId);
        Assert.Empty(db.ChallengeParticipants);
        Assert.Null(new Submission { ChallengeTaskId = seed.Task!.Id }.ChallengeTeamParticipantId);
    }

    [Fact]
    public async Task TeamProgressAndLeaderboard_AreSharedSortedAndSnapshotBased()
    {
        await using var db = CreateDb();
        var first = SeedTeamChallenge(db, withProblem: true);
        Assert.True((await ChallengeService(db, first.Owner).RegisterTeamAsync(first.Challenge.Id)).IsSuccess);
        var firstParticipant = await db.ChallengeTeamParticipants.SingleAsync();
        db.ChallengeTeamTaskCompletions.Add(new ChallengeTeamTaskCompletion
        {
            Id = Guid.NewGuid(), ChallengeId = first.Challenge.Id, ChallengeTaskId = first.Task!.Id,
            ChallengeTeamParticipantId = firstParticipant.Id, Score = 70, IsCompleted = false,
            ContributorUserId = first.Owner.Id, CompletedAt = Now, UpdatedAt = Now
        });
        await db.SaveChangesAsync();

        var memberDetail = await ChallengeService(db, first.Member).GetChallengeAsync(first.Challenge.Id);
        var board = await ChallengeService(db, first.Member).GetLeaderboardAsync(first.Challenge.Id);

        Assert.Equal(70, memberDetail.Value!.Tasks.Single().EarnedScore);
        Assert.Equal(ChallengeParticipationMode.TeamOnly, board.Value!.ParticipationMode);
        Assert.Single(board.Value.TeamEntries);
        Assert.Equal("Alpha", board.Value.TeamEntries[0].TeamName);
        Assert.Empty(board.Value.Entries);
    }

    [Fact]
    public async Task AdminAudit_ExposesFrozenRosterContributorAndBestSubmissionOnlyToManagerEndpoint()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db, withProblem: true);
        Assert.True((await ChallengeService(db, seed.Owner).RegisterTeamAsync(seed.Challenge.Id)).IsSuccess);
        var participant = await db.ChallengeTeamParticipants.SingleAsync();
        var submission = new Submission { Id = Guid.NewGuid(), ProblemId = seed.Problem!.Id, UserId = seed.Owner.Id, ChallengeTaskId = seed.Task!.Id, ChallengeTeamParticipantId = participant.Id, Language = JudgeLanguage.Cpp17, SourceCode = "x", Status = JudgeStatus.Accepted, CreatedAt = Now };
        db.Submissions.Add(submission);
        db.ChallengeTeamTaskCompletions.Add(new ChallengeTeamTaskCompletion { Id = Guid.NewGuid(), ChallengeId = seed.Challenge.Id, ChallengeTaskId = seed.Task.Id, ChallengeTeamParticipantId = participant.Id, BestSubmissionId = submission.Id, ContributorUserId = seed.Owner.Id, Score = 100, IsCompleted = true, CompletedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync();

        var audit = await ChallengeService(db, seed.Setter).GetAdminSummaryAsync(seed.Challenge.Id);
        var publicBoard = await ChallengeService(db, seed.Member).GetLeaderboardAsync(seed.Challenge.Id);

        Assert.True(audit.IsSuccess);
        Assert.Equal(seed.Owner.UserName, audit.Value!.Teams.Single().Tasks.Single().ContributorUserName);
        Assert.Equal(submission.Id, audit.Value.Teams.Single().Tasks.Single().BestSubmissionId);
        Assert.Equal(2, audit.Value.Teams.Single().Roster.Count);
        Assert.DoesNotContain(publicBoard.Value!.TeamEntries.GetType().GetProperties(), property => property.Name.Contains("Contributor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ParticipationMode_IsLockedByPublishStartOrParticipant_AndTeamFileTaskIsRejected()
    {
        await using var db = CreateDb();
        var seed = SeedTeamChallenge(db);
        var request = new UpdateChallengeRequest { Title = seed.Challenge.Title, StartAt = seed.Challenge.StartAt, EndAt = seed.Challenge.EndAt, IsPublished = true, ParticipationMode = ChallengeParticipationMode.Individual };
        Assert.Equal("Participation mode is locked.", (await ChallengeService(db, seed.Setter).UpdateChallengeAsync(seed.Challenge.Id, request)).ErrorMessage);

        var file = new CreateChallengeTaskRequest { Title = "file", TaskType = ChallengeTaskType.FileUpload, Difficulty = ChallengeTaskDifficulty.Pawn, BoardX = 0, BoardY = 0 };
        Assert.Equal("Team-only challenges support algorithm tasks only.", (await ChallengeService(db, seed.Setter).AddTaskAsync(seed.Challenge.Id, file)).ErrorMessage);
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static Seed SeedTeamChallenge(OnlineJudgeDbContext db, bool withProblem = false)
    {
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var owner = AddUser(db, "owner");
        var member = AddUser(db, "member");
        var team = new Team { Id = Guid.NewGuid(), Name = "Alpha", NormalizedName = "ALPHA", OwnerUserId = owner.Id, CreatedAt = Now, UpdatedAt = Now };
        db.Teams.Add(team);
        db.TeamMembers.AddRange(Member(team.Id, owner.Id, TeamMemberRole.Owner, true), Member(team.Id, member.Id, TeamMemberRole.Member, true));
        var challenge = new Challenge { Id = Guid.NewGuid(), Title = "Team", Description = "", StartAt = Now.AddHours(-1), EndAt = Now.AddHours(1), CreatedByUserId = setter.Id, IsPublished = true, ParticipationMode = ChallengeParticipationMode.TeamOnly, CreatedAt = Now, UpdatedAt = Now };
        db.Challenges.Add(challenge);
        Problem? problem = null;
        ChallengeTask? task = null;
        if (withProblem)
        {
            problem = new Problem { Id = Guid.NewGuid(), Title = "P", Description = "", CreatedByUserId = setter.Id, IsPublished = true, CreatedAt = Now, UpdatedAt = Now };
            task = new ChallengeTask { Id = Guid.NewGuid(), ChallengeId = challenge.Id, Title = "T", TaskType = ChallengeTaskType.Algorithm, Difficulty = ChallengeTaskDifficulty.Pawn, AlgorithmProblemId = problem.Id, Score = 100, IsPublished = true, CreatedAt = Now, UpdatedAt = Now };
            db.Problems.Add(problem);
            db.ChallengeTasks.Add(task);
        }
        db.SaveChanges();
        return new Seed(setter, owner, member, team, challenge, problem, task);
    }

    private static User AddUser(OnlineJudgeDbContext db, string name, UserRole role = UserRole.Answerer)
    {
        var user = new User { Id = Guid.NewGuid(), UserName = name, Email = $"{name}@test", PasswordHash = "hash", Role = role, CreatedAt = Now, UpdatedAt = Now };
        db.Users.Add(user);
        return user;
    }

    private static TeamMember Member(Guid teamId, Guid userId, TeamMemberRole role, bool active) => new() { Id = Guid.NewGuid(), TeamId = teamId, UserId = userId, Role = role, IsActive = active, JoinedAt = Now };
    private static ChallengeService ChallengeService(OnlineJudgeDbContext db, User user) => new(db, new CurrentUser(user.Id), new ContentVisibilityPolicy(new FixedTimeProvider(Now)));
    private static SubmissionService SubmissionService(OnlineJudgeDbContext db, User user) => new(db, new NoopQueue(), new CurrentUser(user.Id), new ContentVisibilityPolicy(new FixedTimeProvider(Now)));
    private static CreateSubmissionRequest Request(Seed seed) => new() { ProblemId = seed.Problem!.Id, ChallengeTaskId = seed.Task!.Id, Language = JudgeLanguage.Cpp17, SourceCode = "int main(){}" };

    private sealed record Seed(User Setter, User Owner, User Member, Team Team, Challenge Challenge, Problem? Problem, ChallengeTask? Task);
    private sealed class CurrentUser(Guid id) : ICurrentUser { public bool IsAuthenticated => true; public Guid? UserId => id; public string? UserName => null; public UserRole? Role => null; }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class NoopQueue : IJudgeQueue { public Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.CompletedTask; }
}
