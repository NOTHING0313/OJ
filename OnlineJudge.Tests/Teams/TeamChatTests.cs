using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Teams;

namespace OnlineJudge.Tests.Teams;

public class TeamChatTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ActiveOwnerAndMember_CanReadAndSend()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();

        var ownerMessage = await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = " owner " });
        var memberMessage = await Service(db, seed.Member).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = "member" });
        var page = await Service(db, seed.Member).GetMessagesAsync(seed.Team.Id, null, null);

        Assert.True(ownerMessage.IsSuccess);
        Assert.True(memberMessage.IsSuccess);
        Assert.Equal(["member", "owner"], page.Value!.Messages.Select(message => message.Content).Order());
        Assert.Equal(page.Value.Messages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).Select(message => message.Id),
            page.Value.Messages.Select(message => message.Id));
        Assert.All(page.Value.Messages, message => Assert.Equal(TeamChatMessageType.User, message.Type));
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    [InlineData(UserRole.Root)]
    public async Task Outsider_CannotReadOrSend_RegardlessOfRole(UserRole role)
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var outsider = AddUser(db, $"outsider-{role}", role);
        await db.SaveChangesAsync();

        Assert.Equal("Forbidden.", (await Service(db, outsider).GetMessagesAsync(seed.Team.Id, null, null)).ErrorMessage);
        Assert.Equal("Forbidden.", (await Service(db, outsider).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = "no" })).ErrorMessage);
    }

    [Fact]
    public async Task LeftMember_CannotReadOrSend()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();
        var membership = db.TeamMembers.Single(member => member.UserId == seed.Member.Id);
        membership.IsActive = false;
        membership.LeftAt = Now;
        await db.SaveChangesAsync();

        Assert.Equal("Forbidden.", (await Service(db, seed.Member).GetMessagesAsync(seed.Team.Id, null, null)).ErrorMessage);
        Assert.Equal("Forbidden.", (await Service(db, seed.Member).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = "no" })).ErrorMessage);
    }

    [Fact]
    public async Task NewlyJoinedActiveMember_CanReadExistingTeamHistory()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var newcomer = AddUser(db, "newcomer");
        await db.SaveChangesAsync();
        await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = "before join" });
        db.TeamMembers.Add(Member(seed.Team, newcomer, TeamMemberRole.Member));
        await db.SaveChangesAsync();

        var page = await Service(db, newcomer).GetMessagesAsync(seed.Team.Id, null, null);

        Assert.Equal("before join", Assert.Single(page.Value!.Messages).Content);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task EmptyOrWhitespaceContent_IsRejected(string content)
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();

        Assert.Equal("Message content is required.", (await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = content })).ErrorMessage);
    }

    [Fact]
    public async Task Exactly2000Characters_IsAccepted_And2001Rejected()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();

        Assert.True((await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = new string('a', 2000) })).IsSuccess);
        Assert.Contains("2000", (await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = new string('a', 2001) })).ErrorMessage);
    }

    [Fact]
    public async Task Content_IsTrimmed_AndHtmlIsStoredAsText()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();

        var result = await Service(db, seed.Owner).SendAsync(seed.Team.Id, new SendTeamChatMessageRequest { Content = "  <script>alert(1)</script>  " });

        Assert.Equal("<script>alert(1)</script>", result.Value!.Content);
        Assert.Equal("<script>alert(1)</script>", (await db.TeamChatMessages.SingleAsync()).Content);
    }

    [Fact]
    public void SendRequest_CannotSpecifySystemSenderOrEventIdentity()
    {
        var properties = typeof(SendTeamChatMessageRequest).GetProperties().Select(property => property.Name).ToList();

        Assert.Equal([nameof(SendTeamChatMessageRequest.Content)], properties);
    }

    [Fact]
    public async Task CursorPagination_IsStableForEqualCreatedAt()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        for (var index = 1; index <= 55; index++)
        {
            db.TeamChatMessages.Add(new TeamChatMessage
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), TeamId = seed.Team.Id,
                SenderUserId = seed.Owner.Id, Type = TeamChatMessageType.User, Content = index.ToString(), CreatedAt = Now
            });
        }
        await db.SaveChangesAsync();

        var newest = (await Service(db, seed.Owner).GetMessagesAsync(seed.Team.Id, null, null)).Value!;
        var oldestOnPage = newest.Messages[0];
        var older = (await Service(db, seed.Owner).GetMessagesAsync(seed.Team.Id, oldestOnPage.CreatedAt, oldestOnPage.Id)).Value!;

        Assert.Equal(50, newest.Messages.Count);
        Assert.True(newest.HasMore);
        Assert.Equal(5, older.Messages.Count);
        Assert.False(older.HasMore);
        Assert.Empty(newest.Messages.Select(message => message.Id).Intersect(older.Messages.Select(message => message.Id)));
    }

    [Fact]
    public async Task Cursor_RequiresBothValues()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        await db.SaveChangesAsync();

        Assert.Contains("Both", (await Service(db, seed.Owner).GetMessagesAsync(seed.Team.Id, Now, null)).ErrorMessage);
        Assert.Contains("Both", (await Service(db, seed.Owner).GetMessagesAsync(seed.Team.Id, null, Guid.NewGuid())).ErrorMessage);
    }

    [Fact]
    public async Task Announcements_ShowOnlyRegisteredRelevantTeamChallenges()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var other = new Team { Id = Guid.NewGuid(), Name = "Other", NormalizedName = "OTHER", OwnerUserId = seed.Owner.Id, CreatedAt = Now, UpdatedAt = Now };
        db.Teams.Add(other);
        var visible = Challenge("Visible", Now.AddHours(-1), Now.AddHours(2));
        var unrelated = Challenge("Unrelated", Now.AddHours(-1), Now.AddHours(2));
        db.Challenges.AddRange(visible, unrelated);
        db.ChallengeTeamParticipants.AddRange(Participant(seed.Team, visible), Participant(other, unrelated));
        await db.SaveChangesAsync();

        var result = await Service(db, seed.Member).GetChallengeAnnouncementsAsync(seed.Team.Id);

        var announcement = Assert.Single(result.Value!);
        Assert.Equal(visible.Id, announcement.ChallengeId);
        Assert.Equal("active", announcement.Status);
    }

    [Fact]
    public async Task CompletedChallenge_ReconcilesExactlyOneSnapshotMessageWithFinalContributor()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var challenge = Challenge("Cup", Now.AddHours(-2), Now.AddHours(2));
        var firstTask = Task(challenge, "First");
        var lastTask = Task(challenge, "Last");
        var participant = Participant(seed.Team, challenge, "Frozen Team");
        db.Challenges.Add(challenge);
        db.ChallengeTasks.AddRange(firstTask, lastTask);
        db.ChallengeTeamParticipants.Add(participant);
        db.ChallengeTeamTaskCompletions.AddRange(
            Completion(participant, firstTask, seed.Owner, Now.AddMinutes(-5)),
            Completion(participant, lastTask, seed.Member, Now));
        await db.SaveChangesAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();

        var message = Assert.Single(await db.TeamChatMessages.ToListAsync());
        Assert.Equal("member 已提交，Frozen Team 已完成挑战", message.Content);
        Assert.Equal($"challenge-completed:{challenge.Id}:{participant.Id}", message.EventKey);
        Assert.Equal(challenge.Id, message.RelatedChallengeId);
    }

    [Fact]
    public async Task IncompleteChallenge_DoesNotCreateCompletionMessage()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var challenge = Challenge("Cup", Now.AddHours(-2), Now.AddHours(2));
        var firstTask = Task(challenge, "First");
        _ = Task(challenge, "Last");
        var participant = Participant(seed.Team, challenge);
        db.Challenges.Add(challenge); db.ChallengeTeamParticipants.Add(participant);
        db.ChallengeTeamTaskCompletions.Add(Completion(participant, firstTask, seed.Owner, Now));
        await db.SaveChangesAsync();

        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();

        Assert.Empty(db.TeamChatMessages);
    }

    [Fact]
    public async Task LaterScoreUpdates_DoNotDuplicateCompletionEvent()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var challenge = Challenge("Cup", Now.AddHours(-2), Now.AddHours(2));
        var task = Task(challenge, "Only");
        var participant = Participant(seed.Team, challenge);
        var completion = Completion(participant, task, seed.Owner, Now);
        db.Challenges.Add(challenge); db.ChallengeTeamParticipants.Add(participant); db.ChallengeTeamTaskCompletions.Add(completion);
        await db.SaveChangesAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();
        completion.Score -= 10; completion.UpdatedAt = Now.AddMinutes(1); await db.SaveChangesAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now.AddMinutes(1))).ReconcileAsync();
        completion.Score += 20; completion.UpdatedAt = Now.AddMinutes(2); await db.SaveChangesAsync();

        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now.AddMinutes(2))).ReconcileAsync();

        Assert.Single(db.TeamChatMessages);
    }

    [Fact]
    public async Task PeerReviewAssignment_ReconcilesOneInternalLinkMessage()
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var targetTeam = new Team { Id = Guid.NewGuid(), Name = "Target", NormalizedName = "TARGET", OwnerUserId = seed.Owner.Id, CreatedAt = Now, UpdatedAt = Now };
        var challenge = Challenge("Review", Now.AddDays(-2), Now.AddMinutes(-1), peerReview: true);
        var reviewer = Participant(seed.Team, challenge);
        var target = Participant(targetTeam, challenge);
        var assignment = new ChallengePeerReviewAssignment
        {
            Id = Guid.NewGuid(), ChallengeId = challenge.Id, ReviewerParticipantId = reviewer.Id, TargetParticipantId = target.Id,
            ReviewerTeamNameSnapshot = reviewer.TeamNameSnapshot, TargetTeamNameSnapshot = target.TeamNameSnapshot,
            TargetProjectNameSnapshot = "Target Project", TargetRepositoryUrlSnapshot = "https://github.com/a/b.git", CreatedAt = Now
        };
        db.Teams.Add(targetTeam); db.Challenges.Add(challenge); db.ChallengeTeamParticipants.AddRange(reviewer, target); db.ChallengePeerReviewAssignments.Add(assignment);
        await db.SaveChangesAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();
        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();

        var message = Assert.Single(await db.TeamChatMessages.ToListAsync());
        Assert.Equal("挑战已结束，互评任务已发布", message.Content);
        Assert.Equal(assignment.Id, message.RelatedPeerReviewAssignmentId);
        Assert.Equal(reviewer.TeamId, message.TeamId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task NoReadyPeerReviewAssignment_ProducesNoMessage(bool peerReviewEnabled, bool addAssignment)
    {
        await using var db = CreateDb();
        var seed = SeedTeam(db);
        var challenge = Challenge("Review", Now.AddDays(-2), Now.AddMinutes(-1), peerReviewEnabled);
        db.Challenges.Add(challenge);
        db.ChallengeTeamParticipants.Add(Participant(seed.Team, challenge));
        await db.SaveChangesAsync();
        _ = addAssignment;

        await new TeamChatSystemEventReconciler(db, new FixedTimeProvider(Now)).ReconcileAsync();

        Assert.Empty(db.TeamChatMessages);
    }

    [Fact]
    public void Model_HasStablePagingAndPartialUniqueEventIndexes()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(TeamChatMessage))!;

        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(TeamChatMessage.TeamId), nameof(TeamChatMessage.CreatedAt), nameof(TeamChatMessage.Id)]));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"EventKey\" IS NOT NULL"
            && index.Properties.Single().Name == nameof(TeamChatMessage.EventKey));
    }

    [Fact]
    public void JudgeScoringSeasonAndPeerReview_DoNotDependOnTeamChat()
    {
        var root = FindRepositoryRoot();
        var worker = File.ReadAllText(Path.Combine(root, "OnlineJudge.JudgeWorker", "Worker.cs"));
        var bestScore = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengeBestScoreStore.cs"));
        var seasonScore = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Leaderboards", "SeasonScoreService.cs"));
        var peerReview = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengePeerReviewService.cs"));

        Assert.DoesNotContain("TeamChat", worker);
        Assert.DoesNotContain("TeamChatMessage", worker);
        Assert.DoesNotContain("TeamChat", bestScore);
        Assert.DoesNotContain("TeamChat", seasonScore);
        Assert.DoesNotContain("TeamChat", peerReview);
    }

    private static TeamChatService Service(OnlineJudgeDbContext db, User user) =>
        new(db, new TestCurrentUser(user.Id), new FixedTimeProvider(Now));

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new OnlineJudgeDbContext(options);
    }

    private static (Team Team, User Owner, User Member) SeedTeam(OnlineJudgeDbContext db)
    {
        var owner = AddUser(db, "owner");
        var member = AddUser(db, "member");
        var team = new Team { Id = Guid.NewGuid(), Name = "Alpha", NormalizedName = "ALPHA", OwnerUserId = owner.Id, CreatedAt = Now, UpdatedAt = Now };
        db.Teams.Add(team);
        db.TeamMembers.AddRange(Member(team, owner, TeamMemberRole.Owner), Member(team, member, TeamMemberRole.Member));
        return (team, owner, member);
    }

    private static User AddUser(OnlineJudgeDbContext db, string name, UserRole role = UserRole.Answerer)
    {
        var user = new User { Id = Guid.NewGuid(), UserName = name, Email = $"{name}@example.com", PasswordHash = "hash", Role = role, CreatedAt = Now, UpdatedAt = Now };
        db.Users.Add(user);
        return user;
    }

    private static TeamMember Member(Team team, User user, TeamMemberRole role) => new()
    {
        Id = Guid.NewGuid(), TeamId = team.Id, UserId = user.Id, Team = team, User = user, Role = role, IsActive = true, JoinedAt = Now
    };

    private static Challenge Challenge(string title, DateTimeOffset start, DateTimeOffset end, bool peerReview = false) => new()
    {
        Id = Guid.NewGuid(), Title = title, Description = title, StartAt = start, EndAt = end, IsPublished = true,
        ParticipationMode = ChallengeParticipationMode.TeamOnly, PeerReviewEnabled = peerReview,
        PeerReviewEndAt = peerReview ? end.AddDays(2) : null, CreatedAt = Now, UpdatedAt = Now
    };

    private static ChallengeTask Task(Challenge challenge, string title)
    {
        var task = new ChallengeTask { Id = Guid.NewGuid(), ChallengeId = challenge.Id, Challenge = challenge, Title = title, Description = title, IsPublished = true, CreatedAt = Now, UpdatedAt = Now };
        challenge.Tasks.Add(task);
        return task;
    }

    private static ChallengeTeamParticipant Participant(Team team, Challenge challenge, string? snapshot = null) => new()
    {
        Id = Guid.NewGuid(), ChallengeId = challenge.Id, Challenge = challenge, TeamId = team.Id, Team = team,
        TeamNameSnapshot = snapshot ?? team.Name, RegisteredByUserId = team.OwnerUserId, RegisteredAt = Now
    };

    private static ChallengeTeamTaskCompletion Completion(ChallengeTeamParticipant participant, ChallengeTask task, User contributor, DateTimeOffset completedAt) => new()
    {
        Id = Guid.NewGuid(), ChallengeId = participant.ChallengeId, ChallengeTaskId = task.Id,
        ChallengeTeamParticipantId = participant.Id, ChallengeTeamParticipant = participant, ChallengeTask = task,
        ContributorUserId = contributor.Id, ContributorUser = contributor, Score = 100, IsCompleted = true,
        CompletedAt = completedAt, UpdatedAt = completedAt
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => null;
        public UserRole? Role => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
