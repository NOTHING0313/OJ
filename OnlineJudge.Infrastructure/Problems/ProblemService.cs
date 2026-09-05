using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Application.Problems.Dtos;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Problems.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.SecurityAudit;
using OnlineJudge.Infrastructure.ContentVisibility;

namespace OnlineJudge.Infrastructure.Problems;

public class ProblemService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    ContentVisibilityPolicy visibilityPolicy,
    ISecurityAuditWriter? auditWriter = null,
    JudgeResourcePolicy? resourcePolicy = null) : IProblemService
{
    private JudgeResourcePolicy ResourcePolicy { get; } = resourcePolicy ?? JudgeResourcePolicy.Default;

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
                ProblemKind = problem.ProblemKind,
                TimeLimitMs = problem.TimeLimitMs,
                MemoryLimitMb = problem.MemoryLimitMb,
                IsPublished = problem.IsPublished,
                JudgeMode = problem.JudgeMode,
                AllowedLanguagesMask = problem.AllowedLanguagesMask,
                CreatedAt = problem.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var totalScores = await ProblemScoreQuery.GetTotalsAsync(dbContext, problems.Select(problem => problem.Id), cancellationToken);
        foreach (var problem in problems)
        {
            problem.TotalScore = totalScores.GetValueOrDefault(problem.Id);
        }

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
            .Include(problem => problem.ChoiceQuestions.Where(question => !question.IsDeleted))
                .ThenInclude(question => question.Options.Where(option => !option.IsDeleted))
            .Include(problem => problem.CurrentJudgeRevision)!
                .ThenInclude(revision => revision!.ChoiceQuestions)
                    .ThenInclude(question => question.Options)
            .FirstOrDefaultAsync(problem => problem.Id == id && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ProblemDetailDto>.Failure("Problem not found.");
        }

        var includeHiddenTestCases = await CanViewAllTestCasesForCurrentUserAsync(problem, cancellationToken);

        return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases, usePublishedChoiceRevision: problem.IsPublished));
    }

    public async Task<Result<ProblemDetailDto>> GetProblemAuthoringAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null) return Result<ProblemDetailDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        var problem = await dbContext.Problems.AsNoTracking()
            .Include(item => item.TestCases.Where(testCase => !testCase.IsDeleted))
            .Include(item => item.ChoiceQuestions.Where(question => !question.IsDeleted)).ThenInclude(question => question.Options.Where(option => !option.IsDeleted))
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (problem is null) return Result<ProblemDetailDto>.Failure("Problem not found.");
        var canEdit = await CanEditProblemAsync(userResult.Value, problem, cancellationToken);
        var canManageJudge = await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken);
        if (!canEdit && !canManageJudge) return Result<ProblemDetailDto>.Failure("Forbidden.");
        return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases: canManageJudge));
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

        var validation = ValidateProblemRequest(request.Title, request.Description, request.InputDescription, request.OutputDescription, request.TimeLimitMs, request.MemoryLimitMb, request.ProblemKind, request.JudgeMode, request.AllowedLanguagesMask, request.FunctionSpecJson, request.StarterCodeJson);
        if (validation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(validation.ErrorMessage!);
        }

        if (request.IsPublished)
        {
            return Result<ProblemDetailDto>.Failure(ProblemJudgeRevisionPublisher.NoActiveTestCasesMessage);
        }

        var now = DateTimeOffset.UtcNow;
        var problem = new Problem
        {
            Id = Guid.NewGuid(),
            ProblemKind = request.ProblemKind,
            AuthoringVersion = 1,
            Title = request.Title,
            Description = request.Description,
            InputDescription = request.InputDescription,
            OutputDescription = request.OutputDescription,
            TimeLimitMs = request.ProblemKind == ProblemKind.Programming ? request.TimeLimitMs : null,
            MemoryLimitMb = request.ProblemKind == ProblemKind.Programming ? request.MemoryLimitMb : null,
            IsPublished = false,
            JudgeMode = request.ProblemKind == ProblemKind.Programming ? request.JudgeMode : null,
            AllowedLanguagesMask = request.ProblemKind == ProblemKind.Programming ? request.AllowedLanguagesMask : 0,
            FunctionSpecJson = request.ProblemKind == ProblemKind.Programming && request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null,
            StarterCodeJson = request.ProblemKind == ProblemKind.Programming && request.JudgeMode == JudgeMode.Function ? request.StarterCodeJson : null,
            ChoiceAnswerRevealPolicy = request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealPolicy : null,
            ChoiceAnswerRevealAt = request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealAt : null,
            CreatedByUserId = userResult.Value.Id,
            CreatedAt = now,
            UpdatedAt = now
        };

        var choiceBuild = BuildChoiceQuestions(problem.Id, request.ChoiceQuestions, now, preserveRequestedIds: false);
        if (choiceBuild.IsFailure || choiceBuild.Value is null)
        {
            return Result<ProblemDetailDto>.Failure(choiceBuild.ErrorMessage!);
        }
        problem.ChoiceQuestions = request.ProblemKind == ProblemKind.ChoiceSet ? choiceBuild.Value : [];
        var choiceValidation = ChoiceProblemDefinitionValidator.Validate(problem.ChoiceQuestions, problem.ChoiceAnswerRevealPolicy, problem.ChoiceAnswerRevealAt, requireComplete: false);
        if (choiceValidation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(choiceValidation.ErrorMessage!);
        }

        dbContext.Problems.Add(problem);
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemCreated, "Problem", problem.Id.ToString()));
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

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, id, cancellationToken);

        var problem = await dbContext.Problems
            .Include(problem => problem.TestCases.Where(testCase => !testCase.IsDeleted))
            .Include(problem => problem.ChoiceQuestions.Where(question => !question.IsDeleted))
                .ThenInclude(question => question.Options.Where(option => !option.IsDeleted))
            .FirstOrDefaultAsync(problem => problem.Id == id && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ProblemDetailDto>.Failure("Problem not found.");
        }

        var canEditMetadata = await CanEditProblemAsync(userResult.Value, problem, cancellationToken);
        var canManageJudgeDefinition = await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken);
        if (!canEditMetadata && !canManageJudgeDefinition)
        {
            return Result<ProblemDetailDto>.Failure("Forbidden.");
        }

        if (request.ExpectedAuthoringVersion.HasValue && request.ExpectedAuthoringVersion.Value != problem.AuthoringVersion)
        {
            return Result<ProblemDetailDto>.Failure($"authoring_version_conflict:{problem.AuthoringVersion}");
        }

        if (request.ProblemKind == ProblemKind.ChoiceSet && problem.ProblemKind != ProblemKind.ChoiceSet
            && await dbContext.ChallengeTasks.AsNoTracking().AnyAsync(task => task.AlgorithmProblemId == problem.Id, cancellationToken))
        {
            return Result<ProblemDetailDto>.Failure("Choice problems cannot be bound to algorithm challenge tasks.");
        }

        var validation = ValidateProblemRequest(request.Title, request.Description, request.InputDescription, request.OutputDescription, request.TimeLimitMs, request.MemoryLimitMb, request.ProblemKind, request.JudgeMode, request.AllowedLanguagesMask, request.FunctionSpecJson, request.StarterCodeJson);
        if (validation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(validation.ErrorMessage!);
        }

        var testCaseCollectionValidation = request.ProblemKind == ProblemKind.Programming ? ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(
            request.TimeLimitMs!.Value,
            problem.TestCases.Select(JudgeTestCasePayload.From).ToList(),
            ResourcePolicy,
            requireAtLeastOne: request.IsPublished) : Result.Success();
        if (testCaseCollectionValidation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(testCaseCollectionValidation.ErrorMessage!);
        }

        var choiceBuild = BuildChoiceQuestions(problem.Id, request.ChoiceQuestions, DateTimeOffset.UtcNow, preserveRequestedIds: true);
        if (choiceBuild.IsFailure || choiceBuild.Value is null)
        {
            return Result<ProblemDetailDto>.Failure(choiceBuild.ErrorMessage!);
        }
        var desiredChoiceQuestions = request.ProblemKind == ProblemKind.ChoiceSet ? choiceBuild.Value : [];
        var ownershipValidation = await ValidateChoiceOwnershipAsync(problem.Id, desiredChoiceQuestions, cancellationToken);
        if (ownershipValidation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(ownershipValidation.ErrorMessage!);
        }
        var choiceValidation = ChoiceProblemDefinitionValidator.Validate(
            desiredChoiceQuestions,
            request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealPolicy : null,
            request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealAt : null,
            requireComplete: request.IsPublished && request.ProblemKind == ProblemKind.ChoiceSet);
        if (choiceValidation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(choiceValidation.ErrorMessage!);
        }

        var revealValidation = await ValidateRevealPolicyTransitionAsync(problem, request, cancellationToken);
        if (revealValidation.IsFailure)
        {
            return Result<ProblemDetailDto>.Failure(revealValidation.ErrorMessage!);
        }

        var choiceContentChanged = !ChoiceContentEquals(problem.ChoiceQuestions, desiredChoiceQuestions);
        var judgeDefinitionChanged = problem.ProblemKind != request.ProblemKind
            || problem.JudgeMode != (request.ProblemKind == ProblemKind.Programming ? request.JudgeMode : null)
            || problem.AllowedLanguagesMask != request.AllowedLanguagesMask
            || !string.Equals(problem.FunctionSpecJson, request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null, StringComparison.Ordinal)
            || problem.TimeLimitMs != (request.ProblemKind == ProblemKind.Programming ? request.TimeLimitMs : null)
            || problem.MemoryLimitMb != (request.ProblemKind == ProblemKind.Programming ? request.MemoryLimitMb : null)
            || choiceContentChanged;
        var requiresRevision = request.IsPublished
            && (!problem.IsPublished || problem.CurrentJudgeRevisionId is null || judgeDefinitionChanged);

        var metadataChanged = !string.Equals(problem.Title, request.Title, StringComparison.Ordinal)
            || !string.Equals(problem.Description, request.Description, StringComparison.Ordinal)
            || !string.Equals(problem.InputDescription, request.InputDescription, StringComparison.Ordinal)
            || !string.Equals(problem.OutputDescription, request.OutputDescription, StringComparison.Ordinal);
        var revealPolicyChanged = problem.ChoiceAnswerRevealPolicy != (request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealPolicy : null)
            || problem.ChoiceAnswerRevealAt != (request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealAt : null);
        var publicationChanged = problem.IsPublished != request.IsPublished;
        var authoringChanged = judgeDefinitionChanged || metadataChanged || publicationChanged || revealPolicyChanged;
        if (metadataChanged && !canEditMetadata
            || judgeDefinitionChanged && !canManageJudgeDefinition
            || (publicationChanged || revealPolicyChanged) && !(canEditMetadata && canManageJudgeDefinition))
        {
            return Result<ProblemDetailDto>.Failure("Forbidden.");
        }
        if (!authoringChanged)
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<ProblemDetailDto>.Success(ToDetailDto(problem, includeHiddenTestCases: true));
        }

        problem.Title = request.Title;
        problem.Description = request.Description;
        problem.InputDescription = request.InputDescription;
        problem.OutputDescription = request.OutputDescription;
        problem.ProblemKind = request.ProblemKind;
        problem.TimeLimitMs = request.ProblemKind == ProblemKind.Programming ? request.TimeLimitMs : null;
        problem.MemoryLimitMb = request.ProblemKind == ProblemKind.Programming ? request.MemoryLimitMb : null;
        problem.IsPublished = request.IsPublished;
        problem.JudgeMode = request.ProblemKind == ProblemKind.Programming ? request.JudgeMode : null;
        problem.AllowedLanguagesMask = request.ProblemKind == ProblemKind.Programming ? request.AllowedLanguagesMask : 0;
        problem.FunctionSpecJson = request.ProblemKind == ProblemKind.Programming && request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null;
        problem.StarterCodeJson = request.ProblemKind == ProblemKind.Programming && request.JudgeMode == JudgeMode.Function ? request.StarterCodeJson : null;
        problem.ChoiceAnswerRevealPolicy = request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealPolicy : null;
        problem.ChoiceAnswerRevealAt = request.ProblemKind == ProblemKind.ChoiceSet ? request.ChoiceAnswerRevealAt : null;
        if (choiceContentChanged)
        {
            await ReplaceChoiceQuestionsAsync(problem, desiredChoiceQuestions, cancellationToken);
        }
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = DateTimeOffset.UtcNow;

        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemUpdated, "Problem", problem.Id.ToString()));
        if (requiresRevision)
        {
            var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
            if (revisionResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<ProblemDetailDto>.Failure(revisionResult.ErrorMessage!);
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

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
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemDeleted, "Problem", problem.Id.ToString()));
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

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<TestCaseDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<TestCaseDto>.Failure("Forbidden.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result<TestCaseDto>.Failure("Choice problems do not use judge test cases.");
        }

        var validation = ProblemJudgeDefinitionValidator.ValidateTestCase(problem, request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson, request.Visibility, request.Score, ResourcePolicy);
        if (validation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(validation.ErrorMessage!);
        }

        var prospectiveValidation = await ValidateProspectiveTestCasesAsync(
            problemId,
            problem.TimeLimitMs!.Value,
            [new JudgeTestCasePayload(request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson)],
            excludedTestCaseId: null,
            cancellationToken);
        if (prospectiveValidation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(prospectiveValidation.ErrorMessage!);
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
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemTestCasesChanged, "Problem", problemId.ToString(), Metadata: new Dictionary<string, string?> { ["testCaseCountDelta"] = "1" }));
        if (problem.IsPublished)
        {
            var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
            if (revisionResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<TestCaseDto>.Failure(revisionResult.ErrorMessage!);
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return Result<TestCaseDto>.Success(ToTestCaseDto(testCase));
    }

    public async Task<Result<TestCaseDto>> UpdateTestCaseAsync(Guid problemId, Guid testCaseId, UpdateTestCaseRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<TestCaseDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<TestCaseDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<TestCaseDto>.Failure("Forbidden.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result<TestCaseDto>.Failure("Choice problems do not use judge test cases.");
        }

        var testCase = await dbContext.TestCases
            .FirstOrDefaultAsync(testCase => testCase.Id == testCaseId && testCase.ProblemId == problemId && !testCase.IsDeleted, cancellationToken);

        if (testCase is null)
        {
            return Result<TestCaseDto>.Failure("Test case not found.");
        }

        var validation = ProblemJudgeDefinitionValidator.ValidateTestCase(problem, request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson, request.Visibility, request.Score, ResourcePolicy);
        if (validation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(validation.ErrorMessage!);
        }

        var prospectiveValidation = await ValidateProspectiveTestCasesAsync(
            problemId,
            problem.TimeLimitMs!.Value,
            [new JudgeTestCasePayload(request.Input, request.ExpectedOutput, request.ArgumentsJson, request.ExpectedJson)],
            testCaseId,
            cancellationToken);
        if (prospectiveValidation.IsFailure)
        {
            return Result<TestCaseDto>.Failure(prospectiveValidation.ErrorMessage!);
        }

        testCase.Input = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.Input : string.Empty;
        testCase.ExpectedOutput = problem.JudgeMode == JudgeMode.StandardInputOutput ? request.ExpectedOutput : string.Empty;
        testCase.ArgumentsJson = problem.JudgeMode == JudgeMode.Function ? request.ArgumentsJson : null;
        testCase.ExpectedJson = problem.JudgeMode == JudgeMode.Function ? request.ExpectedJson : null;
        testCase.Visibility = Enum.IsDefined(request.Visibility) ? request.Visibility : TestCaseVisibility.Hidden;
        testCase.Score = request.Score;
        var now = DateTimeOffset.UtcNow;
        testCase.UpdatedAt = now;
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;

        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemTestCasesChanged, "Problem", problemId.ToString(), Metadata: new Dictionary<string, string?> { ["testCaseCountDelta"] = "0" }));
        if (problem.IsPublished)
        {
            var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
            if (revisionResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<TestCaseDto>.Failure(revisionResult.ErrorMessage!);
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Result<TestCaseDto>.Success(ToTestCaseDto(testCase));
    }

    public async Task<Result> DeleteTestCaseAsync(Guid problemId, Guid testCaseId, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result.Failure("Forbidden.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result.Failure("Choice problems do not use judge test cases.");
        }

        var testCase = await dbContext.TestCases
            .FirstOrDefaultAsync(testCase => testCase.Id == testCaseId && testCase.ProblemId == problemId && !testCase.IsDeleted, cancellationToken);

        if (testCase is null)
        {
            return Result.Failure("Test case not found.");
        }

        if (problem.IsPublished)
        {
            var activeTestCaseCount = await dbContext.TestCases.CountAsync(
                item => item.ProblemId == problemId && !item.IsDeleted,
                cancellationToken);
            if (activeTestCaseCount <= 1)
            {
                return Result.Failure(ProblemJudgeRevisionPublisher.NoActiveTestCasesMessage);
            }
        }

        var now = DateTimeOffset.UtcNow;
        testCase.IsDeleted = true;
        testCase.DeletedAt = now;
        testCase.UpdatedAt = now;
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemTestCasesChanged, "Problem", problemId.ToString(), Metadata: new Dictionary<string, string?> { ["testCaseCountDelta"] = "-1" }));
        if (problem.IsPublished)
        {
            var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
            if (revisionResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result.Failure(revisionResult.ErrorMessage!);
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ImportTestCasesResultDto>> ImportTestCasesAsync(Guid problemId, ImportTestCasesRequest request, CancellationToken cancellationToken = default)
    {
        var userResult = await GetActiveCurrentUserAsync(cancellationToken);
        if (userResult.IsFailure || userResult.Value is null)
        {
            return Result<ImportTestCasesResultDto>.Failure(userResult.ErrorMessage ?? "Unauthorized.");
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await ProblemJudgeRevisionPublisher.AcquireProblemLockAsync(dbContext, problemId, cancellationToken);

        var problem = await dbContext.Problems
            .FirstOrDefaultAsync(problem => problem.Id == problemId && !problem.IsDeleted, cancellationToken);

        if (problem is null)
        {
            return Result<ImportTestCasesResultDto>.Failure("Problem not found.");
        }

        if (!await CanManageTestCasesAsync(userResult.Value, problem, cancellationToken))
        {
            return Result<ImportTestCasesResultDto>.Failure("Forbidden.");
        }

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result<ImportTestCasesResultDto>.Failure("Choice problems do not use judge test cases.");
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

        if (request.Items.Count > ResourcePolicy.MaxImportTestCases)
        {
            return Result<ImportTestCasesResultDto>.Failure($"A batch cannot contain more than {ResourcePolicy.MaxImportTestCases} test cases.");
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
            var itemErrorCount = errors.Count;

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

            var input = problem.JudgeMode == JudgeMode.StandardInputOutput ? item.Input ?? string.Empty : string.Empty;
            var expectedOutput = problem.JudgeMode == JudgeMode.StandardInputOutput ? item.ExpectedOutput ?? string.Empty : string.Empty;
            var argumentsJson = problem.JudgeMode == JudgeMode.Function ? ToRawJson(item.ArgumentsJson) : null;
            var expectedJson = problem.JudgeMode == JudgeMode.Function ? ToRawJson(item.ExpectedJson) : null;
            if (errors.Count == itemErrorCount)
            {
                var boundsValidation = ProblemJudgeDefinitionValidator.ValidateTestCase(
                    problem,
                    input,
                    expectedOutput,
                    argumentsJson,
                    expectedJson,
                    visibility,
                    score,
                    ResourcePolicy);
                if (boundsValidation.IsFailure)
                {
                    errors.Add(ImportError(itemNumber, "item", boundsValidation.ErrorMessage!));
                }
            }

            if (errors.Count > itemErrorCount)
            {
                continue;
            }

            testCases.Add(new TestCase
            {
                Id = Guid.NewGuid(),
                ProblemId = problemId,
                Input = input,
                ExpectedOutput = expectedOutput,
                ArgumentsJson = argumentsJson,
                ExpectedJson = expectedJson,
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

        var importBytes = testCases.Sum(testCase => ProblemJudgeDefinitionValidator.GetPayloadSizeBytes(JudgeTestCasePayload.From(testCase)));
        if (importBytes > ResourcePolicy.MaxImportPayloadBytes)
        {
            return Result<ImportTestCasesResultDto>.Failure($"Batch test data exceeds the {ResourcePolicy.MaxImportPayloadBytes}-byte UTF-8 limit.");
        }

        var prospectiveValidation = await ValidateProspectiveTestCasesAsync(
            problemId,
            problem.TimeLimitMs!.Value,
            testCases.Select(JudgeTestCasePayload.From).ToList(),
            excludedTestCaseId: null,
            cancellationToken);
        if (prospectiveValidation.IsFailure)
        {
            return Result<ImportTestCasesResultDto>.Failure(prospectiveValidation.ErrorMessage!);
        }

        dbContext.TestCases.AddRange(testCases);
        problem.AuthoringVersion = checked(problem.AuthoringVersion + 1);
        problem.UpdatedAt = now;
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.ProblemTestCasesChanged, "Problem", problemId.ToString(), Metadata: new Dictionary<string, string?> { ["testCaseCountDelta"] = testCases.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        if (problem.IsPublished)
        {
            var revisionResult = await ProblemJudgeRevisionPublisher.PublishAsync(dbContext, problem, ResourcePolicy, cancellationToken);
            if (revisionResult.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
                return Result<ImportTestCasesResultDto>.Failure(revisionResult.ErrorMessage!);
            }
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

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

        if (problem.ProblemKind != ProblemKind.Programming)
        {
            return Result<IReadOnlyList<TestCaseExportItemDto>>.Failure("Choice problems do not use judge test cases.");
        }

        var items = problem.TestCases
            .OrderBy(testCase => testCase.CreatedAt)
            .Select(testCase => ToExportItem(problem.JudgeMode!.Value, testCase))
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

    private Result ValidateProblemRequest(
        string title,
        string description,
        string inputDescription,
        string outputDescription,
        int? timeLimitMs,
        int? memoryLimitMb,
        ProblemKind problemKind,
        JudgeMode? judgeMode,
        int allowedLanguagesMask,
        string? functionSpecJson,
        string? starterCodeJson)
    {
        return ProblemJudgeDefinitionValidator.ValidateProblem(
            title,
            description,
            inputDescription,
            outputDescription,
            timeLimitMs,
            memoryLimitMb,
            problemKind,
            judgeMode,
            allowedLanguagesMask,
            functionSpecJson,
            starterCodeJson,
            ResourcePolicy);
    }

    private async Task<Result> ValidateProspectiveTestCasesAsync(
        Guid problemId,
        int timeLimitMs,
        IReadOnlyCollection<JudgeTestCasePayload> additions,
        Guid? excludedTestCaseId,
        CancellationToken cancellationToken)
    {
        var stored = await dbContext.TestCases
            .AsNoTracking()
            .Where(testCase => testCase.ProblemId == problemId
                && !testCase.IsDeleted
                && (!excludedTestCaseId.HasValue || testCase.Id != excludedTestCaseId.Value))
            .Select(testCase => new
            {
                testCase.Input,
                testCase.ExpectedOutput,
                testCase.ArgumentsJson,
                testCase.ExpectedJson
            })
            .ToListAsync(cancellationToken);
        var payloads = stored
            .Select(testCase => new JudgeTestCasePayload(testCase.Input, testCase.ExpectedOutput, testCase.ArgumentsJson, testCase.ExpectedJson))
            .Concat(additions)
            .ToList();

        return ProblemJudgeDefinitionValidator.ValidateTestCaseCollection(
            timeLimitMs,
            payloads,
            ResourcePolicy,
            requireAtLeastOne: false);
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

    private static Result<List<ProblemChoiceQuestion>> BuildChoiceQuestions(Guid problemId, IReadOnlyList<ChoiceQuestionWriteRequest> requests, DateTimeOffset now, bool preserveRequestedIds)
    {
        var questionIds = new HashSet<Guid>();
        var optionIds = new HashSet<Guid>();
        var result = new List<ProblemChoiceQuestion>(requests.Count);
        for (var questionIndex = 0; questionIndex < requests.Count; questionIndex++)
        {
            var request = requests[questionIndex];
            var questionId = preserveRequestedIds ? request.Id.GetValueOrDefault() : Guid.Empty;
            if (questionId == Guid.Empty) questionId = Guid.NewGuid();
            if (!questionIds.Add(questionId))
            {
                return Result<List<ProblemChoiceQuestion>>.Failure("Choice question IDs must be unique.");
            }

            var question = new ProblemChoiceQuestion
            {
                Id = questionId,
                ProblemId = problemId,
                Order = questionIndex,
                StemMarkdown = request.StemMarkdown,
                SelectionMode = request.SelectionMode,
                Score = request.Score,
                ExplanationMarkdown = request.ExplanationMarkdown,
                CreatedAt = now,
                UpdatedAt = now
            };
            for (var optionIndex = 0; optionIndex < request.Options.Count; optionIndex++)
            {
                var optionRequest = request.Options[optionIndex];
                var optionId = preserveRequestedIds ? optionRequest.Id.GetValueOrDefault() : Guid.Empty;
                if (optionId == Guid.Empty) optionId = Guid.NewGuid();
                if (!optionIds.Add(optionId))
                {
                    return Result<List<ProblemChoiceQuestion>>.Failure("Choice option IDs must be unique.");
                }

                question.Options.Add(new ProblemChoiceOption
                {
                    Id = optionId,
                    QuestionId = questionId,
                    Order = optionIndex,
                    ContentMarkdown = optionRequest.ContentMarkdown,
                    IsCorrect = optionRequest.IsCorrect,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            result.Add(question);
        }

        return Result<List<ProblemChoiceQuestion>>.Success(result);
    }

    private async Task ReplaceChoiceQuestionsAsync(Problem problem, IReadOnlyList<ProblemChoiceQuestion> desired, CancellationToken cancellationToken)
    {
        var existing = problem.ChoiceQuestions.Where(question => !question.IsDeleted).ToDictionary(question => question.Id);
        var desiredIds = desired.Select(question => question.Id).ToHashSet();
        var offset = Math.Max(existing.Values.Select(question => question.Order).DefaultIfEmpty(0).Max(), desired.Count) + 1;
        foreach (var question in existing.Values)
        {
            question.Order += offset;
            foreach (var option in question.Options.Where(option => !option.IsDeleted)) option.Order += ChoiceProblemDefinitionValidator.MaxOptionsPerQuestion + 1;
        }
        if (existing.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var current in existing.Values.Where(question => !desiredIds.Contains(question.Id)))
        {
            current.IsDeleted = true;
            current.UpdatedAt = now;
            foreach (var option in current.Options) option.IsDeleted = true;
        }

        foreach (var desiredQuestion in desired)
        {
            if (!existing.TryGetValue(desiredQuestion.Id, out var current))
            {
                problem.ChoiceQuestions.Add(desiredQuestion);
                continue;
            }

            current.Order = desiredQuestion.Order;
            current.StemMarkdown = desiredQuestion.StemMarkdown;
            current.SelectionMode = desiredQuestion.SelectionMode;
            current.Score = desiredQuestion.Score;
            current.ExplanationMarkdown = desiredQuestion.ExplanationMarkdown;
            current.UpdatedAt = now;

            var existingOptions = current.Options.Where(option => !option.IsDeleted).ToDictionary(option => option.Id);
            var desiredOptionIds = desiredQuestion.Options.Select(option => option.Id).ToHashSet();
            foreach (var option in existingOptions.Values.Where(option => !desiredOptionIds.Contains(option.Id))) option.IsDeleted = true;
            foreach (var desiredOption in desiredQuestion.Options)
            {
                if (!existingOptions.TryGetValue(desiredOption.Id, out var option))
                {
                    desiredOption.QuestionId = current.Id;
                    current.Options.Add(desiredOption);
                    continue;
                }
                option.Order = desiredOption.Order;
                option.ContentMarkdown = desiredOption.ContentMarkdown;
                option.IsCorrect = desiredOption.IsCorrect;
                option.UpdatedAt = now;
            }
        }
    }

    private async Task<Result> ValidateChoiceOwnershipAsync(Guid problemId, IReadOnlyCollection<ProblemChoiceQuestion> desired, CancellationToken cancellationToken)
    {
        var questionIds = desired.Select(question => question.Id).ToList();
        var foreignQuestion = await dbContext.ProblemChoiceQuestions.AsNoTracking()
            .AnyAsync(question => questionIds.Contains(question.Id) && question.ProblemId != problemId, cancellationToken);
        if (foreignQuestion) return Result.Failure("Choice question ID does not belong to this problem.");

        var optionOwners = desired.SelectMany(question => question.Options.Select(option => new { OptionId = option.Id, QuestionId = question.Id })).ToList();
        var optionIds = optionOwners.Select(item => item.OptionId).ToList();
        var storedOptions = await dbContext.ProblemChoiceOptions.AsNoTracking()
            .Where(option => optionIds.Contains(option.Id))
            .Select(option => new { option.Id, option.QuestionId })
            .ToListAsync(cancellationToken);
        return storedOptions.Any(stored => optionOwners.Any(desiredOption => desiredOption.OptionId == stored.Id && desiredOption.QuestionId != stored.QuestionId))
            ? Result.Failure("Choice option ID does not belong to this question.")
            : Result.Success();
    }

    private async Task<Result> ValidateRevealPolicyTransitionAsync(Problem problem, UpdateProblemRequest request, CancellationToken cancellationToken)
    {
        if (problem.ProblemKind != ProblemKind.ChoiceSet || request.ProblemKind != ProblemKind.ChoiceSet)
        {
            return Result.Success();
        }

        var now = DateTimeOffset.UtcNow;
        var newPolicyIsVisibleNow = request.ChoiceAnswerRevealPolicy == ChoiceAnswerRevealPolicy.AtScheduledTime
            && request.ChoiceAnswerRevealAt <= now;
        if (problem.ChoiceAnswerRevealPolicy == ChoiceAnswerRevealPolicy.AtScheduledTime
            && problem.ChoiceAnswerRevealAt <= now
            && !newPolicyIsVisibleNow)
        {
            return Result.Failure("answers_already_revealed");
        }

        if (problem.ChoiceAnswerRevealPolicy == ChoiceAnswerRevealPolicy.AfterSubmission && !newPolicyIsVisibleNow)
        {
            var anyChoiceSubmission = await dbContext.Submissions.AsNoTracking()
                .AnyAsync(submission => submission.ProblemId == problem.Id && submission.SubmissionKind == SubmissionKind.Choice, cancellationToken);
            if (anyChoiceSubmission && request.ChoiceAnswerRevealPolicy != ChoiceAnswerRevealPolicy.AfterSubmission)
            {
                return Result.Failure("answers_already_revealed");
            }
        }

        return Result.Success();
    }

    private static bool ChoiceContentEquals(IReadOnlyCollection<ProblemChoiceQuestion> current, IReadOnlyCollection<ProblemChoiceQuestion> desired)
    {
        var left = current.Where(question => !question.IsDeleted).OrderBy(question => question.Order).ToList();
        var right = desired.OrderBy(question => question.Order).ToList();
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].StemMarkdown, right[i].StemMarkdown, StringComparison.Ordinal)
                || left[i].SelectionMode != right[i].SelectionMode
                || left[i].Score != right[i].Score
                || !string.Equals(left[i].ExplanationMarkdown, right[i].ExplanationMarkdown, StringComparison.Ordinal)) return false;
            var leftOptions = left[i].Options.Where(option => !option.IsDeleted).OrderBy(option => option.Order).ToList();
            var rightOptions = right[i].Options.OrderBy(option => option.Order).ToList();
            if (leftOptions.Count != rightOptions.Count) return false;
            for (var j = 0; j < leftOptions.Count; j++)
            {
                if (!string.Equals(leftOptions[j].ContentMarkdown, rightOptions[j].ContentMarkdown, StringComparison.Ordinal)
                    || leftOptions[j].IsCorrect != rightOptions[j].IsCorrect) return false;
            }
        }
        return true;
    }

    private static ProblemDetailDto ToDetailDto(Problem problem, bool includeHiddenTestCases, bool usePublishedChoiceRevision = false)
    {
        return new ProblemDetailDto
        {
            Id = problem.Id,
            ProblemKind = problem.ProblemKind,
            AuthoringVersion = problem.AuthoringVersion,
            CurrentJudgeRevisionId = problem.CurrentJudgeRevisionId,
            Title = problem.Title,
            Description = problem.Description,
            InputDescription = problem.InputDescription,
            OutputDescription = problem.OutputDescription,
            TimeLimitMs = problem.TimeLimitMs,
            MemoryLimitMb = problem.MemoryLimitMb,
            IsPublished = problem.IsPublished,
            JudgeMode = problem.JudgeMode,
            AllowedLanguagesMask = problem.AllowedLanguagesMask,
            TotalScore = problem.ProblemKind == ProblemKind.ChoiceSet
                ? (usePublishedChoiceRevision
                    ? problem.CurrentJudgeRevision?.ChoiceQuestions.Sum(question => question.Score) ?? 0
                    : problem.ChoiceQuestions.Where(question => !question.IsDeleted).Sum(question => question.Score))
                : problem.CalculateTotalScore(),
            FunctionSpecJson = problem.FunctionSpecJson,
            StarterCodeJson = problem.StarterCodeJson,
            ChoiceAnswerRevealPolicy = problem.ChoiceAnswerRevealPolicy,
            ChoiceAnswerRevealAt = problem.ChoiceAnswerRevealAt,
            CreatedAt = problem.CreatedAt,
            UpdatedAt = problem.UpdatedAt,
            TestCases = problem.TestCases
                .Where(testCase => !testCase.IsDeleted && (includeHiddenTestCases || testCase.Visibility == TestCaseVisibility.Sample))
                .OrderBy(testCase => testCase.CreatedAt)
                .Select(ToTestCaseDto)
                .ToList(),
            ChoiceQuestions = usePublishedChoiceRevision && problem.CurrentJudgeRevision is not null
                ? problem.CurrentJudgeRevision.ChoiceQuestions.OrderBy(question => question.Order).Select(question => new ChoiceQuestionDto
                {
                    Id = question.Id,
                    Order = question.Order,
                    StemMarkdown = question.StemMarkdown,
                    SelectionMode = question.SelectionMode,
                    Score = question.Score,
                    Options = question.Options.OrderBy(option => option.Order).Select(option => new ChoiceOptionDto { Id = option.Id, Order = option.Order, ContentMarkdown = option.ContentMarkdown }).ToList(),
                    CorrectOptionIds = includeHiddenTestCases ? question.Options.Where(option => option.IsCorrect).Select(option => option.Id).ToList() : null,
                    ExplanationMarkdown = includeHiddenTestCases ? question.ExplanationMarkdown : null
                }).ToList()
                : problem.ChoiceQuestions.Where(question => !question.IsDeleted).OrderBy(question => question.Order).Select(question => new ChoiceQuestionDto
                {
                    Id = question.Id,
                    Order = question.Order,
                    StemMarkdown = question.StemMarkdown,
                    SelectionMode = question.SelectionMode,
                    Score = question.Score,
                    Options = question.Options.Where(option => !option.IsDeleted).OrderBy(option => option.Order).Select(option => new ChoiceOptionDto { Id = option.Id, Order = option.Order, ContentMarkdown = option.ContentMarkdown }).ToList(),
                    CorrectOptionIds = question.Options.Where(option => !option.IsDeleted && option.IsCorrect).Select(option => option.Id).ToList(),
                    ExplanationMarkdown = question.ExplanationMarkdown
                }).ToList()
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
