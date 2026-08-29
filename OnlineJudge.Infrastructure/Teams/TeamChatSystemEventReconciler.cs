using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Teams.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Teams;

public sealed class TeamChatSystemEventReconciler(
    OnlineJudgeDbContext dbContext,
    TimeProvider timeProvider) : ITeamChatSystemEventReconciler
{
    private const int CandidateLimit = 100;
    private static readonly TimeSpan CandidateWindow = TimeSpan.FromDays(7);

    public async Task ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        await ReconcileChallengeCompletionsAsync(now, cancellationToken);
        await ReconcilePeerReviewsAsync(now, cancellationToken);
    }

    private async Task ReconcileChallengeCompletionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - CandidateWindow;
        var participants = await dbContext.ChallengeTeamParticipants.AsNoTracking()
            .Include(participant => participant.Challenge).ThenInclude(challenge => challenge!.Tasks)
            .Include(participant => participant.TaskCompletions).ThenInclude(completion => completion.ContributorUser)
            .Where(participant => participant.Challenge!.ParticipationMode == ChallengeParticipationMode.TeamOnly
                && participant.Challenge.EndAt >= cutoff
                && participant.Challenge.StartAt <= now)
            .OrderByDescending(participant => participant.Challenge!.EndAt)
            .ToListAsync(cancellationToken);

        var challengeIds = participants.Select(participant => participant.ChallengeId).Distinct().ToList();
        HashSet<string> existingKeys = challengeIds.Count == 0
            ? []
            : await dbContext.TeamChatMessages.AsNoTracking()
                .Where(message => message.EventKey != null && message.RelatedChallengeId != null
                    && challengeIds.Contains(message.RelatedChallengeId.Value))
                .Select(message => message.EventKey!)
                .ToHashSetAsync(cancellationToken);

        foreach (var participant in participants
            .Where(participant => !existingKeys.Contains(ChallengeCompletedKey(participant)))
            .Take(CandidateLimit))
        {
            var taskCount = participant.Challenge!.Tasks.Count;
            var completed = participant.TaskCompletions.Where(completion => completion.IsCompleted).ToList();
            if (taskCount == 0 || completed.Select(completion => completion.ChallengeTaskId).Distinct().Count() != taskCount) continue;
            var finalCompletion = completed.OrderByDescending(completion => completion.CompletedAt)
                .ThenByDescending(completion => completion.Id)
                .First();
            var contributor = finalCompletion.ContributorUser?.UserName;
            if (string.IsNullOrWhiteSpace(contributor)) contributor = "队员";
            await TryAddSystemMessageAsync(new TeamChatMessage
            {
                Id = Guid.NewGuid(),
                TeamId = participant.TeamId,
                Type = TeamChatMessageType.System,
                Content = $"{contributor} 已提交，{participant.TeamNameSnapshot} 已完成挑战",
                RelatedChallengeId = participant.ChallengeId,
                EventKey = ChallengeCompletedKey(participant),
                CreatedAt = now
            }, cancellationToken);
        }
    }

    private async Task ReconcilePeerReviewsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - CandidateWindow;
        var assignments = await dbContext.ChallengePeerReviewAssignments.AsNoTracking()
            .Include(assignment => assignment.Challenge)
            .Include(assignment => assignment.ReviewerParticipant)
            .Where(assignment => assignment.Challenge!.ParticipationMode == ChallengeParticipationMode.TeamOnly
                && assignment.Challenge.PeerReviewEnabled
                && assignment.Challenge.EndAt <= now
                && assignment.Challenge.EndAt >= cutoff)
            .OrderByDescending(assignment => assignment.Challenge!.EndAt)
            .ToListAsync(cancellationToken);

        var challengeIds = assignments.Select(assignment => assignment.ChallengeId).Distinct().ToList();
        HashSet<string> existingKeys = challengeIds.Count == 0
            ? []
            : await dbContext.TeamChatMessages.AsNoTracking()
                .Where(message => message.EventKey != null && message.RelatedChallengeId != null
                    && challengeIds.Contains(message.RelatedChallengeId.Value))
                .Select(message => message.EventKey!)
                .ToHashSetAsync(cancellationToken);

        foreach (var assignment in assignments
            .Where(assignment => !existingKeys.Contains(PeerReviewReadyKey(assignment)))
            .Take(CandidateLimit))
        {
            await TryAddSystemMessageAsync(new TeamChatMessage
            {
                Id = Guid.NewGuid(),
                TeamId = assignment.ReviewerParticipant!.TeamId,
                Type = TeamChatMessageType.System,
                Content = "挑战已结束，互评任务已发布",
                RelatedChallengeId = assignment.ChallengeId,
                RelatedPeerReviewAssignmentId = assignment.Id,
                EventKey = PeerReviewReadyKey(assignment),
                CreatedAt = now
            }, cancellationToken);
        }
    }

    private async Task TryAddSystemMessageAsync(TeamChatMessage message, CancellationToken cancellationToken)
    {
        if (await dbContext.TeamChatMessages.AsNoTracking()
            .AnyAsync(existing => existing.EventKey == message.EventKey, cancellationToken)) return;
        dbContext.TeamChatMessages.Add(message);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(message).State = EntityState.Detached;
            if (!await dbContext.TeamChatMessages.AsNoTracking()
                .AnyAsync(existing => existing.EventKey == message.EventKey, cancellationToken)) throw;
        }
    }

    private static string ChallengeCompletedKey(ChallengeTeamParticipant participant) =>
        $"challenge-completed:{participant.ChallengeId}:{participant.Id}";

    private static string PeerReviewReadyKey(ChallengePeerReviewAssignment assignment) =>
        $"peer-review-ready:{assignment.ChallengeId}:{assignment.ReviewerParticipantId}:{assignment.Id}";
}
