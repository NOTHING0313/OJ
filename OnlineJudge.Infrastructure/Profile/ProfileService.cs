using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Profile.Dtos;
using OnlineJudge.Application.Profile.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Profile;

public class ProfileService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser) : IProfileService
{
    public async Task<Result<ProfileSummaryDto>> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ProfileSummaryDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        return await BuildProfileAsync(userResult.Value.Id, cancellationToken);
    }

    public async Task<Result<ProfileSummaryDto>> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ProfileSummaryDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role != UserRole.Root)
        {
            return Result<ProfileSummaryDto>.Failure("Forbidden.");
        }

        return await BuildProfileAsync(userId, cancellationToken);
    }

    private async Task<Result<User>> GetActiveCurrentUserAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null)
        {
            return Result<User>.Failure("Unauthorized.");
        }

        if (user.IsBlacklisted)
        {
            return Result<User>.Failure("Account is blacklisted.");
        }

        return Result<User>.Success(user);
    }

    private async Task<Result<ProfileSummaryDto>> BuildProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profileUser = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new ProfileUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                IsBlacklisted = user.IsBlacklisted,
                CreatedAt = user.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profileUser is null)
        {
            return Result<ProfileSummaryDto>.Failure("User not found.");
        }

        var summary = new ProfileSummaryDto
        {
            User = profileUser,
            SubmissionSummary = await BuildSubmissionSummaryAsync(userId, cancellationToken),
            ProblemSummary = await BuildProblemSummaryAsync(userId, cancellationToken),
            LanguageSummary = await BuildLanguageSummaryAsync(userId, cancellationToken),
            ChallengeSummary = await BuildChallengeSummaryAsync(userId, cancellationToken),
            RecentSubmissions = await BuildRecentSubmissionsAsync(userId, cancellationToken),
            RecentChallengeCompletions = await BuildRecentChallengeCompletionsAsync(userId, cancellationToken),
            RecentFileReviews = await BuildRecentFileReviewsAsync(userId, cancellationToken)
        };

        return Result<ProfileSummaryDto>.Success(summary);
    }

    private async Task<SubmissionSummaryDto> BuildSubmissionSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var stats = await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Accepted = group.Count(submission => submission.Status == JudgeStatus.Accepted),
                WrongAnswer = group.Count(submission => submission.Status == JudgeStatus.WrongAnswer),
                CompileError = group.Count(submission => submission.Status == JudgeStatus.CompileError),
                RuntimeError = group.Count(submission => submission.Status == JudgeStatus.RuntimeError),
                SystemError = group.Count(submission => submission.Status == JudgeStatus.SystemError),
                LastSubmittedAt = group.Max(submission => (DateTimeOffset?)submission.CreatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stats is null)
        {
            return new SubmissionSummaryDto();
        }

        return new SubmissionSummaryDto
        {
            TotalSubmissionCount = stats.Total,
            AcceptedSubmissionCount = stats.Accepted,
            WrongAnswerCount = stats.WrongAnswer,
            CompileErrorCount = stats.CompileError,
            RuntimeErrorCount = stats.RuntimeError,
            SystemErrorCount = stats.SystemError,
            AcceptedRate = stats.Total == 0 ? 0 : (double)stats.Accepted / stats.Total,
            LastSubmittedAt = stats.LastSubmittedAt
        };
    }

    private async Task<ProblemSummaryDto> BuildProblemSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var acceptedProblems = await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.UserId == userId && submission.Status == JudgeStatus.Accepted)
            .GroupBy(submission => submission.ProblemId)
            .Select(group => new
            {
                ProblemId = group.Key,
                AcceptedAt = group.Max(submission => submission.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var recentAcceptedRows = acceptedProblems
            .OrderByDescending(row => row.AcceptedAt)
            .Take(5)
            .ToList();

        var problemIds = recentAcceptedRows.Select(row => row.ProblemId).ToList();
        var titles = await dbContext.Problems
            .AsNoTracking()
            .Where(problem => problemIds.Contains(problem.Id))
            .Select(problem => new { problem.Id, problem.Title })
            .ToDictionaryAsync(problem => problem.Id, problem => problem.Title, cancellationToken);

        return new ProblemSummaryDto
        {
            AcceptedProblemCount = acceptedProblems.Count,
            RecentAcceptedProblems = recentAcceptedRows
                .Select(row => new AcceptedProblemDto
                {
                    ProblemId = row.ProblemId,
                    Title = titles.TryGetValue(row.ProblemId, out var title) ? title : "题目已删除",
                    AcceptedAt = row.AcceptedAt
                })
                .ToList()
        };
    }

    private async Task<IReadOnlyList<LanguageSummaryDto>> BuildLanguageSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.UserId == userId)
            .GroupBy(submission => submission.Language)
            .Select(group => new LanguageSummaryDto
            {
                Language = group.Key,
                SubmissionCount = group.Count(),
                AcceptedCount = group.Count(submission => submission.Status == JudgeStatus.Accepted)
            })
            .OrderBy(summary => summary.Language)
            .ToListAsync(cancellationToken);
    }

    private async Task<ChallengeProfileSummaryDto> BuildChallengeSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var participantChallengeIds = await dbContext.ChallengeParticipants
            .AsNoTracking()
            .Where(participant => participant.UserId == userId)
            .Select(participant => participant.ChallengeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var completionChallengeIds = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.UserId == userId)
            .Select(completion => completion.ChallengeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var completionStats = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                CompletedTaskCount = group.Count(completion => completion.IsCompleted),
                TotalScore = group.Sum(completion => completion.Score),
                LastCompletedAt = group.Max(completion => (DateTimeOffset?)completion.UpdatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ChallengeProfileSummaryDto
        {
            ParticipatedChallengeCount = participantChallengeIds.Union(completionChallengeIds).Count(),
            CompletedTaskCount = completionStats?.CompletedTaskCount ?? 0,
            TotalScore = completionStats?.TotalScore ?? 0,
            LastCompletedAt = completionStats?.LastCompletedAt
        };
    }

    private async Task<IReadOnlyList<RecentSubmissionDto>> BuildRecentSubmissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.UserId == userId)
            .OrderByDescending(submission => submission.CreatedAt)
            .Take(10)
            .Select(submission => new RecentSubmissionDto
            {
                Id = submission.Id,
                ProblemId = submission.ProblemId,
                ProblemTitle = submission.Problem == null ? "题目已删除" : submission.Problem.Title,
                Language = submission.Language,
                Status = submission.Status,
                CreatedAt = submission.CreatedAt,
                FinishedAt = submission.FinishedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<RecentChallengeCompletionDto>> BuildRecentChallengeCompletionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.UserId == userId && completion.IsCompleted)
            .OrderByDescending(completion => completion.CompletedAt)
            .Take(10)
            .Select(completion => new RecentChallengeCompletionDto
            {
                ChallengeId = completion.ChallengeId,
                ChallengeTitle = completion.Challenge == null ? "挑战已删除" : completion.Challenge.Title,
                TaskId = completion.ChallengeTaskId,
                TaskTitle = completion.ChallengeTask == null ? "任务已删除" : completion.ChallengeTask.Title,
                Score = completion.Score,
                CompletedAt = completion.CompletedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<RecentFileReviewDto>> BuildRecentFileReviewsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.ChallengeTaskFileSubmissions
            .AsNoTracking()
            .Where(submission => submission.UserId == userId)
            .OrderByDescending(submission => submission.ReviewedAt.HasValue)
            .ThenByDescending(submission => submission.ReviewedAt)
            .ThenByDescending(submission => submission.CreatedAt)
            .Take(10)
            .Select(submission => new RecentFileReviewDto
            {
                ChallengeId = submission.ChallengeId,
                ChallengeTitle = submission.Challenge == null ? "挑战已删除" : submission.Challenge.Title,
                TaskId = submission.ChallengeTaskId,
                TaskTitle = submission.ChallengeTask == null ? "任务已删除" : submission.ChallengeTask.Title,
                ReviewScore = submission.ReviewScore,
                ReviewComment = submission.ReviewComment,
                ReviewedAt = submission.ReviewedAt,
                SubmittedAt = submission.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
