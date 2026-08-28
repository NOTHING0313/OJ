using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Teams;

namespace OnlineJudge.Tests.Teams;

public class TeamCoreTests
{
    [Fact]
    public async Task CreateTeam_CreatesConsistentOwnerMembership()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        await db.SaveChangesAsync();

        var result = await Service(db, owner).CreateTeamAsync(new CreateTeamRequest { Name = "Alpha Team" });

        Assert.True(result.IsSuccess);
        var team = await db.Teams.SingleAsync();
        var membership = await db.TeamMembers.SingleAsync();
        Assert.Equal(owner.Id, team.OwnerUserId);
        Assert.Equal(owner.Id, membership.UserId);
        Assert.Equal(TeamMemberRole.Owner, membership.Role);
        Assert.True(membership.IsActive);
    }

    [Fact]
    public async Task CreateTeam_RejectsSecondActiveTeamAndCaseInsensitiveDuplicateName()
    {
        await using var db = CreateDb();
        var first = AddUser(db, "first");
        var second = AddUser(db, "second");
        await db.SaveChangesAsync();
        Assert.True((await Service(db, first).CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).IsSuccess);

        var sameUser = await Service(db, first).CreateTeamAsync(new CreateTeamRequest { Name = "Beta" });
        var sameName = await Service(db, second).CreateTeamAsync(new CreateTeamRequest { Name = " alpha " });

        Assert.Equal("User already belongs to an active team.", sameUser.ErrorMessage);
        Assert.Equal("Team name already exists.", sameName.ErrorMessage);
    }

    [Fact]
    public async Task Invite_Accept_CreatesMemberAndPreservesOneActiveTeam()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var member = AddUser(db, "member");
        var otherOwner = AddUser(db, "other-owner");
        await db.SaveChangesAsync();
        var team = (await Service(db, owner).CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var otherTeam = (await Service(db, otherOwner).CreateTeamAsync(new CreateTeamRequest { Name = "Beta" })).Value!;
        var invite = await Service(db, owner).InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = member.UserName });
        Assert.True(invite.IsSuccess);

        Assert.True((await Service(db, member).AcceptInvitationAsync(invite.Value!.Id)).IsSuccess);
        Assert.True(await db.TeamMembers.AnyAsync(item => item.TeamId == team.Id && item.UserId == member.Id && item.IsActive));
        var secondInvite = await Service(db, otherOwner).InviteAsync(otherTeam.Id, new InviteTeamMemberRequest { UserName = member.UserName });
        Assert.Equal("User already belongs to an active team.", secondInvite.ErrorMessage);
    }

    [Fact]
    public async Task Invite_RejectsSelfExistingMemberDuplicateAndBlacklistedUser()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var target = AddUser(db, "target");
        var blocked = AddUser(db, "blocked", blacklisted: true);
        await db.SaveChangesAsync();
        var service = Service(db, owner);
        var team = (await service.CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;

        Assert.Equal("Cannot invite yourself.", (await service.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = owner.UserName })).ErrorMessage);
        Assert.Equal("Account is blacklisted.", (await service.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = blocked.UserName })).ErrorMessage);
        Assert.True((await service.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = target.UserName })).IsSuccess);
        Assert.Equal("A pending invitation already exists.", (await service.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = target.UserName })).ErrorMessage);
    }

    [Fact]
    public async Task Invitation_DeclineCancelAndRepeatedResponsesAreRejected()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var first = AddUser(db, "first");
        var second = AddUser(db, "second");
        await db.SaveChangesAsync();
        var ownerService = Service(db, owner);
        var team = (await ownerService.CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var declined = (await ownerService.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = first.UserName })).Value!;
        var cancelled = (await ownerService.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = second.UserName })).Value!;

        Assert.True((await Service(db, first).DeclineInvitationAsync(declined.Id)).IsSuccess);
        Assert.Equal("Invitation is no longer pending.", (await Service(db, first).AcceptInvitationAsync(declined.Id)).ErrorMessage);
        Assert.True((await ownerService.CancelInvitationAsync(team.Id, cancelled.Id)).IsSuccess);
        Assert.Equal("Invitation is no longer pending.", (await Service(db, second).AcceptInvitationAsync(cancelled.Id)).ErrorMessage);
        Assert.Equal("Invitation is no longer pending.", (await ownerService.CancelInvitationAsync(team.Id, declined.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Accept_RejectsDeletedTeamFullTeamAndBlacklistedInvitee()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var target = AddUser(db, "target");
        await db.SaveChangesAsync();
        var ownerService = Service(db, owner);
        var team = (await ownerService.CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var invitation = (await ownerService.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = target.UserName })).Value!;
        target.IsBlacklisted = true;
        await db.SaveChangesAsync();
        Assert.Equal("Account is blacklisted.", (await Service(db, target).AcceptInvitationAsync(invitation.Id)).ErrorMessage);

        target.IsBlacklisted = false;
        (await db.Teams.FindAsync(team.Id))!.IsDeleted = true;
        await db.SaveChangesAsync();
        Assert.Equal("Team not found.", (await Service(db, target).AcceptInvitationAsync(invitation.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Accept_RejectsFullTeamAndAlreadyAcceptedInvitation()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var target = AddUser(db, "target");
        await db.SaveChangesAsync();
        var ownerService = Service(db, owner);
        var team = (await ownerService.CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var invitation = (await ownerService.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = target.UserName })).Value!;
        for (var index = 0; index < 9; index++)
        {
            var user = AddUser(db, $"member-{index}");
            db.TeamMembers.Add(new TeamMember
            {
                Id = Guid.NewGuid(), TeamId = team.Id, UserId = user.Id, Role = TeamMemberRole.Member,
                IsActive = true, JoinedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();

        Assert.Equal("Team is full.", (await Service(db, target).AcceptInvitationAsync(invitation.Id)).ErrorMessage);
        var lastMember = await db.TeamMembers.FirstAsync(item => item.TeamId == team.Id && item.Role == TeamMemberRole.Member);
        lastMember.IsActive = false;
        lastMember.LeftAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.True((await Service(db, target).AcceptInvitationAsync(invitation.Id)).IsSuccess);
        Assert.Equal("Invitation is no longer pending.", (await Service(db, target).AcceptInvitationAsync(invitation.Id)).ErrorMessage);
    }

    [Fact]
    public async Task MemberLeave_PreservesHistoryAndCanJoinAnotherTeam()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var owner2 = AddUser(db, "owner2");
        var member = AddUser(db, "member");
        await db.SaveChangesAsync();
        var first = (await Service(db, owner).CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var second = (await Service(db, owner2).CreateTeamAsync(new CreateTeamRequest { Name = "Beta" })).Value!;
        var invite = (await Service(db, owner).InviteAsync(first.Id, new InviteTeamMemberRequest { UserName = member.UserName })).Value!;
        await Service(db, member).AcceptInvitationAsync(invite.Id);

        Assert.True((await Service(db, member).LeaveTeamAsync(first.Id)).IsSuccess);
        var oldMembership = await db.TeamMembers.SingleAsync(item => item.TeamId == first.Id && item.UserId == member.Id);
        Assert.False(oldMembership.IsActive);
        Assert.NotNull(oldMembership.LeftAt);
        var nextInvite = (await Service(db, owner2).InviteAsync(second.Id, new InviteTeamMemberRequest { UserName = member.UserName })).Value!;
        Assert.True((await Service(db, member).AcceptInvitationAsync(nextInvite.Id)).IsSuccess);
        Assert.Equal(2, await db.TeamMembers.CountAsync(item => item.UserId == member.Id));
    }

    [Fact]
    public async Task OwnerCannotLeaveOrRemoveSelf_ButCanRemoveMember()
    {
        await using var db = CreateDb();
        var (owner, member, team) = await TeamWithMember(db);
        var service = Service(db, owner);

        Assert.Contains("transfer ownership", (await service.LeaveTeamAsync(team.Id)).ErrorMessage);
        Assert.Equal("Owner cannot remove themselves.", (await service.RemoveMemberAsync(team.Id, owner.Id)).ErrorMessage);
        Assert.True((await service.RemoveMemberAsync(team.Id, member.Id)).IsSuccess);
        Assert.False((await db.TeamMembers.SingleAsync(item => item.UserId == member.Id)).IsActive);
    }

    [Fact]
    public async Task TransferOwnership_AtomicallyUpdatesTeamAndBothRoles()
    {
        await using var db = CreateDb();
        var (owner, member, team) = await TeamWithMember(db);

        Assert.True((await Service(db, owner).TransferOwnershipAsync(team.Id, new TransferTeamOwnershipRequest { UserId = member.Id })).IsSuccess);
        Assert.Equal(member.Id, (await db.Teams.FindAsync(team.Id))!.OwnerUserId);
        Assert.Equal(TeamMemberRole.Member, (await db.TeamMembers.SingleAsync(item => item.UserId == owner.Id)).Role);
        Assert.Equal(TeamMemberRole.Owner, (await db.TeamMembers.SingleAsync(item => item.UserId == member.Id)).Role);
    }

    [Fact]
    public async Task Dissolve_SoftDeletesTeamCancelsInvitationsAndDeactivatesMembers()
    {
        await using var db = CreateDb();
        var owner = AddUser(db, "owner");
        var invited = AddUser(db, "invited");
        await db.SaveChangesAsync();
        var service = Service(db, owner);
        var team = (await service.CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        await service.InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = invited.UserName });

        Assert.True((await service.DissolveTeamAsync(team.Id)).IsSuccess);
        Assert.True((await db.Teams.FindAsync(team.Id))!.IsDeleted);
        Assert.All(await db.TeamMembers.Where(item => item.TeamId == team.Id).ToListAsync(), item => Assert.False(item.IsActive));
        Assert.Equal(TeamInvitationStatus.Cancelled, (await db.TeamInvitations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rbac_MemberAndOtherAnswererCannotModifyOrReadOtherTeam_AuditRolesCanRead()
    {
        await using var db = CreateDb();
        var (owner, member, team) = await TeamWithMember(db);
        var other = AddUser(db, "other");
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var root = AddUser(db, "root", UserRole.Root);
        await db.SaveChangesAsync();

        Assert.Equal("Forbidden.", (await Service(db, member).UpdateTeamAsync(team.Id, new UpdateTeamRequest { Name = "Changed" })).ErrorMessage);
        Assert.Equal("Team not found.", (await Service(db, other).GetTeamAsync(team.Id)).ErrorMessage);
        Assert.True((await Service(db, setter).GetTeamAsync(team.Id)).IsSuccess);
        Assert.True((await Service(db, root).GetAllTeamsAsync()).IsSuccess);
        Assert.Equal("Forbidden.", (await Service(db, setter).DeleteProjectAsync(team.Id, Guid.NewGuid())).ErrorMessage);
        _ = owner;
    }

    [Fact]
    public async Task ProjectCrud_OwnerCanMutateMemberCanReadAndDuplicatesAreRejected()
    {
        await using var db = CreateDb();
        var (owner, member, team) = await TeamWithMember(db);
        var ownerService = Service(db, owner);
        var created = await ownerService.CreateProjectAsync(team.Id, new CreateTeamProjectRequest { Name = "Judge", RepositoryUrl = "HTTPS://GitHub.com/a/b.git/" });
        Assert.True(created.IsSuccess);
        Assert.Equal("https://github.com/a/b.git", created.Value!.RepositoryUrl);
        Assert.Equal("Repository is already bound to this team.", (await ownerService.CreateProjectAsync(team.Id, new CreateTeamProjectRequest { Name = "Other", RepositoryUrl = "https://github.com/a/b.git" })).ErrorMessage);
        Assert.True((await Service(db, member).GetProjectsAsync(team.Id)).IsSuccess);
        Assert.Equal("Forbidden.", (await Service(db, member).CreateProjectAsync(team.Id, new CreateTeamProjectRequest { Name = "No", RepositoryUrl = "https://github.com/a/c.git" })).ErrorMessage);
        Assert.True((await ownerService.UpdateProjectAsync(team.Id, created.Value.Id, new UpdateTeamProjectRequest { Name = "Judge 2", RepositoryUrl = "https://gitee.com/a/b.git" })).IsSuccess);
        Assert.True((await ownerService.DeleteProjectAsync(team.Id, created.Value.Id)).IsSuccess);
    }

    [Theory]
    [InlineData("https://github.com/a/b.git")]
    [InlineData("https://gitee.com/a/b.git")]
    [InlineData("https://gitlab.com/a/b.git")]
    public void RepositoryValidator_AllowsConfiguredHttpsHosts(string url)
    {
        Assert.True(Validator().ValidateAndNormalize(url).IsSuccess);
    }

    [Theory]
    [InlineData("http://github.com/a/b")]
    [InlineData("ssh://git@github.com/a/b")]
    [InlineData("git@github.com:a/b.git")]
    [InlineData("file:///tmp/repo")]
    [InlineData("https://localhost/a")]
    [InlineData("https://127.0.0.1/a")]
    [InlineData("https://10.0.0.1/a")]
    [InlineData("https://192.168.1.1/a")]
    [InlineData("https://172.16.0.1/a")]
    [InlineData("https://[::1]/a")]
    [InlineData("https://user:password@github.com/a/b")]
    [InlineData("https://evil.com/a")]
    [InlineData("https://github.com.evil.com/a")]
    [InlineData("https://evilgithub.com/a")]
    [InlineData("https://github.com/a/b?token=secret")]
    [InlineData("https://github.com/a/b#fragment")]
    public void RepositoryValidator_RejectsUnsafeUrls(string url)
    {
        Assert.True(Validator().ValidateAndNormalize(url).IsFailure);
    }

    [Fact]
    public void Model_HasDatabaseBackedActiveMembershipIndexes()
    {
        using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(TeamMember))!;
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"IsActive\" = TRUE" && index.Properties.Select(property => property.Name).SequenceEqual([nameof(TeamMember.UserId)]));
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"IsActive\" = TRUE" && index.Properties.Select(property => property.Name).SequenceEqual([nameof(TeamMember.TeamId), nameof(TeamMember.UserId)]));
    }

    private static async Task<(User Owner, User Member, TeamDto Team)> TeamWithMember(OnlineJudgeDbContext db)
    {
        var owner = AddUser(db, "owner");
        var member = AddUser(db, "member");
        await db.SaveChangesAsync();
        var team = (await Service(db, owner).CreateTeamAsync(new CreateTeamRequest { Name = "Alpha" })).Value!;
        var invitation = (await Service(db, owner).InviteAsync(team.Id, new InviteTeamMemberRequest { UserName = member.UserName })).Value!;
        await Service(db, member).AcceptInvitationAsync(invitation.Id);
        return (owner, member, team);
    }

    private static TeamService Service(OnlineJudgeDbContext db, User user)
    {
        return new TeamService(db, new TestCurrentUser(user.Id), TimeProvider.System, Validator());
    }

    private static TeamRepositoryUrlValidator Validator()
    {
        return new TeamRepositoryUrlValidator(new TeamProjectOptions());
    }

    private static OnlineJudgeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OnlineJudgeDbContext(options);
    }

    private static User AddUser(OnlineJudgeDbContext db, string userName, UserRole role = UserRole.Answerer, bool blacklisted = false)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = userName, Email = $"{userName}@example.com", PasswordHash = "hash",
            Role = role, IsBlacklisted = blacklisted, CreatedAt = now, UpdatedAt = now
        };
        db.Users.Add(user);
        return user;
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => null;
        public UserRole? Role => null;
    }
}
