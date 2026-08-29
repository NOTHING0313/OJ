using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Api.Services;
using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Challenges.Services;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.ContentVisibility;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Tests.Challenges;

public class ChallengePeerReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultsAndEfModel_AreBackwardCompatibleAndUnique()
    {
        using var db = CreateDb();
        Assert.False(new Challenge().PeerReviewEnabled);
        Assert.Null(new Challenge().PeerReviewEndAt);
        var assignment = db.Model.FindEntityType(typeof(ChallengePeerReviewAssignment))!;
        var review = db.Model.FindEntityType(typeof(ChallengePeerReview))!;
        Assert.Contains(assignment.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeId,ReviewerParticipantId");
        Assert.Contains(assignment.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "ChallengeId,TargetParticipantId");
        Assert.Contains(review.GetIndexes(), index => index.IsUnique && Names(index.Properties) == "AssignmentId");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public async Task AssignmentRing_HandlesTeamCountsWithoutSelfReview(int teamCount, int expectedAssignments)
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, teamCount);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var assignments = await db.ChallengePeerReviewAssignments.ToListAsync();
        Assert.Equal(expectedAssignments, assignments.Count);
        Assert.DoesNotContain(assignments, assignment => assignment.ReviewerParticipantId == assignment.TargetParticipantId);
        Assert.Equal(assignments.Count, assignments.Select(assignment => assignment.ReviewerParticipantId).Distinct().Count());
        Assert.Equal(assignments.Count, assignments.Select(assignment => assignment.TargetParticipantId).Distinct().Count());
    }

    [Fact]
    public async Task AssignmentRing_IsDeterministicAndIdempotent()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 3);
        var service = Service(db, seed.Setter, seed.Time);
        await service.EnsureAssignmentsAsync();
        var first = await db.ChallengePeerReviewAssignments.OrderBy(row => row.ReviewerParticipantId)
            .Select(row => new { row.ReviewerParticipantId, row.TargetParticipantId }).ToListAsync();
        await service.EnsureAssignmentsAsync();
        var second = await db.ChallengePeerReviewAssignments.OrderBy(row => row.ReviewerParticipantId)
            .Select(row => new { row.ReviewerParticipantId, row.TargetParticipantId }).ToListAsync();
        Assert.Equal(first, second);
        Assert.Equal(3, second.Count);
    }

    [Fact]
    public async Task Workspace_IsOnlyAvailableToFrozenReviewerRoster()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var outsider = AddUser(db, "outsider");
        await db.SaveChangesAsync();
        var denied = await Service(db, outsider, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id);
        var allowed = await Service(db, seed.Participants[0].User, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id);
        Assert.Equal("Forbidden.", denied.ErrorMessage);
        Assert.True(allowed.IsSuccess);
        Assert.True(allowed.Value!.AssignmentReady);
        Assert.NotEqual(seed.Participants[0].Participant.TeamNameSnapshot, allowed.Value.TargetTeamName);
    }

    [Fact]
    public async Task FrozenRosterMembers_ShareLastWriteWinsDraft()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2, membersPerTeam: 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var firstTeam = seed.Participants[0];
        Assert.True((await Service(db, firstTeam.User, seed.Time).SaveDraftAsync(seed.Challenge.Id,
            new SaveChallengePeerReviewRequest { Summary = "first" })).IsSuccess);
        Assert.True((await Service(db, firstTeam.OtherUsers[0], seed.Time).SaveDraftAsync(seed.Challenge.Id,
            new SaveChallengePeerReviewRequest { Summary = "second" })).IsSuccess);
        var workspace = await Service(db, firstTeam.User, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id);
        Assert.Equal("second", workspace.Value!.Review!.Summary);
        Assert.Single(db.ChallengePeerReviews);
    }

    [Fact]
    public async Task Submit_IsValidatedAndImmutable()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        Assert.Equal("Overall score is required.", (await service.SubmitAsync(seed.Challenge.Id, new SaveChallengePeerReviewRequest())).ErrorMessage);
        var submitted = await service.SubmitAsync(seed.Challenge.Id, ValidReview());
        Assert.True(submitted.IsSuccess);
        Assert.Equal(ChallengePeerReviewStatus.Submitted, submitted.Value!.Review!.Status);
        Assert.Equal("Peer review has already been submitted.", (await service.SaveDraftAsync(seed.Challenge.Id, ValidReview())).ErrorMessage);
        Assert.Equal("Peer review has already been submitted.", (await service.SubmitAsync(seed.Challenge.Id, ValidReview())).ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ScoreOutsideOneToFive_IsRejected(int score)
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        var request = ValidReview(); request.OverallScore = score;
        Assert.Equal("Overall score must be between 1 and 5.", (await service.SaveDraftAsync(seed.Challenge.Id, request)).ErrorMessage);
    }

    [Fact]
    public async Task Deadline_ClosesDraftAndSubmitButKeepsReadOnlyWorkspace()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        seed.Time.Now = Now.AddHours(3);
        Assert.Equal("Peer review deadline has passed.", (await service.SaveDraftAsync(seed.Challenge.Id, ValidReview())).ErrorMessage);
        var workspace = await service.GetMyWorkspaceAsync(seed.Challenge.Id);
        Assert.True(workspace.Value!.IsExpired);
        Assert.False(workspace.Value.CanEdit);
    }

    [Fact]
    public async Task Audit_AllowsAnyProblemSetterOrRootAndRejectsAnswerers()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var otherSetter = AddUser(db, "other-setter", UserRole.ProblemSetter);
        var root = AddUser(db, "root", UserRole.Root);
        await db.SaveChangesAsync();
        Assert.True((await Service(db, seed.Setter, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).IsSuccess);
        Assert.True((await Service(db, otherSetter, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).IsSuccess);
        Assert.True((await Service(db, root, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).IsSuccess);
        Assert.Equal("Forbidden.", (await Service(db, seed.Participants[0].User, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Audit_UsesDatabaseRoleAndBlacklistStateInsteadOfTokenRole()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var downgraded = AddUser(db, "downgraded", UserRole.Answerer);
        var blacklisted = AddUser(db, "blacklisted", UserRole.ProblemSetter);
        blacklisted.IsBlacklisted = true;
        await db.SaveChangesAsync();

        var staleTokenService = new ChallengePeerReviewService(db, new CurrentUser(downgraded.Id, UserRole.ProblemSetter), seed.Time);
        Assert.Equal("Forbidden.", (await staleTokenService.GetAdminAuditAsync(seed.Challenge.Id)).ErrorMessage);
        Assert.Equal("Account is blacklisted.", (await Service(db, blacklisted, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task UnrelatedProblemSetter_AuditIsReadOnlyAndDoesNotGrantChallengeWrites()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var otherSetter = AddUser(db, "other-setter", UserRole.ProblemSetter);
        await db.SaveChangesAsync();
        var challengeService = ChallengeService(db, otherSetter, seed.Time);
        var update = new UpdateChallengeRequest
        {
            Title = seed.Challenge.Title, Description = seed.Challenge.Description,
            StartAt = seed.Challenge.StartAt, EndAt = seed.Challenge.EndAt,
            IsPublished = seed.Challenge.IsPublished, ParticipationMode = seed.Challenge.ParticipationMode,
            PeerReviewEnabled = seed.Challenge.PeerReviewEnabled, PeerReviewEndAt = seed.Challenge.PeerReviewEndAt
        };

        Assert.True((await Service(db, otherSetter, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).IsSuccess);
        Assert.Equal("Forbidden.", (await challengeService.UpdateChallengeAsync(seed.Challenge.Id, update)).ErrorMessage);
        Assert.Equal("Forbidden.", (await challengeService.DeleteChallengeAsync(seed.Challenge.Id)).ErrorMessage);
        Assert.True(await db.Challenges.AnyAsync(challenge => challenge.Id == seed.Challenge.Id));
    }

    [Fact]
    public async Task AssignmentUsesFrozenProjectSnapshotAndDoesNotAffectScores()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var participant = seed.Participants[1].Participant;
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        participant.ProjectNameSnapshot = "renamed later";
        participant.RepositoryUrlSnapshot = "https://example.com/changed.git";
        await db.SaveChangesAsync();
        var workspace = await Service(db, seed.Participants[0].User, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id);
        Assert.Equal("Project 2", workspace.Value!.TargetProjectName);
        Assert.Equal("https://example.com/team-2.git", workspace.Value.TargetRepositoryUrl);
        Assert.Empty(db.ChallengeTeamTaskCompletions);
        Assert.Empty(db.ChallengeTaskCompletions);
        Assert.Empty(db.LeaderboardUserProblemScores);
    }

    [Fact]
    public async Task Registration_RequiresOwnedProjectAndFreezesProjectMetadata()
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var owner = AddUser(db, "owner");
        var otherOwner = AddUser(db, "other");
        var team = AddTeam(db, owner, "Alpha");
        var otherTeam = AddTeam(db, otherOwner, "Beta");
        var project = AddProject(db, team, owner, "Alpha Project", "https://example.com/alpha.git");
        var otherProject = AddProject(db, otherTeam, otherOwner, "Other", "https://example.com/other.git");
        var time = new MutableTimeProvider(Now);
        var challenge = AddChallenge(db, setter, Now.AddHours(-1), Now.AddHours(1));
        db.SaveChanges();
        var service = ChallengeService(db, owner, time);
        Assert.Equal("Team project is invalid for this team.",
            (await service.RegisterTeamAsync(challenge.Id, new RegisterChallengeTeamRequest { SelectedTeamProjectId = otherProject.Id })).ErrorMessage);
        var result = await service.RegisterTeamAsync(challenge.Id, new RegisterChallengeTeamRequest { SelectedTeamProjectId = project.Id });
        Assert.True(result.IsSuccess);
        Assert.Equal(project.Id, result.Value!.SelectedTeamProjectId);
        Assert.Equal("Alpha Project", result.Value.ProjectName);
        Assert.Equal("https://example.com/alpha.git", result.Value.RepositoryUrl);
    }

    [Theory]
    [InlineData(ChallengeParticipationMode.Individual, true, "Peer review requires team-only participation.")]
    [InlineData(ChallengeParticipationMode.TeamOnly, false, null)]
    public async Task Configuration_RequiresTeamOnlyAndValidDeadline(ChallengeParticipationMode mode, bool enabled, string? expectedError)
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        await db.SaveChangesAsync();
        var request = new CreateChallengeRequest
        {
            Title = "C", StartAt = Now.AddHours(1), EndAt = Now.AddHours(2), ParticipationMode = mode,
            PeerReviewEnabled = enabled, PeerReviewEndAt = enabled ? Now.AddHours(3) : null
        };
        var result = await ChallengeService(db, setter, new MutableTimeProvider(Now)).CreateChallengeAsync(request);
        Assert.Equal(expectedError, result.ErrorMessage);
        Assert.Equal(expectedError is null, result.IsSuccess);
    }

    [Fact]
    public async Task PublishedPeerReviewConfiguration_IsFrozen()
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var challenge = AddChallenge(db, setter, Now.AddHours(1), Now.AddHours(2));
        challenge.IsPublished = true;
        await db.SaveChangesAsync();
        var request = new UpdateChallengeRequest
        {
            Title = challenge.Title, StartAt = challenge.StartAt, EndAt = challenge.EndAt, IsPublished = true,
            ParticipationMode = ChallengeParticipationMode.TeamOnly, PeerReviewEnabled = false
        };
        Assert.Equal("Peer review configuration is locked.",
            (await ChallengeService(db, setter, new MutableTimeProvider(Now)).UpdateChallengeAsync(challenge.Id, request)).ErrorMessage);
    }

    [Theory]
    [InlineData(null, "Peer review deadline is required.")]
    [InlineData("2026-08-29T10:00:00Z", "Peer review deadline must be after challenge end.")]
    public async Task EnabledConfiguration_ValidatesDeadline(string? deadline, string expectedError)
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        await db.SaveChangesAsync();
        var result = await ChallengeService(db, setter, new MutableTimeProvider(Now)).CreateChallengeAsync(new CreateChallengeRequest
        {
            Title = "C", StartAt = Now.AddHours(1), EndAt = Now.AddHours(2), ParticipationMode = ChallengeParticipationMode.TeamOnly,
            PeerReviewEnabled = true, PeerReviewEndAt = deadline is null ? null : DateTimeOffset.Parse(deadline)
        });
        Assert.Equal(expectedError, result.ErrorMessage);
    }

    [Fact]
    public async Task StartedPeerReviewConfiguration_IsFrozen()
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var challenge = AddChallenge(db, setter, Now.AddMinutes(-1), Now.AddHours(1));
        challenge.IsPublished = false;
        await db.SaveChangesAsync();
        var request = new UpdateChallengeRequest { Title = challenge.Title, StartAt = challenge.StartAt, EndAt = challenge.EndAt, ParticipationMode = ChallengeParticipationMode.TeamOnly, PeerReviewEnabled = false };
        Assert.Equal("Peer review configuration is locked.", (await ChallengeService(db, setter, new MutableTimeProvider(Now)).UpdateChallengeAsync(challenge.Id, request)).ErrorMessage);
    }

    [Fact]
    public async Task RegistrationWithoutProject_IsRejectedWhenEnabledAndAllowedWhenDisabled()
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var owner = AddUser(db, "owner");
        AddTeam(db, owner, "Alpha");
        var enabled = AddChallenge(db, setter, Now.AddHours(-1), Now.AddHours(1));
        var disabled = AddChallenge(db, setter, Now.AddHours(-1), Now.AddHours(1));
        disabled.PeerReviewEnabled = false; disabled.PeerReviewEndAt = null;
        await db.SaveChangesAsync();
        var service = ChallengeService(db, owner, new MutableTimeProvider(Now));
        Assert.Equal("Team project is required for peer review.", (await service.RegisterTeamAsync(enabled.Id)).ErrorMessage);
        Assert.True((await service.RegisterTeamAsync(disabled.Id)).IsSuccess);
    }

    [Fact]
    public async Task RegistrationSnapshot_SurvivesProjectRenameUrlChangeAndDeletion()
    {
        await using var db = CreateDb();
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var owner = AddUser(db, "owner");
        var team = AddTeam(db, owner, "Alpha");
        var project = AddProject(db, team, owner, "Original", "https://example.com/original.git");
        var challenge = AddChallenge(db, setter, Now.AddHours(-1), Now.AddHours(1));
        await db.SaveChangesAsync();
        Assert.True((await ChallengeService(db, owner, new MutableTimeProvider(Now)).RegisterTeamAsync(challenge.Id,
            new RegisterChallengeTeamRequest { SelectedTeamProjectId = project.Id })).IsSuccess);
        project.Name = "Renamed"; project.RepositoryUrl = "https://example.com/changed.git";
        await db.SaveChangesAsync();
        var participant = await db.ChallengeTeamParticipants.SingleAsync(row => row.ChallengeId == challenge.Id);
        Assert.Equal("Original", participant.ProjectNameSnapshot);
        Assert.Equal("https://example.com/original.git", participant.RepositoryUrlSnapshot);
        db.TeamProjects.Remove(project);
        await db.SaveChangesAsync();
        Assert.Equal("Original", participant.ProjectNameSnapshot);
        Assert.Equal("https://example.com/original.git", participant.RepositoryUrlSnapshot);
    }

    [Fact]
    public async Task FrozenRosterIdentity_AllowsFormerMemberAndRejectsLaterMember()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var formerMembership = await db.TeamMembers.SingleAsync(member => member.UserId == seed.Participants[0].User.Id);
        formerMembership.IsActive = false;
        var later = AddUser(db, "later");
        db.TeamMembers.Add(new TeamMember { Id = Guid.NewGuid(), TeamId = formerMembership.TeamId, UserId = later.Id, Role = TeamMemberRole.Member, IsActive = true, JoinedAt = Now });
        await db.SaveChangesAsync();
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        Assert.True((await Service(db, seed.Participants[0].User, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id)).IsSuccess);
        Assert.Equal("Forbidden.", (await Service(db, later, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task ReviewerReceivesOnlyOwnAssignmentAndTargetCannotReadIncomingReview()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 3);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var first = await Service(db, seed.Participants[0].User, seed.Time).SubmitAsync(seed.Challenge.Id, ValidReview());
        Assert.True(first.IsSuccess);
        var targetTeamName = first.Value!.TargetTeamName;
        var target = seed.Participants.Single(item => item.Participant.TeamNameSnapshot == targetTeamName);
        var targetWorkspace = await Service(db, target.User, seed.Time).GetMyWorkspaceAsync(seed.Challenge.Id);
        Assert.NotEqual(seed.Participants[0].Participant.TeamNameSnapshot, targetWorkspace.Value!.TargetTeamName);
        Assert.Null(targetWorkspace.Value.Review);
        Assert.Equal("Forbidden.", (await Service(db, target.User, seed.Time).GetAdminAuditAsync(seed.Challenge.Id)).ErrorMessage);
    }

    [Fact]
    public async Task Draft_AllowsPartialCreationAndUpdate()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        var created = await service.SaveDraftAsync(seed.Challenge.Id, new SaveChallengePeerReviewRequest { Strengths = "one" });
        Assert.True(created.IsSuccess);
        Assert.Equal("one", created.Value!.Review!.Strengths);
        var updated = await service.SaveDraftAsync(seed.Challenge.Id, new SaveChallengePeerReviewRequest { Improvements = "two" });
        Assert.True(updated.IsSuccess);
        Assert.Null(updated.Value!.Review!.Strengths);
        Assert.Equal("two", updated.Value.Review.Improvements);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("strengths")]
    [InlineData("improvements")]
    public async Task Submit_RequiresEveryTextField(string missingField)
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        var request = ValidReview();
        if (missingField == "summary") request.Summary = "";
        if (missingField == "strengths") request.Strengths = "";
        if (missingField == "improvements") request.Improvements = "";
        var result = await service.SubmitAsync(seed.Challenge.Id, request);
        Assert.Contains("required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatSubmit_DoesNotChangeSubmittedAt()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        var submitted = await service.SubmitAsync(seed.Challenge.Id, ValidReview());
        var submittedAt = submitted.Value!.Review!.SubmittedAt;
        seed.Time.Now = Now.AddMinutes(1);
        Assert.True((await service.SubmitAsync(seed.Challenge.Id, ValidReview())).IsFailure);
        Assert.Equal(submittedAt, (await service.GetMyWorkspaceAsync(seed.Challenge.Id)).Value!.Review!.SubmittedAt);
    }

    [Fact]
    public async Task AfterDeadline_SubmitIsRejected()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        var service = Service(db, seed.Participants[0].User, seed.Time);
        await service.EnsureAssignmentsAsync();
        seed.Time.Now = Now.AddHours(3);
        Assert.Equal("Peer review deadline has passed.", (await service.SubmitAsync(seed.Challenge.Id, ValidReview())).ErrorMessage);
    }

    [Fact]
    public async Task ReviewSubmission_DoesNotChangeTeamLeaderboard()
    {
        await using var db = CreateDb();
        var seed = SeedEndedChallenge(db, 2);
        await Service(db, seed.Setter, seed.Time).EnsureAssignmentsAsync();
        var challengeService = ChallengeService(db, seed.Participants[0].User, seed.Time);
        var before = await challengeService.GetLeaderboardAsync(seed.Challenge.Id);
        Assert.True((await Service(db, seed.Participants[0].User, seed.Time).SubmitAsync(seed.Challenge.Id, ValidReview())).IsSuccess);
        var after = await challengeService.GetLeaderboardAsync(seed.Challenge.Id);
        Assert.Equal(before.Value!.TeamEntries.Select(item => item.TotalScore), after.Value!.TeamEntries.Select(item => item.TotalScore));
    }

    [Fact]
    public void PublicChallengeAndLeaderboardDtos_DoNotExposeReviews()
    {
        Assert.DoesNotContain(typeof(ChallengeDetailDto).GetProperties(), property => property.Name.Contains("Review", StringComparison.Ordinal) && property.Name != "PeerReviewEnabled" && property.Name != "PeerReviewEndAt" && property.Name != "PeerReviewConfigurationLocked");
        Assert.DoesNotContain(typeof(ChallengeTeamLeaderboardEntryDto).GetProperties(), property => property.Name.Contains("Review", StringComparison.Ordinal));
    }

    [Fact]
    public void FrontendPeerReviewAudit_IsProblemSetterRootOnlyAndReadOnly()
    {
        var root = FindRepositoryRoot();
        var routes = File.ReadAllText(Path.Combine(root, "frontend", "src", "main.tsx"));
        var detail = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "ChallengeDetailPage.tsx"));
        var audit = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "ChallengePeerReviewAuditPage.tsx"));
        Assert.Contains("peer-review-audit", routes);
        Assert.Contains("allowedRoles={[2, 3]}", routes);
        Assert.Contains("canManageContent(currentUser?.role)", detail);
        Assert.Contains("getChallengePeerReviewAdminAudit", audit);
        Assert.DoesNotContain("updateChallenge", audit);
        Assert.DoesNotContain("deleteChallenge", audit);
        Assert.DoesNotContain("saveChallengePeerReviewDraft", audit);
    }

    [Fact]
    public void WorkerSource_RetriesFailuresAndStopsCleanlyWithoutTestDelay()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Services", "ChallengePeerReviewAssignmentWorker.cs"));
        Assert.Contains("catch (Exception exception)", source);
        Assert.Contains("next poll will retry", source);
        Assert.Contains("OperationCanceledException", source);
        Assert.Contains("stoppingToken.IsCancellationRequested", source);
        Assert.DoesNotContain("Task.Delay(TimeSpan.FromSeconds(60)", source);
    }

    [Fact]
    public async Task WorkerFailure_IsRetriedWithInjectedTestInterval()
    {
        var peerReviewService = new WorkerPeerReviewService(failFirst: true, signalAtCall: 2);
        await using var provider = new ServiceCollection().AddSingleton<IChallengePeerReviewService>(peerReviewService).BuildServiceProvider();
        var worker = new ChallengePeerReviewAssignmentWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ChallengePeerReviewAssignmentWorker>.Instance,
            TimeSpan.FromMilliseconds(1));
        await worker.StartAsync(CancellationToken.None);
        var calls = await peerReviewService.Signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);
        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task WorkerCancellation_StopsCleanly()
    {
        var peerReviewService = new WorkerPeerReviewService(failFirst: false, signalAtCall: 1);
        await using var provider = new ServiceCollection().AddSingleton<IChallengePeerReviewService>(peerReviewService).BuildServiceProvider();
        var worker = new ChallengePeerReviewAssignmentWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ChallengePeerReviewAssignmentWorker>.Instance,
            TimeSpan.FromMilliseconds(1));
        await worker.StartAsync(CancellationToken.None);
        await peerReviewService.Signal.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await worker.StopAsync(timeout.Token);
    }

    [Fact]
    public void PublicContractsAndFrontend_DoNotExposeAuditOrScoreIntegration()
    {
        var workspaceProperties = typeof(ChallengePeerReviewWorkspaceDto).GetProperties().Select(property => property.Name).ToList();
        Assert.DoesNotContain("ReviewerRoster", workspaceProperties);
        Assert.DoesNotContain("ReviewerTeam", workspaceProperties);
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "OnlineJudge.Infrastructure", "Challenges", "ChallengePeerReviewService.cs"));
        Assert.DoesNotContain("SeasonScoreService", service);
        Assert.DoesNotContain("ChallengeTeamTaskCompletion", service);
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "pages", "ChallengePeerReviewPage.tsx"));
        Assert.Contains("rel=\"noreferrer noopener\"", page);
        Assert.Contains("共享一份草稿", page);
    }

    [Fact]
    public void WorkerAndMigration_UsePeriodicReconciliationAndExpectedSchemaOnly()
    {
        var root = FindRepositoryRoot();
        var worker = File.ReadAllText(Path.Combine(root, "OnlineJudge.Api", "Services", "ChallengePeerReviewAssignmentWorker.cs"));
        Assert.Contains("PeriodicTimer", worker);
        Assert.Contains("TimeSpan.FromSeconds(60)", worker);
        Assert.Contains("EnsureAssignmentsAsync", worker);
        var migration = Directory.GetFiles(Path.Combine(root, "OnlineJudge.Infrastructure", "Persistence", "Migrations"), "*_AddChallengePeerReviews.cs").Single();
        var source = File.ReadAllText(migration);
        Assert.Contains("PeerReviewEnabled", source);
        Assert.Contains("PeerReviewEndAt", source);
        Assert.Contains("SelectedTeamProjectId", source);
        Assert.Contains("ProjectNameSnapshot", source);
        Assert.Contains("RepositoryUrlSnapshot", source);
        Assert.Contains("ChallengePeerReviewAssignments", source);
        Assert.Contains("ChallengePeerReviews", source);
    }

    private static Seed SeedEndedChallenge(OnlineJudgeDbContext db, int teamCount, int membersPerTeam = 1)
    {
        var setter = AddUser(db, "setter", UserRole.ProblemSetter);
        var challenge = AddChallenge(db, setter, Now.AddHours(-2), Now.AddHours(-1));
        var participants = new List<ParticipantSeed>();
        for (var index = 0; index < teamCount; index++)
        {
            var user = AddUser(db, $"owner-{index}");
            var otherUsers = Enumerable.Range(1, membersPerTeam - 1).Select(member => AddUser(db, $"member-{index}-{member}")).ToList();
            var team = AddTeam(db, user, $"Team {index + 1}");
            var participant = new ChallengeTeamParticipant
            {
                Id = Guid.NewGuid(), ChallengeId = challenge.Id, TeamId = team.Id, RegisteredByUserId = user.Id,
                TeamNameSnapshot = team.Name, ProjectNameSnapshot = $"Project {index + 1}",
                RepositoryUrlSnapshot = $"https://example.com/team-{index + 1}.git", RegisteredAt = Now.AddMinutes(index)
            };
            db.ChallengeTeamParticipants.Add(participant);
            db.ChallengeTeamRosterMembers.Add(new ChallengeTeamRosterMember { Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTeamParticipantId = participant.Id, UserId = user.Id, UserNameSnapshot = user.UserName, TeamMemberRoleSnapshot = TeamMemberRole.Owner });
            foreach (var member in otherUsers)
            {
                db.ChallengeTeamRosterMembers.Add(new ChallengeTeamRosterMember { Id = Guid.NewGuid(), ChallengeId = challenge.Id, ChallengeTeamParticipantId = participant.Id, UserId = member.Id, UserNameSnapshot = member.UserName, TeamMemberRoleSnapshot = TeamMemberRole.Member });
            }
            participants.Add(new ParticipantSeed(user, otherUsers, participant));
        }
        db.SaveChanges();
        return new Seed(setter, challenge, participants, new MutableTimeProvider(Now));
    }

    private static Challenge AddChallenge(OnlineJudgeDbContext db, User setter, DateTimeOffset startAt, DateTimeOffset endAt)
    {
        var challenge = new Challenge { Id = Guid.NewGuid(), Title = "Peer", Description = "", StartAt = startAt, EndAt = endAt, PeerReviewEndAt = endAt.AddHours(2), PeerReviewEnabled = true, ParticipationMode = ChallengeParticipationMode.TeamOnly, IsPublished = true, CreatedByUserId = setter.Id, CreatedAt = Now, UpdatedAt = Now };
        db.Challenges.Add(challenge);
        return challenge;
    }

    private static Team AddTeam(OnlineJudgeDbContext db, User owner, string name)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant(), OwnerUserId = owner.Id, CreatedAt = Now, UpdatedAt = Now };
        db.Teams.Add(team);
        db.TeamMembers.Add(new TeamMember { Id = Guid.NewGuid(), TeamId = team.Id, UserId = owner.Id, Role = TeamMemberRole.Owner, IsActive = true, JoinedAt = Now });
        return team;
    }

    private static TeamProject AddProject(OnlineJudgeDbContext db, Team team, User owner, string name, string repositoryUrl)
    {
        var project = new TeamProject { Id = Guid.NewGuid(), TeamId = team.Id, Name = name, RepositoryUrl = repositoryUrl, CreatedByUserId = owner.Id, CreatedAt = Now, UpdatedAt = Now };
        db.TeamProjects.Add(project);
        return project;
    }

    private static User AddUser(OnlineJudgeDbContext db, string name, UserRole role = UserRole.Answerer)
    {
        var user = new User { Id = Guid.NewGuid(), UserName = name, Email = $"{name}@test", PasswordHash = "hash", Role = role, CreatedAt = Now, UpdatedAt = Now };
        db.Users.Add(user);
        return user;
    }

    private static SaveChallengePeerReviewRequest ValidReview() => new() { OverallScore = 4, Summary = "summary", Strengths = "strengths", Improvements = "improvements" };
    private static ChallengePeerReviewService Service(OnlineJudgeDbContext db, User user, TimeProvider time) => new(db, new CurrentUser(user.Id), time);
    private static ChallengeService ChallengeService(OnlineJudgeDbContext db, User user, TimeProvider time) => new(db, new CurrentUser(user.Id), new ContentVisibilityPolicy(time));
    private static string Names(IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IProperty> properties) => string.Join(',', properties.Select(property => property.Name));
    private static OnlineJudgeDbContext CreateDb() => new(new DbContextOptionsBuilder<OnlineJudgeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
    private static string FindRepositoryRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }

    private sealed record Seed(User Setter, Challenge Challenge, List<ParticipantSeed> Participants, MutableTimeProvider Time);
    private sealed record ParticipantSeed(User User, List<User> OtherUsers, ChallengeTeamParticipant Participant);
    private sealed class CurrentUser(Guid id, UserRole? tokenRole = null) : ICurrentUser { public bool IsAuthenticated => true; public Guid? UserId => id; public string? UserName => null; public UserRole? Role => tokenRole; }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class WorkerPeerReviewService(bool failFirst, int signalAtCall) : IChallengePeerReviewService
    {
        private int calls;
        public TaskCompletionSource<int> Signal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task EnsureAssignmentsAsync(CancellationToken cancellationToken = default)
        {
            var currentCall = Interlocked.Increment(ref calls);
            if (currentCall >= signalAtCall) Signal.TrySetResult(currentCall);
            if (failFirst && currentCall == 1) throw new InvalidOperationException("expected test failure");
            return Task.CompletedTask;
        }

        public Task<Result<ChallengePeerReviewWorkspaceDto>> GetMyWorkspaceAsync(Guid challengeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ChallengePeerReviewWorkspaceDto>> SaveDraftAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ChallengePeerReviewWorkspaceDto>> SubmitAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<ChallengePeerReviewAdminSummaryDto>> GetAdminAuditAsync(Guid challengeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
