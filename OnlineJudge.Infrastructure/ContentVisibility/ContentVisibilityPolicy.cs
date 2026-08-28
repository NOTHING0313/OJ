using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.ContentVisibility;

public sealed class ContentVisibilityPolicy(TimeProvider timeProvider)
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public IQueryable<Challenge> ApplyChallengeVisibility(IQueryable<Challenge> query, UserRole? role)
    {
        if (CanManageContent(role))
        {
            return query;
        }

        var now = UtcNow;
        return query.Where(challenge => challenge.IsPublished && challenge.StartAt <= now);
    }

    public IQueryable<Problem> ApplyProblemVisibility(IQueryable<Problem> query, UserRole? role)
    {
        if (CanManageContent(role))
        {
            return query;
        }

        var now = UtcNow;
        return query.Where(problem => problem.IsPublished
            && !problem.ChallengeTasks.Any(task => task.Challenge != null
                && task.Challenge.IsPublished
                && now < task.Challenge.StartAt));
    }

    public bool CanViewChallenge(UserRole? role, Challenge challenge)
    {
        return CanManageContent(role)
            || challenge.IsPublished && challenge.StartAt <= UtcNow;
    }

    public bool IsChallengeOpen(Challenge challenge)
    {
        var now = UtcNow;
        return now >= challenge.StartAt && now <= challenge.EndAt;
    }

    private static bool CanManageContent(UserRole? role)
    {
        return role is UserRole.ProblemSetter or UserRole.Root;
    }
}
