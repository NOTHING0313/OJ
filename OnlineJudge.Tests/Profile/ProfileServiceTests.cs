using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Profile.Dtos;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Profile;

namespace OnlineJudge.Tests.Profile;

public class ProfileServiceTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetMyProfile_Returns_Current_User_Profile()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var result = await service.GetMyProfileAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ids.AnswererA, result.Value!.User.Id);
        Assert.Equal("answerer-a", result.Value.User.UserName);
    }

    [Fact]
    public async Task GetUserProfile_AsRoot_Returns_Target_User_Profile()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.Root, UserRole.Root);

        var result = await service.GetUserProfileAsync(ids.AnswererA);

        Assert.True(result.IsSuccess);
        Assert.Equal(ids.AnswererA, result.Value!.User.Id);
    }

    [Fact]
    public async Task GetUserProfile_AsAnswerer_ForOtherUser_ReturnsForbidden()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var result = await service.GetUserProfileAsync(ids.AnswererB);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetUserProfile_AsProblemSetter_ForOtherUser_ReturnsForbidden()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.ProblemSetter, UserRole.ProblemSetter);

        var result = await service.GetUserProfileAsync(ids.AnswererA);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden.", result.ErrorMessage);
    }

    [Fact]
    public async Task GetUserProfile_AsRoot_ForMissingUser_ReturnsNotFound()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.Root, UserRole.Root);

        var result = await service.GetUserProfileAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("User not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task Profile_DoesNotExpose_SourceCode_Or_PasswordHash()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var result = await service.GetMyProfileAsync();
        var json = JsonSerializer.Serialize(result.Value);

        Assert.DoesNotContain("sourceCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(typeof(RecentSubmissionDto).GetProperty("SourceCode"));
        Assert.Null(typeof(ProfileUserDto).GetProperty("PasswordHash"));
    }

    [Fact]
    public async Task SubmissionAndProblemSummary_UseExpectedCountsAndDistinctAcceptedProblems()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var result = await service.GetMyProfileAsync();
        var profile = result.Value!;

        Assert.Equal(5, profile.SubmissionSummary.TotalSubmissionCount);
        Assert.Equal(3, profile.SubmissionSummary.AcceptedSubmissionCount);
        Assert.Equal(1, profile.SubmissionSummary.WrongAnswerCount);
        Assert.Equal(1, profile.SubmissionSummary.CompileErrorCount);
        Assert.Equal(0, profile.SubmissionSummary.RuntimeErrorCount);
        Assert.Equal(0, profile.SubmissionSummary.SystemErrorCount);
        Assert.Equal(0.6, profile.SubmissionSummary.AcceptedRate, 3);
        Assert.Equal(2, profile.ProblemSummary.AcceptedProblemCount);
        Assert.Equal(2, profile.ProblemSummary.RecentAcceptedProblems.Count);
        Assert.Equal(ids.Problem1, profile.ProblemSummary.RecentAcceptedProblems.Last().ProblemId);
    }

    [Fact]
    public async Task LanguageSummary_GroupsSubmissionAndAcceptedCounts()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var profile = (await service.GetMyProfileAsync()).Value!;

        var cpp = Assert.Single(profile.LanguageSummary, item => item.Language == JudgeLanguage.Cpp17);
        var csharp = Assert.Single(profile.LanguageSummary, item => item.Language == JudgeLanguage.CSharp);
        Assert.Equal(4, cpp.SubmissionCount);
        Assert.Equal(2, cpp.AcceptedCount);
        Assert.Equal(1, csharp.SubmissionCount);
        Assert.Equal(1, csharp.AcceptedCount);
    }

    [Fact]
    public async Task ChallengeParticipatedCount_UsesParticipantsAndCompletionsUnion()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var profile = (await service.GetMyProfileAsync()).Value!;

        Assert.Equal(2, profile.ChallengeSummary.ParticipatedChallengeCount);
        Assert.Equal(2, profile.ChallengeSummary.CompletedTaskCount);
        Assert.Equal(30, profile.ChallengeSummary.TotalScore);
        Assert.Equal(BaseTime.AddHours(8), profile.ChallengeSummary.LastCompletedAt);
    }

    [Fact]
    public async Task MissingProblemOrChallengeReferences_DoNotThrow()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var missingProblemId = Guid.NewGuid();
        var missingChallengeId = Guid.NewGuid();
        var missingTaskId = Guid.NewGuid();

        dbContext.Submissions.Add(new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = missingProblemId,
            UserId = ids.AnswererA,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "missing problem source",
            Status = JudgeStatus.Accepted,
            CreatedAt = BaseTime.AddHours(9)
        });
        dbContext.ChallengeTaskCompletions.Add(new ChallengeTaskCompletion
        {
            Id = Guid.NewGuid(),
            ChallengeId = missingChallengeId,
            ChallengeTaskId = missingTaskId,
            UserId = ids.AnswererA,
            Score = 5,
            CompletedAt = BaseTime.AddHours(10)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);
        var result = await service.GetMyProfileAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value!.RecentSubmissions);
        Assert.NotEmpty(result.Value.RecentChallengeCompletions);
    }

    [Fact]
    public async Task RecentSubmissions_LimitedToTen_SortedDescending_AndDoesNotContainSourceCode()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsersAndProblems(dbContext);

        for (var index = 0; index < 12; index++)
        {
            dbContext.Submissions.Add(new Submission
            {
                Id = Guid.NewGuid(),
                ProblemId = ids.Problem1,
                UserId = ids.AnswererA,
                Language = JudgeLanguage.Cpp17,
                SourceCode = $"secret source {index}",
                Status = JudgeStatus.Accepted,
                CreatedAt = BaseTime.AddMinutes(index),
                FinishedAt = BaseTime.AddMinutes(index + 1)
            });
        }
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);
        var profile = (await service.GetMyProfileAsync()).Value!;

        Assert.Equal(10, profile.RecentSubmissions.Count);
        Assert.True(profile.RecentSubmissions.Zip(profile.RecentSubmissions.Skip(1), (left, right) => left.CreatedAt >= right.CreatedAt).All(value => value));
        var json = JsonSerializer.Serialize(profile.RecentSubmissions);
        Assert.DoesNotContain("sourceCode", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret source", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecentFileReviews_ReturnsReviewedAndUnreviewedRecords()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProfileData(dbContext);
        var service = CreateService(dbContext, ids.AnswererA, UserRole.Answerer);

        var profile = (await service.GetMyProfileAsync()).Value!;

        Assert.Equal(2, profile.RecentFileReviews.Count);
        Assert.Contains(profile.RecentFileReviews, item => item.ReviewScore == 8 && item.ReviewComment == "结构清晰");
        Assert.Contains(profile.RecentFileReviews, item => item.ReviewScore is null && item.ReviewComment is null);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static ProfileService CreateService(OnlineJudgeDbContext dbContext, Guid currentUserId, UserRole role)
    {
        return new ProfileService(dbContext, new TestCurrentUser(currentUserId, role));
    }

    private static TestIds SeedProfileData(OnlineJudgeDbContext dbContext)
    {
        var ids = SeedUsersAndProblems(dbContext);
        var problem1 = dbContext.Problems.Local.First(problem => problem.Id == ids.Problem1);
        var problem2 = dbContext.Problems.Local.First(problem => problem.Id == ids.Problem2);

        dbContext.Submissions.AddRange(
            Submission(ids.Problem1, ids.AnswererA, JudgeStatus.Accepted, JudgeLanguage.Cpp17, BaseTime.AddHours(1), "accepted 1", problem1),
            Submission(ids.Problem1, ids.AnswererA, JudgeStatus.Accepted, JudgeLanguage.Cpp17, BaseTime.AddHours(2), "accepted 2", problem1),
            Submission(ids.Problem1, ids.AnswererA, JudgeStatus.WrongAnswer, JudgeLanguage.Cpp17, BaseTime.AddHours(3), "wrong", problem1),
            Submission(ids.Problem2, ids.AnswererA, JudgeStatus.Accepted, JudgeLanguage.CSharp, BaseTime.AddHours(4), "accepted 3", problem2),
            Submission(ids.Problem2, ids.AnswererA, JudgeStatus.CompileError, JudgeLanguage.Cpp17, BaseTime.AddHours(5), "compile", problem2));

        var challenge1 = new Challenge
        {
            Id = ids.Challenge1,
            Title = "挑战一",
            Description = "challenge",
            CreatedByUserId = ids.Root,
            StartAt = BaseTime,
            EndAt = BaseTime.AddDays(7),
            IsPublished = true,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
        var challenge2 = new Challenge
        {
            Id = ids.Challenge2,
            Title = "挑战二",
            Description = "challenge",
            CreatedByUserId = ids.Root,
            StartAt = BaseTime,
            EndAt = BaseTime.AddDays(7),
            IsPublished = true,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
        var task1 = ChallengeTask(ids.Task1, ids.Challenge1, "任务一", challenge1);
        var task2 = ChallengeTask(ids.Task2, ids.Challenge2, "任务二", challenge2);

        dbContext.Challenges.AddRange(challenge1, challenge2);
        dbContext.ChallengeTasks.AddRange(task1, task2);
        dbContext.ChallengeParticipants.Add(new ChallengeParticipant
        {
            Id = Guid.NewGuid(),
            ChallengeId = ids.Challenge1,
            UserId = ids.AnswererA,
            JoinedAt = BaseTime.AddHours(1),
            Challenge = challenge1
        });
        dbContext.ChallengeTaskCompletions.AddRange(
            Completion(ids.Challenge1, ids.Task1, ids.AnswererA, 10, BaseTime.AddHours(7), challenge1, task1),
            Completion(ids.Challenge2, ids.Task2, ids.AnswererA, 20, BaseTime.AddHours(8), challenge2, task2));
        dbContext.ChallengeTaskFileSubmissions.AddRange(
            FileSubmission(ids.Challenge1, ids.Task1, ids.AnswererA, BaseTime.AddHours(6), 8, "结构清晰", BaseTime.AddHours(9), challenge1, task1),
            FileSubmission(ids.Challenge2, ids.Task2, ids.AnswererA, BaseTime.AddHours(10), null, null, null, challenge2, task2));

        dbContext.SaveChanges();
        return ids;
    }

    private static TestIds SeedUsersAndProblems(OnlineJudgeDbContext dbContext)
    {
        var ids = new TestIds();
        dbContext.Users.AddRange(
            User(ids.Root, "root", UserRole.Root),
            User(ids.AnswererA, "answerer-a", UserRole.Answerer),
            User(ids.AnswererB, "answerer-b", UserRole.Answerer),
            User(ids.ProblemSetter, "setter", UserRole.ProblemSetter));
        dbContext.Problems.AddRange(
            Problem(ids.Problem1, "A + B", ids.ProblemSetter),
            Problem(ids.Problem2, "Two Sum", ids.ProblemSetter));
        dbContext.SaveChanges();
        return ids;
    }

    private static User User(Guid id, string userName, UserRole role)
    {
        return new User
        {
            Id = id,
            UserName = userName,
            Email = $"{userName}@example.test",
            AvatarUrl = null,
            PasswordHash = "hashed-password-should-not-leak",
            Role = role,
            IsBlacklisted = false,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
    }

    private static Problem Problem(Guid id, string title, Guid createdByUserId)
    {
        return new Problem
        {
            Id = id,
            Title = title,
            Description = "problem",
            InputDescription = "input",
            OutputDescription = "output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            CreatedByUserId = createdByUserId,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
    }

    private static Submission Submission(Guid problemId, Guid userId, JudgeStatus status, JudgeLanguage language, DateTimeOffset createdAt, string sourceCode, Problem problem)
    {
        return new Submission
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            UserId = userId,
            Language = language,
            SourceCode = sourceCode,
            Status = status,
            CreatedAt = createdAt,
            FinishedAt = createdAt.AddMinutes(1),
            Problem = problem
        };
    }

    private static ChallengeTask ChallengeTask(Guid id, Guid challengeId, string title, Challenge challenge)
    {
        return new ChallengeTask
        {
            Id = id,
            ChallengeId = challengeId,
            Title = title,
            Description = "task",
            TaskType = ChallengeTaskType.FileUpload,
            Difficulty = ChallengeTaskDifficulty.Pawn,
            BoardX = 0,
            BoardY = 0,
            Score = 10,
            IsPublished = true,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime,
            Challenge = challenge
        };
    }

    private static ChallengeTaskCompletion Completion(Guid challengeId, Guid taskId, Guid userId, int score, DateTimeOffset completedAt, Challenge challenge, ChallengeTask task)
    {
        return new ChallengeTaskCompletion
        {
            Id = Guid.NewGuid(),
            ChallengeId = challengeId,
            ChallengeTaskId = taskId,
            UserId = userId,
            Score = score,
            CompletedAt = completedAt,
            Challenge = challenge,
            ChallengeTask = task
        };
    }

    private static ChallengeTaskFileSubmission FileSubmission(Guid challengeId, Guid taskId, Guid userId, DateTimeOffset createdAt, int? reviewScore, string? reviewComment, DateTimeOffset? reviewedAt, Challenge challenge, ChallengeTask task)
    {
        return new ChallengeTaskFileSubmission
        {
            Id = Guid.NewGuid(),
            ChallengeId = challengeId,
            ChallengeTaskId = taskId,
            UserId = userId,
            OriginalFileName = "answer.zip",
            StoredFileName = $"{Guid.NewGuid()}.zip",
            FilePath = "App_Data/challenge-file-submissions/answer.zip",
            FileSizeBytes = 1024,
            ContentType = "application/zip",
            ReviewScore = reviewScore,
            ReviewComment = reviewComment,
            ReviewedByUserId = reviewedAt.HasValue ? Guid.NewGuid() : null,
            ReviewedAt = reviewedAt,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            Challenge = challenge,
            ChallengeTask = task
        };
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;

        public string? UserName => "test-user";

        public UserRole? Role => role;
    }

    private sealed class TestIds
    {
        public Guid Root { get; } = Guid.NewGuid();

        public Guid AnswererA { get; } = Guid.NewGuid();

        public Guid AnswererB { get; } = Guid.NewGuid();

        public Guid ProblemSetter { get; } = Guid.NewGuid();

        public Guid Problem1 { get; } = Guid.NewGuid();

        public Guid Problem2 { get; } = Guid.NewGuid();

        public Guid Challenge1 { get; } = Guid.NewGuid();

        public Guid Challenge2 { get; } = Guid.NewGuid();

        public Guid Task1 { get; } = Guid.NewGuid();

        public Guid Task2 { get; } = Guid.NewGuid();
    }
}
