using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Application.Challenges.Requests;
using OnlineJudge.Application.Challenges.Services;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Challenges;

public class ChallengePeerReviewService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<ChallengePeerReviewService> logger) : IChallengePeerReviewService
{
    public ChallengePeerReviewService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, TimeProvider timeProvider)
        : this(dbContext, currentUser, timeProvider, NullLogger<ChallengePeerReviewService>.Instance)
    {
    }

    public async Task EnsureAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var challengeIds = await dbContext.Challenges.AsNoTracking()
            .Where(challenge => challenge.ParticipationMode == ChallengeParticipationMode.TeamOnly
                && challenge.PeerReviewEnabled && challenge.EndAt <= now)
            .Select(challenge => challenge.Id)
            .ToListAsync(cancellationToken);

        foreach (var challengeId in challengeIds)
        {
            try
            {
                await EnsureChallengeAssignmentsAsync(challengeId, now, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Challenge peer review assignment generation failed. ChallengeId={ChallengeId}", challengeId);
            }
        }
    }

    public async Task<Result<ChallengePeerReviewWorkspaceDto>> GetMyWorkspaceAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var access = await GetReviewerAccessAsync(challengeId, cancellationToken);
        if (access.IsFailure || access.Value is null)
        {
            return Result<ChallengePeerReviewWorkspaceDto>.Failure(access.ErrorMessage!);
        }

        var assignment = await LoadAssignmentAsync(challengeId, access.Value.ParticipantId, cancellationToken);
        if (assignment is null)
        {
            var participantCount = await dbContext.ChallengeTeamParticipants.AsNoTracking()
                .CountAsync(participant => participant.ChallengeId == challengeId, cancellationToken);
            return Result<ChallengePeerReviewWorkspaceDto>.Success(new ChallengePeerReviewWorkspaceDto
            {
                AssignmentReady = false,
                InsufficientTeams = participantCount < 2,
                IsExpired = timeProvider.GetUtcNow() > access.Value.PeerReviewEndAt,
                PeerReviewEndAt = access.Value.PeerReviewEndAt
            });
        }

        return Result<ChallengePeerReviewWorkspaceDto>.Success(ToWorkspace(assignment, access.Value.PeerReviewEndAt));
    }

    public Task<Result<ChallengePeerReviewWorkspaceDto>> SaveDraftAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(challengeId, request, submit: false, cancellationToken);

    public Task<Result<ChallengePeerReviewWorkspaceDto>> SubmitAsync(Guid challengeId, SaveChallengePeerReviewRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(challengeId, request, submit: true, cancellationToken);

    public async Task<Result<ChallengePeerReviewAdminSummaryDto>> GetAdminAuditAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ChallengePeerReviewAdminSummaryDto>.Failure(userResult.ErrorMessage!);
        }

        var challenge = await dbContext.Challenges.AsNoTracking().FirstOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        if (challenge is null) return Result<ChallengePeerReviewAdminSummaryDto>.Failure("Challenge not found.");
        if (userResult.Value.Role is not UserRole.ProblemSetter and not UserRole.Root)
        {
            return Result<ChallengePeerReviewAdminSummaryDto>.Failure("Forbidden.");
        }

        var assignments = await dbContext.ChallengePeerReviewAssignments.AsNoTracking()
            .Where(assignment => assignment.ChallengeId == challengeId)
            .Include(assignment => assignment.ReviewerParticipant).ThenInclude(participant => participant!.RosterMembers)
            .Include(assignment => assignment.Review)
            .OrderBy(assignment => assignment.CreatedAt)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);
        var rows = assignments.Select(assignment => new ChallengePeerReviewAdminDto
        {
            AssignmentId = assignment.Id,
            ReviewerTeam = assignment.ReviewerTeamNameSnapshot,
            TargetTeam = assignment.TargetTeamNameSnapshot,
            TargetProjectName = assignment.TargetProjectNameSnapshot,
            TargetRepositoryUrl = assignment.TargetRepositoryUrlSnapshot,
            ReviewStatus = assignment.Review?.Status,
            OverallScore = assignment.Review?.OverallScore,
            Summary = assignment.Review?.Summary,
            Strengths = assignment.Review?.Strengths,
            Improvements = assignment.Review?.Improvements,
            SubmittedAt = assignment.Review?.SubmittedAt,
            ReviewerRoster = assignment.ReviewerParticipant?.RosterMembers
                .OrderBy(member => member.UserNameSnapshot).Select(member => member.UserNameSnapshot).ToList() ?? []
        }).ToList();
        return Result<ChallengePeerReviewAdminSummaryDto>.Success(new ChallengePeerReviewAdminSummaryDto
        {
            AssignmentCount = rows.Count,
            SubmittedCount = rows.Count(row => row.ReviewStatus == ChallengePeerReviewStatus.Submitted),
            Assignments = rows
        });
    }

    private async Task EnsureChallengeAssignmentsAsync(Guid challengeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        if (await dbContext.ChallengePeerReviewAssignments.AnyAsync(assignment => assignment.ChallengeId == challengeId, cancellationToken))
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return;
        }

        var participants = await dbContext.ChallengeTeamParticipants
            .Where(participant => participant.ChallengeId == challengeId)
            .OrderBy(participant => participant.RegisteredAt)
            .ThenBy(participant => participant.Id)
            .ToListAsync(cancellationToken);
        if (participants.Count < 2)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (participants.Any(participant => string.IsNullOrWhiteSpace(participant.ProjectNameSnapshot)
            || string.IsNullOrWhiteSpace(participant.RepositoryUrlSnapshot)))
        {
            throw new InvalidOperationException($"Peer review participant project snapshot is missing for challenge {challengeId}.");
        }

        for (var index = 0; index < participants.Count; index++)
        {
            var reviewer = participants[index];
            var target = participants[(index + 1) % participants.Count];
            dbContext.ChallengePeerReviewAssignments.Add(new ChallengePeerReviewAssignment
            {
                Id = Guid.NewGuid(),
                ChallengeId = challengeId,
                ReviewerParticipantId = reviewer.Id,
                TargetParticipantId = target.Id,
                ReviewerTeamNameSnapshot = reviewer.TeamNameSnapshot,
                TargetTeamNameSnapshot = target.TeamNameSnapshot,
                TargetProjectNameSnapshot = target.ProjectNameSnapshot!,
                TargetRepositoryUrlSnapshot = target.RepositoryUrlSnapshot!,
                CreatedAt = now
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Result<ChallengePeerReviewWorkspaceDto>> SaveAsync(Guid challengeId, SaveChallengePeerReviewRequest request, bool submit, CancellationToken cancellationToken)
    {
        var access = await GetReviewerAccessAsync(challengeId, cancellationToken);
        if (access.IsFailure || access.Value is null) return Result<ChallengePeerReviewWorkspaceDto>.Failure(access.ErrorMessage!);
        var now = timeProvider.GetUtcNow();
        if (now > access.Value.PeerReviewEndAt) return Result<ChallengePeerReviewWorkspaceDto>.Failure("Peer review deadline has passed.");

        var assignment = await LoadAssignmentAsync(challengeId, access.Value.ParticipantId, cancellationToken);
        if (assignment is null) return Result<ChallengePeerReviewWorkspaceDto>.Failure("Peer review assignment is not ready.");
        if (assignment.Review?.Status == ChallengePeerReviewStatus.Submitted)
        {
            return Result<ChallengePeerReviewWorkspaceDto>.Failure("Peer review has already been submitted.");
        }

        var validation = ValidateReview(request, submit);
        if (validation.IsFailure) return Result<ChallengePeerReviewWorkspaceDto>.Failure(validation.ErrorMessage!);
        var isNewReview = assignment.Review is null;
        var review = assignment.Review ?? new ChallengePeerReview
        {
            Id = Guid.NewGuid(), AssignmentId = assignment.Id, ChallengeId = challengeId,
            ReviewerParticipantId = assignment.ReviewerParticipantId, TargetParticipantId = assignment.TargetParticipantId,
            Status = ChallengePeerReviewStatus.Draft
        };
        ApplyReview(review, request, submit, now);
        if (isNewReview) dbContext.ChallengePeerReviews.Add(review);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewReview)
        {
            dbContext.ChangeTracker.Clear();
            assignment = await LoadAssignmentAsync(challengeId, access.Value.ParticipantId, cancellationToken);
            if (assignment?.Review is null) throw;
            if (assignment.Review.Status == ChallengePeerReviewStatus.Submitted)
            {
                return Result<ChallengePeerReviewWorkspaceDto>.Failure("Peer review has already been submitted.");
            }
            review = assignment.Review;
            ApplyReview(review, request, submit, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        assignment.Review = review;
        return Result<ChallengePeerReviewWorkspaceDto>.Success(ToWorkspace(assignment, access.Value.PeerReviewEndAt));
    }

    private async Task<Result<ReviewerAccess>> GetReviewerAccessAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var userResult = await GetCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ReviewerAccess>.Failure(userResult.ErrorMessage!);
        }
        var userId = userResult.Value.Id;
        var challenge = await dbContext.Challenges.AsNoTracking().FirstOrDefaultAsync(item => item.Id == challengeId, cancellationToken);
        if (challenge is null || challenge.ParticipationMode != ChallengeParticipationMode.TeamOnly || !challenge.PeerReviewEnabled)
        {
            return Result<ReviewerAccess>.Failure("Challenge not found.");
        }
        var now = timeProvider.GetUtcNow();
        if (now < challenge.EndAt) return Result<ReviewerAccess>.Failure("Peer review is not open.");
        var participantId = await dbContext.ChallengeTeamRosterMembers.AsNoTracking()
            .Where(member => member.ChallengeId == challengeId && member.UserId == userId)
            .Select(member => (Guid?)member.ChallengeTeamParticipantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!participantId.HasValue) return Result<ReviewerAccess>.Failure("Forbidden.");
        return Result<ReviewerAccess>.Success(new ReviewerAccess(participantId.Value, challenge.PeerReviewEndAt!.Value));
    }

    private Task<ChallengePeerReviewAssignment?> LoadAssignmentAsync(Guid challengeId, Guid reviewerParticipantId, CancellationToken cancellationToken)
    {
        return dbContext.ChallengePeerReviewAssignments
            .Include(assignment => assignment.Review)
            .FirstOrDefaultAsync(assignment => assignment.ChallengeId == challengeId
                && assignment.ReviewerParticipantId == reviewerParticipantId, cancellationToken);
    }

    private ChallengePeerReviewWorkspaceDto ToWorkspace(ChallengePeerReviewAssignment assignment, DateTimeOffset peerReviewEndAt)
    {
        var expired = timeProvider.GetUtcNow() > peerReviewEndAt;
        return new ChallengePeerReviewWorkspaceDto
        {
            AssignmentReady = true,
            PeerReviewEndAt = peerReviewEndAt,
            IsExpired = expired,
            CanEdit = !expired && assignment.Review?.Status != ChallengePeerReviewStatus.Submitted,
            TargetTeamName = assignment.TargetTeamNameSnapshot,
            TargetProjectName = assignment.TargetProjectNameSnapshot,
            TargetRepositoryUrl = assignment.TargetRepositoryUrlSnapshot,
            Review = assignment.Review is null ? null : new ChallengePeerReviewDto
            {
                Status = assignment.Review.Status,
                OverallScore = assignment.Review.OverallScore,
                Summary = assignment.Review.Summary,
                Strengths = assignment.Review.Strengths,
                Improvements = assignment.Review.Improvements,
                SubmittedAt = assignment.Review.SubmittedAt,
                UpdatedAt = assignment.Review.UpdatedAt
            }
        };
    }

    private static Result ValidateReview(SaveChallengePeerReviewRequest request, bool submit)
    {
        if (request.OverallScore.HasValue && request.OverallScore is < 1 or > 5) return Result.Failure("Overall score must be between 1 and 5.");
        if (request.Summary?.Length > 1000) return Result.Failure("Summary must not exceed 1000 characters.");
        if (request.Strengths?.Length > 2000) return Result.Failure("Strengths must not exceed 2000 characters.");
        if (request.Improvements?.Length > 2000) return Result.Failure("Improvements must not exceed 2000 characters.");
        if (!submit) return Result.Success();
        if (!request.OverallScore.HasValue) return Result.Failure("Overall score is required.");
        if (string.IsNullOrWhiteSpace(request.Summary)) return Result.Failure("Summary is required.");
        if (string.IsNullOrWhiteSpace(request.Strengths)) return Result.Failure("Strengths are required.");
        return string.IsNullOrWhiteSpace(request.Improvements) ? Result.Failure("Improvements are required.") : Result.Success();
    }

    private async Task<Result<User>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId) return Result<User>.Failure("Unauthorized.");
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.IsDeleted) return Result<User>.Failure("Unauthorized.");
        return user.IsBlacklisted ? Result<User>.Failure("Account is blacklisted.") : Result<User>.Success(user);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ApplyReview(ChallengePeerReview review, SaveChallengePeerReviewRequest request, bool submit, DateTimeOffset now)
    {
        review.OverallScore = request.OverallScore;
        review.Summary = Normalize(request.Summary);
        review.Strengths = Normalize(request.Strengths);
        review.Improvements = Normalize(request.Improvements);
        review.UpdatedAt = now;
        if (!submit) return;
        review.Status = ChallengePeerReviewStatus.Submitted;
        review.SubmittedAt = now;
    }

    private sealed record ReviewerAccess(Guid ParticipantId, DateTimeOffset PeerReviewEndAt);
}
