using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Submissions.Dtos;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Application.Submissions.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;

namespace OnlineJudge.Infrastructure.Submissions;

public class SubmissionService(OnlineJudgeDbContext dbContext, IJudgeQueue judgeQueue, ICurrentUser currentUser) : ISubmissionService
{
    public async Task<Result<SubmissionDto>> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<SubmissionDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceCode))
        {
            return Result<SubmissionDto>.Failure("Source code is required.");
        }

        if (!Enum.IsDefined(request.Language))
        {
            return Result<SubmissionDto>.Failure("Unsupported judge language.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == request.ProblemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<SubmissionDto>.Failure("Problem not found.");
        }

        if (problem.JudgeMode == JudgeMode.Function
            && request.Language is not JudgeLanguage.Cpp17 and not JudgeLanguage.CSharp and not JudgeLanguage.C11)
        {
            return Result<SubmissionDto>.Failure("Function mode currently supports C++17, C# and C11 only.");
        }

        if (request.ChallengeTaskId.HasValue)
        {
            var challengeTaskValidation = await ValidateChallengeTaskSubmissionAsync(
                request.ChallengeTaskId.Value,
                request.ProblemId,
                cancellationToken);

            if (challengeTaskValidation.IsFailure)
            {
                return Result<SubmissionDto>.Failure(challengeTaskValidation.ErrorMessage ?? "Invalid challenge task.");
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (request.ChallengeTaskId.HasValue)
        {
            await EnsureParticipantForChallengeTaskAsync(request.ChallengeTaskId.Value, userResult.Value.Id, DateTimeOffset.UtcNow, cancellationToken);
        }

        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = request.ProblemId,
            UserId = userResult.Value.Id,
            ChallengeTaskId = request.ChallengeTaskId,
            Language = request.Language,
            SourceCode = request.SourceCode,
            Status = JudgeStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Submissions.Add(submission);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await judgeQueue.EnqueueSubmissionAsync(submission.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<SubmissionDto>.Failure("Failed to enqueue submission for judging.");
        }

        return Result<SubmissionDto>.Success(ToDto(submission, canViewHiddenCaseResults: false));
    }

    public async Task<Result<SubmissionDto>> GetSubmissionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var submission = await dbContext.Submissions
            .AsNoTracking()
            .Include(submission => submission.Problem)
            .Include(submission => submission.User)
            .Include(submission => submission.CaseResults)
                .ThenInclude(caseResult => caseResult.TestCase)
            .FirstOrDefaultAsync(submission => submission.Id == id, cancellationToken);

        if (submission is null)
        {
            return Result<SubmissionDto>.Failure("Submission not found.");
        }

        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<SubmissionDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (!CanViewSubmission(userResult.Value, submission))
        {
            return Result<SubmissionDto>.Failure("Forbidden.");
        }

        return Result<SubmissionDto>.Success(ToDto(submission, canViewHiddenCaseResults: userResult.Value.Role == UserRole.Root));
    }

    public async Task<Result<PagedResult<SubmissionListItemDto>>> QuerySubmissionsAsync(SubmissionQueryRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<PagedResult<SubmissionListItemDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var user = userResult.Value;
        if (user.Role != UserRole.Root && request.UserId.HasValue && request.UserId.Value != user.Id)
        {
            return Result<PagedResult<SubmissionListItemDto>>.Failure("Forbidden.");
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

        var query = dbContext.Submissions.AsNoTracking();

        if (user.Role == UserRole.Root)
        {
            if (request.Mine == true)
            {
                query = query.Where(submission => submission.UserId == user.Id);
            }
            else if (request.UserId.HasValue)
            {
                query = query.Where(submission => submission.UserId == request.UserId.Value);
            }
        }
        else
        {
            query = query.Where(submission => submission.UserId == user.Id);
        }

        if (request.ProblemId.HasValue)
        {
            query = query.Where(submission => submission.ProblemId == request.ProblemId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(submission => submission.Status == request.Status.Value);
        }

        if (request.Language.HasValue)
        {
            query = query.Where(submission => submission.Language == request.Language.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ProblemKeyword))
        {
            var pattern = $"%{request.ProblemKeyword.Trim()}%";
            query = query.Where(submission => submission.Problem != null && EF.Functions.ILike(submission.Problem.Title, pattern));
        }

        if (user.Role == UserRole.Root && !string.IsNullOrWhiteSpace(request.UserKeyword))
        {
            var pattern = $"%{request.UserKeyword.Trim()}%";
            query = query.Where(submission => submission.User != null && EF.Functions.ILike(submission.User.UserName, pattern));
        }

        if (request.From.HasValue)
        {
            query = query.Where(submission => submission.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(submission => submission.CreatedAt <= request.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(submission => submission.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(submission => new SubmissionListItemDto
            {
                Id = submission.Id,
                ProblemId = submission.ProblemId,
                ProblemTitle = submission.Problem == null ? "题目已删除" : submission.Problem.Title,
                UserId = submission.UserId,
                UserName = submission.User == null ? "未知用户" : submission.User.UserName,
                Language = submission.Language,
                Status = submission.Status,
                TimeUsedMs = submission.TimeUsedMs,
                MemoryUsedKb = submission.MemoryUsedKb,
                CreatedAt = submission.CreatedAt,
                FinishedAt = submission.FinishedAt
            })
            .ToListAsync(cancellationToken);

        return Result<PagedResult<SubmissionListItemDto>>.Success(new PagedResult<SubmissionListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<IReadOnlyList<SubmissionListItemDto>>> GetProblemSubmissionsAsync(Guid problemId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<SubmissionListItemDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<IReadOnlyList<SubmissionListItemDto>>.Failure("Problem not found.");
        }

        var query = dbContext.Submissions
            .AsNoTracking()
            .Where(submission => submission.ProblemId == problemId);

        if (userResult.Value.Role != UserRole.Root)
        {
            query = query.Where(submission => submission.UserId == userResult.Value.Id);
        }

        var submissions = await query
            .OrderByDescending(submission => submission.CreatedAt)
            .Select(submission => new SubmissionListItemDto
            {
                Id = submission.Id,
                ProblemId = submission.ProblemId,
                ProblemTitle = problem.Title,
                UserId = submission.UserId,
                UserName = submission.User == null ? "未知用户" : submission.User.UserName,
                Language = submission.Language,
                Status = submission.Status,
                TimeUsedMs = submission.TimeUsedMs,
                MemoryUsedKb = submission.MemoryUsedKb,
                CreatedAt = submission.CreatedAt,
                FinishedAt = submission.FinishedAt
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<SubmissionListItemDto>>.Success(submissions);
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

    private static bool CanViewSubmission(User user, Submission submission)
    {
        return user.Role == UserRole.Root
            || submission.UserId == user.Id;
    }

    private static SubmissionDto ToDto(Submission submission, bool canViewHiddenCaseResults)
    {
        return new SubmissionDto
        {
            Id = submission.Id,
            ProblemId = submission.ProblemId,
            ProblemTitle = submission.Problem?.Title ?? "题目已删除",
            UserId = submission.UserId,
            UserName = submission.User?.UserName ?? "未知用户",
            ChallengeTaskId = submission.ChallengeTaskId,
            Language = submission.Language,
            SourceCode = submission.SourceCode,
            Status = submission.Status,
            TimeUsedMs = submission.TimeUsedMs,
            MemoryUsedKb = submission.MemoryUsedKb,
            ErrorMessage = submission.ErrorMessage,
            CreatedAt = submission.CreatedAt,
            FinishedAt = submission.FinishedAt,
            CaseResults = submission.CaseResults
                .Select(caseResult => ToCaseResultDto(caseResult, canViewHiddenCaseResults))
                .ToList()
        };
    }

    private static SubmissionCaseResultDto ToCaseResultDto(SubmissionCaseResult caseResult, bool canViewHiddenCaseResults)
    {
        var isHidden = caseResult.TestCase?.Visibility == TestCaseVisibility.Hidden;
        var isRedacted = isHidden && !canViewHiddenCaseResults;

        return new SubmissionCaseResultDto
        {
            Id = caseResult.Id,
            SubmissionId = caseResult.SubmissionId,
            TestCaseId = caseResult.TestCaseId,
            Status = caseResult.Status,
            TimeUsedMs = caseResult.TimeUsedMs,
            MemoryUsedKb = caseResult.MemoryUsedKb,
            ActualOutput = isRedacted ? null : caseResult.ActualOutput,
            ExpectedOutput = isRedacted ? null : caseResult.TestCase?.ExpectedJson ?? caseResult.TestCase?.ExpectedOutput,
            ErrorMessage = isRedacted && !string.IsNullOrWhiteSpace(caseResult.ErrorMessage)
                ? "隐藏测试点错误详情已脱敏。"
                : caseResult.ErrorMessage,
            IsHidden = isHidden,
            IsRedacted = isRedacted
        };
    }

    private async Task<Result> ValidateChallengeTaskSubmissionAsync(Guid challengeTaskId, Guid problemId, CancellationToken cancellationToken)
    {
        var task = await dbContext.ChallengeTasks
            .AsNoTracking()
            .Include(task => task.Challenge)
            .FirstOrDefaultAsync(task => task.Id == challengeTaskId, cancellationToken);

        if (task is null || task.Challenge is null)
        {
            return Result.Failure("Challenge task not found.");
        }

        if (task.TaskType != ChallengeTaskType.Algorithm)
        {
            return Result.Failure("Challenge task is not an algorithm task.");
        }

        if (task.AlgorithmProblemId != problemId)
        {
            return Result.Failure("Challenge task does not match problem.");
        }

        var now = DateTimeOffset.UtcNow;
        if (now < task.Challenge.StartAt || now > task.Challenge.EndAt)
        {
            return Result.Failure("Challenge is not open.");
        }

        return Result.Success();
    }

    private async Task EnsureParticipantForChallengeTaskAsync(Guid challengeTaskId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
    {
        var challengeId = await dbContext.ChallengeTasks
            .AsNoTracking()
            .Where(task => task.Id == challengeTaskId)
            .Select(task => (Guid?)task.ChallengeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!challengeId.HasValue)
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "ChallengeParticipants" ("Id", "ChallengeId", "UserId", "JoinedAt")
             VALUES ({Guid.NewGuid()}, {challengeId.Value}, {userId}, {joinedAt})
             ON CONFLICT ("ChallengeId", "UserId") DO NOTHING;
             """,
            cancellationToken);
    }
}
