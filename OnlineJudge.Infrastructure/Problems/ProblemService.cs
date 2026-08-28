using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Problems.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.ContentVisibility;

namespace OnlineJudge.Infrastructure.Problems;

public class ProblemService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser, ContentVisibilityPolicy visibilityPolicy) : IProblemService
{
    private const int AllAllowedLanguagesMask = 0b111;
    private const string ProblemReferencedByChallengeTaskMessage = "该题目已被挑战任务引用，请先移除相关挑战任务后再删除。";

    public ProblemService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser)
        : this(dbContext, currentUser, new ContentVisibilityPolicy(TimeProvider.System))
    {
    }

    public async Task<Result<IReadOnlyList<ProblemListItemDto>>> GetProblemsAsync(CancellationToken cancellationToken = default)
    {
        var visibilityRole = await GetVisibilityRoleAsync(cancellationToken);
        var query = dbContext.Problems
            .AsNoTracking()
            .Where(problem => !problem.IsDeleted);

        var problems = await visibilityPolicy.ApplyProblemVisibility(query, visibilityRole)
            .OrderByDescending(problem => problem.CreatedAt)
            .Select(problem => new ProblemListItemDto
            {
                Id = problem.Id,
                Title = problem.Title,
                TimeLimitMs = problem.TimeLimitMs,
                MemoryLimitMb = problem.MemoryLimitMb,
                IsPublished = problem.IsPublished,
                JudgeMode = problem.JudgeMode,
                CreatedAt = problem.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ProblemListItemDto>>.Success(problems);
    }

    public async Task<Result<ProblemDetailDto>> GetProblemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var visibilityRole = await GetVisibilityRoleAsync(cancellationToken);
        var query = dbContext.Problems
            .AsNoTracking()
            .Where(problem => !problem.IsDeleted);

        var problem = await visibilityPolicy.ApplyProblemVisibility(query, visibilityRole)
            .Include(problem => problem.TestCases.Where(testCase => !testCase.IsDeleted))
            .FirstOrDefaultAsync(problem => problem.Id == id && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ProblemDetailDto>.Failure("Problem not found.");
        }

        var includeHiddenTestCases = await CanViewAllTestCasesForCurrentUserAsync(problem, cancellationToken);

        return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases));
    }

    public async Task<Result<ProblemDetailDto>> CreateProblemAsync(CreateProblemRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ProblemDetailDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        if (userResult.Value.Role is not (UserRole.ProblemSetter or UserRole.Root))
        {
            return Result<ProblemDetailDto>.Failure("Forbidden.");
        }

        var validation = ValidateProblemRequest(request.JudgeMode, request.AllowedLanguagesMask, request.FunctionSpecJson, request.StarterCodeJson);
        if (validation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(validation.ErrorMessage!);
        }

        var now = DateTimeOffset.UtcNow;
        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            InputDescription = request.InputDescription,
            OutputDescription = request.OutputDescription,
            TimeLimitMs = request.TimeLimitMs,
            MemoryLimitMb = request.MemoryLimitMb,
            IsPublished = request.IsPublished,
            JudgeMode = request.JudgeMode,
            AllowedLanguagesMask = request.AllowedLanguagesMask,
            FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null,
            StarterCodeJson = request.JudgeMode == JudgeMode.Function ? request.StarterCodeJson : null,
            CreatedByUserId = userResult.Value.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Problems.Add(problem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases: true));
    }

    public async Task<Result<ProblemDetailDto>> UpdateProblemAsync(Guid id, UpdateProblemRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ProblemDetailDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .Include(problem => problem.TestCases.Where(testCase => !testCase.IsDeleted))
            .FirstOrDefaultAsync(problem => problem.Id == id && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ProblemDetailDto>.Failure("Problem not found.");
        }

        if (!await CanEditProblemAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<ProblemDetailDto>.Failure("Forbidden.");
        }

        var validation = ValidateProblemRequest(request.JudgeMode, request.AllowedLanguagesMask, request.FunctionSpecJson, request.StarterCodeJson);
        if (validation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(validation.ErrorMessage!);
        }

        problem.Title = request.Title;
        problem.Description = request.Description;
        problem.InputDescription = request.InputDescription;
        problem.OutputDescription = request.OutputDescription;
        problem.TimeLimitMs = request.TimeLimitMs;
        problem.MemoryLimitMb = request.MemoryLimitMb;
        problem.IsPublished = request.IsPublished;
        problem.JudgeMode = request.JudgeMode;
        problem.AllowedLanguagesMask = request.AllowedLanguagesMask;
        problem.FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null;
        problem.StarterCodeJson = request.JudgeMode == JudgeMode.Function ? request.StarterCodeJson : null;
        problem.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases: true));
    }

    public async Task<Result> DeleteProblemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == id && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result.Failure("Problem not found.");
        }

        if (!CanDeleteProblem(userResult.Value, problem))
        {
            return Result.Failure("Forbidden.");
        }

        var isReferencedByChallengeTask = await dbContext.ChallengeTasks
            .AsNoTracking()
            .AnyAsync(task => task.AlgorithmProblemId == id, cancellationToken);

        if (isReferencedByChallengeTask)
        {
            return Result.Failure(ProblemReferencedByChallengeTaskMessage);
        }

        var now = DateTimeOffset.UtcNow;
        problem.IsDeleted = true;
        problem.IsPublished = false;
        problem.DeletedAt = now;
        problem.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<TestCaseDto>> AddTestCaseAsync(Guid problemId, CreateTestCaseRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<TestCaseDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<TestCaseDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<TestCaseDto>.Failure("Forbidden.");
        }

        var validation = ValidateTestCaseValues(problem, request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson, request.Visibility, request.Score);
        if (validation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(validation.ErrorMessage!);
        }

        var visibility = Enum.IsDefined(request.Visibility) ? request.Visibility : TestCaseVisibility.Hidden;
        var now = DateTimeOffset.UtcNow;

        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Input = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.Input : string.Empty,
            ExpectedOutput = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.ExpectedOutput : string.Empty,
            ArgumentsJson = problem.JudgeMode == JudgeMode.Function ? request.ArgumentsJson : null,
            ExpectedJson = problem.JudgeMode == JudgeMode.Function ? request.ExpectedJson : null,
            Visibility = visibility,
            Score = request.Score,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.TestCases.Add(testCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TestCaseDto>.Success(ToTestCaseDto(testCase));
    }

    public async Task<Result<TestCaseDto>> UpdateTestCaseAsync(Guid problemId, Guid testCaseId, UpdateTestCaseRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<TestCaseDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<TestCaseDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<TestCaseDto>.Failure("Forbidden.");
        }

        var testCase = await dbContext.TestCases
            .FirstOrDefaultAsync(testCase => testCase.Id == testCaseId && testCase.ProblemId == problemId && !testCase.IsDeleted, cancellationToken);

        if (testCase is null)
        {
            return Result<TestCaseDto>.Failure("Test case not found.");
        }

        var validation = ValidateTestCaseValues(problem, request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson, request.Visibility, request.Score);
        if (validation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(validation.ErrorMessage!);
        }

        testCase.Input = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.Input : string.Empty;
        testCase.ExpectedOutput = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.ExpectedOutput : string.Empty;
        testCase.ArgumentsJson = problem.JudgeMode == JudgeMode.Function ? request.ArgumentsJson : null;
        testCase.ExpectedJson = problem.JudgeMode == JudgeMode.Function ? request.ExpectedJson : null;
        testCase.Visibility = Enum.IsDefined(request.Visibility) ? request.Visibility : TestCaseVisibility.Hidden;
        testCase.Score = request.Score;
        testCase.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<TestCaseDto>.Success(ToTestCaseDto(testCase));
    }

    public async Task<Result> DeleteTestCaseAsync(Guid problemId, Guid testCaseId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result.Failure("Forbidden.");
        }

        var testCase = await dbContext.TestCases
            .FirstOrDefaultAsync(testCase => testCase.Id == testCaseId && testCase.ProblemId == problemId && !testCase.IsDeleted, cancellationToken);

        if (testCase is null)
        {
            return Result.Failure("Test case not found.");
        }

        var now = DateTimeOffset.UtcNow;
        testCase.IsDeleted = true;
        testCase.DeletedAt = now;
        testCase.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ImportTestCasesResultDto>> ImportTestCasesAsync(Guid problemId, ImportTestCasesRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ImportTestCasesResultDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ImportTestCasesResultDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<ImportTestCasesResultDto>.Failure("Forbidden.");
        }

        if (request.Items.Count == 0)
        {
            return Result<ImportTestCasesResultDto>.Success(new ImportTestCasesResultDto
            {
                Message = "测试点导入失败。",
                Errors =
                [
                    new ImportTestCaseErrorDto
                    {
                        Index = 0,
                        Field = "items",
                        Message = "至少需要导入一个测试点。"
                    }
                ]
            });
        }

        var now = DateTimeOffset.UtcNow;
        var errors = new List<ImportTestCaseErrorDto>();
        var testCases = new List<TestCase>();

        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            var itemNumber = index + 1;
            var score = item.Score ?? 100;
            var visibility = item.Visibility ?? TestCaseVisibility.Hidden;

            if (score < 0)
            {
                errors.Add(ImportError(itemNumber, "score", "Score cannot be negative."));
            }

            if (!Enum.IsDefined(visibility))
            {
                errors.Add(ImportError(itemNumber, "visibility", "Unsupported test case visibility."));
            }

            if (problem.JudgeMode == JudgeMode.StandardInputOutput)
            {
                ValidateStandardImportItem(item, itemNumber, errors);
            }
            else
            {
                ValidateFunctionImportItem(problem, item, itemNumber, errors);
            }

            if (errors.Count > 0)
            {
                continue;
            }

            testCases.Add(new TestCase
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                Input = problem.JudgeMode == JudgeMode.StandardInputOutput ? item.Input ?? string.Empty : string.Empty,
                ExpectedOutput = problem.JudgeMode == JudgeMode.StandardInputOutput ? item.ExpectedOutput ?? string.Empty : string.Empty,
                ArgumentsJson = problem.JudgeMode == JudgeMode.Function ? ToRawJson(item.ArgumentsJson) : null,
                ExpectedJson = problem.JudgeMode == JudgeMode.Function ? ToRawJson(item.ExpectedJson) : null,
                Visibility = visibility,
                Score = score,
                CreatedAt = now.AddTicks(index),
                UpdatedAt = now.AddTicks(index)
            });
        }

        if (errors.Count > 0)
        {
            return Result<ImportTestCasesResultDto>.Success(new ImportTestCasesResultDto
            {
                Message = "测试点导入失败。",
                Errors = errors
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.TestCases.AddRange(testCases);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ImportTestCasesResultDto>.Success(new ImportTestCasesResultDto
        {
            Message = "测试点导入成功。",
            ImportedCount = testCases.Count,
            Items = testCases
                .Select(testCase => new ImportTestCaseResultItemDto
                {
                    Id = testCase.Id,
                    Score = testCase.Score,
                    Visibility = testCase.Visibility
                })
                .ToList()
        });
    }

    public async Task<Result<IReadOnlyList<TestCaseExportItemDto>>> ExportTestCasesAsync(Guid problemId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<TestCaseExportItemDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .Include(problem => problem.TestCases.Where(testCase => !testCase.IsDeleted))
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<IReadOnlyList<TestCaseExportItemDto>>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<IReadOnlyList<TestCaseExportItemDto>>.Failure("Forbidden.");
        }

        var items = problem.TestCases
            .OrderBy(testCase => testCase.CreatedAt)
            .Select(testCase => ToExportItem(problem.JudgeMode, testCase))
            .ToList();

        return Result<IReadOnlyList<TestCaseExportItemDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<ProblemCollaboratorDto>>> GetCollaboratorsAsync(Guid problemId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<IReadOnlyList<ProblemCollaboratorDto>>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<IReadOnlyList<ProblemCollaboratorDto>>.Failure("Problem not found.");
        }

        if (!await CanViewCollaboratorsAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<IReadOnlyList<ProblemCollaboratorDto>>.Failure("Forbidden.");
        }

        var collaborators = await dbContext.ProblemCollaborators
            .AsNoTracking()
            .Include(collaborator => collaborator.User)
            .Include(collaborator => collaborator.GrantedByUser)
            .Where(collaborator => collaborator.ProblemId == problemId)
            .OrderBy(collaborator => collaborator.CreatedAt)
            .Select(collaborator => ToCollaboratorDto(collaborator))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ProblemCollaboratorDto>>.Success(collaborators);
    }

    public async Task<Result<ProblemCollaboratorDto>> GrantCollaboratorAsync(Guid problemId, GrantProblemCollaboratorRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ProblemCollaboratorDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ProblemCollaboratorDto>.Failure("Problem not found.");
        }

        if (!CanManageCollaborators(userResult.Value, problem))
        {
            return Result<ProblemCollaboratorDto>.Failure("Forbidden.");
        }

        if (request.UserId == problem.CreatedByUserId)
        {
            return Result<ProblemCollaboratorDto>.Failure("Problem owner already has full permissions.");
        }

        var targetUser = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == request.UserId, cancellationToken);

        if (targetUser is null)
        {
            return Result<ProblemCollaboratorDto>.Failure("User not found.");
        }

        if (targetUser.Role != UserRole.ProblemSetter)
        {
            return Result<ProblemCollaboratorDto>.Failure("Only ProblemSetter can be granted problem permissions.");
        }

        if (targetUser.IsBlacklisted)
        {
            return Result<ProblemCollaboratorDto>.Failure("Cannot grant permissions to a blacklisted user.");
        }

        var exists = await dbContext.ProblemCollaborators
            .AsNoTracking()
            .AnyAsync(collaborator => collaborator.ProblemId == problemId && collaborator.UserId == request.UserId, cancellationToken);

        if (exists)
        {
            return Result<ProblemCollaboratorDto>.Failure("Problem collaborator already exists.");
        }

        var collaborator = new ProblemCollaborator
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            UserId = request.UserId,
            GrantedByUserId = userResult.Value.Id,
            CanEditProblem = request.CanEditProblem,
            CanManageTestCases = request.CanManageTestCases,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ProblemCollaborators.Add(collaborator);
        await dbContext.SaveChangesAsync(cancellationToken);

        collaborator.User = targetUser;
        collaborator.GrantedByUser = userResult.Value;

        return Result<ProblemCollaboratorDto>.Success(ToCollaboratorDto(collaborator));
    }

    public async Task<Result> RemoveCollaboratorAsync(Guid problemId, Guid userId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        var problem = await dbContext.Problems
            .AsNoTracking()
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result.Failure("Problem not found.");
        }

        if (!CanManageCollaborators(userResult.Value, problem))
        {
            return Result.Failure("Forbidden.");
        }

        if (userId == problem.CreatedByUserId)
        {
            return Result.Failure("Problem owner cannot be removed as collaborator.");
        }

        var collaborator = await dbContext.ProblemCollaborators
            .FirstOrDefaultAsync(collaborator => collaborator.ProblemId == problemId && collaborator.UserId == userId, cancellationToken);

        if (collaborator is null)
        {
            return Result.Failure("Collaborator not found.");
        }

        dbContext.ProblemCollaborators.Remove(collaborator);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
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

    private async Task<UserRole?> GetVisibilityRoleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return null;
        }

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && !user.IsBlacklisted && !user.IsDeleted)
            .Select(user => (UserRole?)user.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> CanEditProblemAsync(User user, Problem problem, CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.Root || problem.CreatedByUserId == user.Id)
        {
            return true;
        }

        if (user.Role != UserRole.ProblemSetter)
        {
            return false;
        }

        return await dbContext.ProblemCollaborators
            .AsNoTracking()
            .AnyAsync(collaborator =>
                collaborator.ProblemId == problem.Id
                && collaborator.UserId == user.Id
                && collaborator.CanEditProblem,
                cancellationToken);
    }

    private async Task<bool> CanManageTestCasesAsync(User user, Problem problem, CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.Root || problem.CreatedByUserId == user.Id)
        {
            return true;
        }

        if (user.Role != UserRole.ProblemSetter)
        {
            return false;
        }

        return await dbContext.ProblemCollaborators
            .AsNoTracking()
            .AnyAsync(collaborator =>
                collaborator.ProblemId == problem.Id
                && collaborator.UserId == user.Id
                && collaborator.CanManageTestCases,
                cancellationToken);
    }

    private async Task<bool> CanViewAllTestCasesForCurrentUserAsync(Problem problem, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return false;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (user is null || user.IsBlacklisted || user.IsDeleted)
        {
            return false;
        }

        return await CanManageTestCasesAsync(user, problem, cancellationToken);
    }

    private async Task<bool> CanViewCollaboratorsAsync(User user, Problem problem, CancellationToken cancellationToken)
    {
        if (CanManageCollaborators(user, problem))
        {
            return true;
        }

        return await dbContext.ProblemCollaborators
            .AsNoTracking()
            .AnyAsync(collaborator => collaborator.ProblemId == problem.Id && collaborator.UserId == user.Id, cancellationToken);
    }

    private static bool CanDeleteProblem(User user, Problem problem)
    {
        return user.Role == UserRole.Root || problem.CreatedByUserId == user.Id;
    }

    private static bool CanManageCollaborators(User user, Problem problem)
    {
        return user.Role == UserRole.Root || problem.CreatedByUserId == user.Id;
    }

    private static Result ValidateProblemRequest(JudgeMode judgeMode, int allowedLanguagesMask, string? functionSpecJson, string? starterCodeJson)
    {
        if (!Enum.IsDefined(judgeMode))
        {
            return Result.Failure("Unsupported judge mode.");
        }

        if (allowedLanguagesMask < 0 || (allowedLanguagesMask & ~AllAllowedLanguagesMask) != 0)
        {
            return Result.Failure("Unsupported allowed languages mask.");
        }

        if (judgeMode == JudgeMode.StandardInputOutput)
        {
            return Result.Success();
        }

        var specResult = FunctionJudgeSpecParser.Parse(functionSpecJson);
        if (specResult.IsFailure)
        {
            return Result.Failure(specResult.ErrorMessage!);
        }

        var languageValidation = ValidateFunctionAllowedLanguages(allowedLanguagesMask, functionSpecJson);
        if (languageValidation.IsFailure)
        {
            return languageValidation;
        }

        return FunctionJudgeSpecParser.ValidateStarterCode(starterCodeJson);
    }

    private static Result ValidateFunctionAllowedLanguages(int allowedLanguagesMask, string? functionSpecJson)
    {
        if (allowedLanguagesMask == 0 || string.IsNullOrWhiteSpace(functionSpecJson))
        {
            return Result.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(functionSpecJson);
            if (!document.RootElement.TryGetProperty("supportedLanguages", out var supportedLanguages)
                || supportedLanguages.ValueKind != JsonValueKind.Array)
            {
                return Result.Success();
            }

            var supportedMask = 0;
            foreach (var item in supportedLanguages.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                supportedMask |= item.GetString()?.ToLowerInvariant() switch
                {
                    "cpp17" => 0b001,
                    "c11" => 0b010,
                    "csharp" => 0b100,
                    _ => 0
                };
            }

            return (allowedLanguagesMask & ~supportedMask) == 0
                ? Result.Success()
                : Result.Failure("Allowed languages include a language not supported by the function spec.");
        }
        catch (JsonException)
        {
            return Result.Success();
        }
    }

    private static Result ValidateTestCaseValues(
        Problem problem,
        string input,
        string expectedOutput,
        string? argumentsJson,
        string? expectedJson,
        TestCaseVisibility visibility,
        int score)
    {
        if (score < 0) return Result.Failure("Score cannot be negative.");
        if (!Enum.IsDefined(visibility)) return Result.Failure("Unsupported test case visibility.");

        if (problem.JudgeMode == JudgeMode.StandardInputOutput)
        {
            if (!string.IsNullOrWhiteSpace(argumentsJson) || !string.IsNullOrWhiteSpace(expectedJson))
            {
                return Result.Failure("Standard input/output test cases cannot use function JSON fields.");
            }

            return Result.Success();
        }

        if (!string.IsNullOrWhiteSpace(input) || !string.IsNullOrWhiteSpace(expectedOutput))
        {
            return Result.Failure("Function test cases cannot use standard input/output fields.");
        }

        var specResult = FunctionJudgeSpecParser.Parse(problem.FunctionSpecJson);
        if (specResult.IsFailure || specResult.Value is null)
        {
            return Result.Failure(specResult.ErrorMessage ?? "Invalid function spec.");
        }

        return FunctionJudgeSpecParser.ValidateTestCase(specResult.Value, argumentsJson, expectedJson);
    }

    private static ProblemDetailDto ToDetailDto(Problem problem, bool includeHiddenTestCases)
    {
        return new ProblemDetailDto
        {
            Id = problem.Id,
            Title = problem.Title,
            Description = problem.Description,
            InputDescription = problem.InputDescription,
            OutputDescription = problem.OutputDescription,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitMb = problem.MemoryLimitMb,
            IsPublished = problem.IsPublished,
            JudgeMode = problem.JudgeMode,
            AllowedLanguagesMask = problem.AllowedLanguagesMask,
            TotalScore = problem.TestCases.Where(testCase => !testCase.IsDeleted).Sum(testCase => testCase.Score),
            FunctionSpecJson = problem.FunctionSpecJson,
            StarterCodeJson = problem.StarterCodeJson,
            CreatedAt = problem.CreatedAt,
            UpdatedAt = problem.UpdatedAt,
            TestCases = problem.TestCases
                .Where(testCase => !testCase.IsDeleted && (includeHiddenTestCases || testCase.Visibility == TestCaseVisibility.Sample))
                .OrderBy(testCase => testCase.CreatedAt)
                .Select(ToTestCaseDto)
                .ToList()
        };
    }

    private static void ValidateStandardImportItem(ImportTestCaseItemRequest item, int itemNumber, List<ImportTestCaseErrorDto> errors)
    {
        if (item.Input is null)
        {
            errors.Add(ImportError(itemNumber, "input", "Standard input/output test cases require input."));
        }

        if (item.ExpectedOutput is null)
        {
            errors.Add(ImportError(itemNumber, "expectedOutput", "Standard input/output test cases require expectedOutput."));
        }

        if (IsProvidedJson(item.ArgumentsJson))
        {
            errors.Add(ImportError(itemNumber, "argumentsJson", "Standard input/output test cases cannot use argumentsJson."));
        }

        if (IsProvidedJson(item.ExpectedJson))
        {
            errors.Add(ImportError(itemNumber, "expectedJson", "Standard input/output test cases cannot use expectedJson."));
        }
    }

    private static void ValidateFunctionImportItem(Problem problem, ImportTestCaseItemRequest item, int itemNumber, List<ImportTestCaseErrorDto> errors)
    {
        if (!string.IsNullOrWhiteSpace(item.Input))
        {
            errors.Add(ImportError(itemNumber, "input", "Function test cases cannot use input."));
        }

        if (!string.IsNullOrWhiteSpace(item.ExpectedOutput))
        {
            errors.Add(ImportError(itemNumber, "expectedOutput", "Function test cases cannot use expectedOutput."));
        }

        if (!IsProvidedJson(item.ArgumentsJson))
        {
            errors.Add(ImportError(itemNumber, "argumentsJson", "Function test cases require argumentsJson."));
            return;
        }

        if (!IsProvidedJson(item.ExpectedJson))
        {
            errors.Add(ImportError(itemNumber, "expectedJson", "Function test cases require expectedJson."));
            return;
        }

        var specResult = FunctionJudgeSpecParser.Parse(problem.FunctionSpecJson);
        if (specResult.IsFailure || specResult.Value is null)
        {
            errors.Add(ImportError(itemNumber, "functionSpecJson", specResult.ErrorMessage ?? "Invalid function spec."));
            return;
        }

        var validation = FunctionJudgeSpecParser.ValidateTestCase(specResult.Value, ToRawJson(item.ArgumentsJson), ToRawJson(item.ExpectedJson));
        if (validation.IsFailure)
        {
            errors.Add(ImportError(itemNumber, "argumentsJson", validation.ErrorMessage ?? "Invalid function test case."));
        }
    }

    private static ImportTestCaseErrorDto ImportError(int index, string field, string message)
    {
        return new ImportTestCaseErrorDto
        {
            Index = index,
            Field = field,
            Message = message
        };
    }

    private static bool IsProvidedJson(JsonElement? value)
    {
        return value.HasValue && value.Value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
    }

    private static string? ToRawJson(JsonElement? value)
    {
        return IsProvidedJson(value) ? value!.Value.GetRawText() : null;
    }

    private static TestCaseExportItemDto ToExportItem(JudgeMode judgeMode, TestCase testCase)
    {
        if (judgeMode == JudgeMode.StandardInputOutput)
        {
            return new TestCaseExportItemDto
            {
                Input = testCase.Input,
                ExpectedOutput = testCase.ExpectedOutput,
                Score = testCase.Score,
                Visibility = testCase.Visibility
            };
        }

        return new TestCaseExportItemDto
        {
            ArgumentsJson = ParseJsonElement(testCase.ArgumentsJson),
            ExpectedJson = ParseJsonElement(testCase.ExpectedJson),
            Score = testCase.Score,
            Visibility = testCase.Visibility
        };
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static TestCaseDto ToTestCaseDto(TestCase testCase)
    {
        return new TestCaseDto
        {
            Id = testCase.Id,
            ProblemId = testCase.ProblemId,
            Input = testCase.Input,
            ExpectedOutput = testCase.ExpectedOutput,
            ArgumentsJson = testCase.ArgumentsJson,
            ExpectedJson = testCase.ExpectedJson,
            Visibility = testCase.Visibility,
            Score = testCase.Score,
            CreatedAt = testCase.CreatedAt
        };
    }

    private static ProblemCollaboratorDto ToCollaboratorDto(ProblemCollaborator collaborator)
    {
        return new ProblemCollaboratorDto
        {
            Id = collaborator.Id,
            ProblemId = collaborator.ProblemId,
            UserId = collaborator.UserId,
            UserName = collaborator.User?.UserName ?? string.Empty,
            AvatarUrl = collaborator.User?.AvatarUrl,
            GrantedByUserId = collaborator.GrantedByUserId,
            GrantedByUserName = collaborator.GrantedByUser?.UserName ?? string.Empty,
            CanEditProblem = collaborator.CanEditProblem,
            CanManageTestCases = collaborator.CanManageTestCases,
            CreatedAt = collaborator.CreatedAt
        };
    }
}
