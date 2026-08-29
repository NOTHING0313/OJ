using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OnlineJudge.Application.Common;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Teams.Dtos;
using OnlineJudge.Application.Teams.Services;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Application.SecurityAudit;

namespace OnlineJudge.Infrastructure.Teams;

public sealed class TeamGitRepositoryService(
    OnlineJudgeDbContext dbContext,
    ICurrentUser currentUser,
    TeamGitRemoteSecurityValidator remoteValidator,
    IGitProcessRunner processRunner,
    ITeamGitRepositoryStorage storage,
    TeamGitSyncLockProvider lockProvider,
    TeamProjectOptions options,
    TimeProvider timeProvider,
    ISecurityAuditWriter? auditWriter = null,
    ILogger<TeamGitRepositoryService>? logger = null) : ITeamGitRepositoryService
{
    private const string GenericSyncFailure = "Repository synchronization failed.";
    private const string UnavailableMessage = "Git repository synchronization is unavailable.";
    private const string AuditReference = "refs/heads/oj-audit";

    public async Task<Result<IReadOnlyList<TeamProjectAuditDto>>> GetProjectsAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        var roleResult = await RequireAuditRoleAsync(cancellationToken);
        if (roleResult.IsFailure)
        {
            return Result<IReadOnlyList<TeamProjectAuditDto>>.Failure(roleResult.ErrorMessage!);
        }

        var teamExists = await dbContext.Teams.AsNoTracking().AnyAsync(team => team.Id == teamId && !team.IsDeleted, cancellationToken);
        if (!teamExists) return Result<IReadOnlyList<TeamProjectAuditDto>>.Failure("Team not found.");
        var projects = await dbContext.TeamProjects.AsNoTracking()
            .Where(project => project.TeamId == teamId)
            .OrderBy(project => project.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<TeamProjectAuditDto>>.Success(projects.Select(ToAuditDto).ToList());
    }

    public async Task<Result<TeamProjectAuditDto>> SyncAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken = default)
    {
        var access = await GetSyncableProjectAsync(teamId, projectId, cancellationToken);
        if (access.IsFailure || access.Value is null)
        {
            return Result<TeamProjectAuditDto>.Failure(access.ErrorMessage ?? "Project not found.");
        }

        var gate = lockProvider.Get(projectId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var project = access.Value;
            await dbContext.Entry(project).ReloadAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var cooldown = TimeSpan.FromSeconds(Math.Clamp(options.SyncCooldownSeconds, 1, 3600));
            if (project.LastSyncAttemptAt is not null && now - project.LastSyncAttemptAt < cooldown)
            {
                return Result<TeamProjectAuditDto>.Failure("Repository was synchronized too recently.");
            }

            project.LastSyncAttemptAt = now;
            auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.TeamGitSyncRequested, "TeamProject", project.Id.ToString(), SecurityAuditResults.Requested));
            await dbContext.SaveChangesAsync(cancellationToken);

            var availability = await RunGitAsync(["--version"], cancellationToken);
            if (!availability.Succeeded)
            {
                return await RecordFailureAsync(project, availability.TimedOut ? "Repository synchronization timed out." : UnavailableMessage, cancellationToken);
            }

            var remote = await remoteValidator.ValidateAsync(project.RepositoryUrl, cancellationToken);
            if (remote.IsFailure || remote.Value is null)
            {
                return await RecordFailureAsync(project, remote.ErrorMessage ?? GenericSyncFailure, cancellationToken);
            }

            var temporaryPath = storage.CreateTemporaryRepositoryPath(project.Id);
            try
            {
                GitProcessResult syncResult;
                var existingRepository = storage.Exists(project.Id);
                if (existingRepository)
                {
                    await storage.CopyToTemporaryAsync(project.Id, temporaryPath, cancellationToken);
                    syncResult = await RunGitAsync([
                        "-C", temporaryPath, "fetch", $"--depth={HistoryDepth}", "--prune", "--no-tags",
                        remote.Value, $"+HEAD:{AuditReference}"
                    ], cancellationToken);
                    if (syncResult.Succeeded)
                    {
                        syncResult = await RunGitAsync(["-C", temporaryPath, "symbolic-ref", "HEAD", AuditReference], cancellationToken);
                    }
                }
                else
                {
                    syncResult = await RunGitAsync([
                        "clone", "--bare", $"--depth={HistoryDepth}", "--no-tags", "--filter=blob:none", remote.Value, temporaryPath
                    ], cancellationToken);
                    if (!syncResult.Succeeded && !syncResult.TimedOut && CanRetryWithoutFilter(syncResult.StandardError))
                    {
                        storage.DeleteTemporary(temporaryPath);
                        syncResult = await RunGitAsync([
                            "clone", "--bare", $"--depth={HistoryDepth}", "--no-tags", remote.Value, temporaryPath
                        ], cancellationToken);
                    }
                }

                if (!syncResult.Succeeded)
                {
                    return await RecordFailureAsync(project, syncResult.TimedOut ? "Repository synchronization timed out." : GenericSyncFailure, cancellationToken);
                }

                if (storage.GetSizeBytes(temporaryPath) > MaximumRepositoryBytes)
                {
                    storage.DeleteTemporary(temporaryPath);
                    return await RecordFailureAsync(project, "Repository exceeds synchronization size limit.", cancellationToken);
                }

                var headResult = await RunGitAsync(["-C", temporaryPath, "rev-parse", "--verify", "HEAD^{commit}"], cancellationToken);
                if (!headResult.Succeeded)
                {
                    return await RecordFailureAsync(project, GenericSyncFailure, cancellationToken);
                }

                string? defaultBranch = project.DefaultBranch;
                if (!existingRepository)
                {
                    var branchResult = await RunGitAsync(["-C", temporaryPath, "symbolic-ref", "--short", "HEAD"], cancellationToken);
                    defaultBranch = branchResult.Succeeded ? Sanitize(branchResult.StandardOutput.Trim(), 255) : null;
                }
                await storage.PromoteAsync(project.Id, temporaryPath, cancellationToken);
                project.LastSyncStatus = TeamProjectSyncStatus.Success;
                project.LastSyncError = null;
                project.LastSyncedAt = timeProvider.GetUtcNow();
                project.DefaultBranch = string.IsNullOrWhiteSpace(defaultBranch) ? null : defaultBranch;
                auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.TeamGitSyncSucceeded, "TeamProject", project.Id.ToString()));
                try
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    logger?.LogError(exception, "Team Git synchronization completed but audit persistence failed. TeamProjectId={TeamProjectId}", project.Id);
                    return Result<TeamProjectAuditDto>.Failure(GenericSyncFailure);
                }
                return Result<TeamProjectAuditDto>.Success(ToAuditDto(project));
            }
            catch (OperationCanceledException)
            {
                storage.DeleteTemporary(temporaryPath);
                throw;
            }
            catch
            {
                storage.DeleteTemporary(temporaryPath);
                return await RecordFailureAsync(project, GenericSyncFailure, cancellationToken);
            }
            finally
            {
                storage.DeleteTemporary(temporaryPath);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<Result<IReadOnlyList<TeamGitCommitDto>>> GetCommitHistoryAsync(Guid teamId, Guid projectId, int skip = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        var history = await GetHistoryAsync(teamId, projectId, skip, limit, cancellationToken);
        return history.IsFailure || history.Value is null
            ? Result<IReadOnlyList<TeamGitCommitDto>>.Failure(history.ErrorMessage ?? "Project not found.")
            : Result<IReadOnlyList<TeamGitCommitDto>>.Success(history.Value.Commits);
    }

    public async Task<Result<TeamProjectGitHistoryDto>> GetHistoryAsync(Guid teamId, Guid projectId, int skip = 0, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) return Result<TeamProjectGitHistoryDto>.Failure("Limit must be between 1 and 100.");
        if (skip < 0 || skip > HistoryDepth) return Result<TeamProjectGitHistoryDto>.Failure($"Skip must be between 0 and {HistoryDepth}.");

        var access = await GetReadableProjectAsync(teamId, projectId, cancellationToken);
        if (access.IsFailure || access.Value is null)
        {
            return Result<TeamProjectGitHistoryDto>.Failure(access.ErrorMessage ?? "Project not found.");
        }

        var project = access.Value;
        if (project.LastSyncedAt is null)
        {
            return Result<TeamProjectGitHistoryDto>.Success(ToHistoryDto(project, []));
        }

        if (!storage.Exists(project.Id))
        {
            return project.LastSyncStatus == TeamProjectSyncStatus.Failed
                ? Result<TeamProjectGitHistoryDto>.Success(ToHistoryDto(project, []))
                : Result<TeamProjectGitHistoryDto>.Failure("Repository cache is unavailable.");
        }

        var result = await RunGitAsync([
            "-C", storage.GetRepositoryPath(project.Id), "log", "HEAD", $"--skip={skip}", $"-n{limit}",
            "--format=%H%x00%h%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%s%x00"
        ], cancellationToken);
        if (result.TimedOut) return Result<TeamProjectGitHistoryDto>.Failure("Repository history request timed out.");
        if (!result.Succeeded) return Result<TeamProjectGitHistoryDto>.Failure(UnavailableMessage);
        if (result.OutputTruncated) return Result<TeamProjectGitHistoryDto>.Failure("Repository history output exceeded the safe limit.");

        var history = ParseHistory(result.StandardOutput);
        return history.IsFailure || history.Value is null
            ? Result<TeamProjectGitHistoryDto>.Failure(history.ErrorMessage ?? "Repository history data is invalid.")
            : Result<TeamProjectGitHistoryDto>.Success(ToHistoryDto(project, history.Value));
    }

    public static Result<IReadOnlyList<TeamGitCommitDto>> ParseHistory(string output)
    {
        var fields = output.Split('\0');
        var commits = new List<TeamGitCommitDto>();
        var meaningfulLength = fields.Length > 0 && string.IsNullOrWhiteSpace(fields[^1]) ? fields.Length - 1 : fields.Length;
        if (meaningfulLength % 9 != 0)
        {
            return Result<IReadOnlyList<TeamGitCommitDto>>.Failure("Repository history data is invalid.");
        }

        for (var index = 0; index < meaningfulLength; index += 9)
        {
            var sha = fields[index].TrimStart('\r', '\n');
            if (!IsSha(sha)
                || !IsSha(fields[index + 1])
                || !DateTimeOffset.TryParse(fields[index + 4], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var authoredAt)
                || !DateTimeOffset.TryParse(fields[index + 7], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var committedAt))
            {
                return Result<IReadOnlyList<TeamGitCommitDto>>.Failure("Repository history data is invalid.");
            }

            commits.Add(new TeamGitCommitDto
            {
                Sha = sha,
                ShortSha = fields[index + 1],
                AuthorName = Sanitize(fields[index + 2], 200),
                AuthorEmail = Sanitize(fields[index + 3], 320),
                AuthoredAt = authoredAt,
                CommitterName = Sanitize(fields[index + 5], 200),
                CommitterEmail = Sanitize(fields[index + 6], 320),
                CommittedAt = committedAt,
                Subject = Sanitize(fields[index + 8], 500)
            });
        }

        return Result<IReadOnlyList<TeamGitCommitDto>>.Success(commits);
    }

    private int HistoryDepth => Math.Clamp(options.MaxCommitHistory, 1, 1000);
    private long MaximumRepositoryBytes => Math.Clamp(options.MaxRepositorySizeMb, 1, 10240) * 1024L * 1024L;

    private Task<GitProcessResult> RunGitAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return processRunner.RunAsync(new GitProcessRequest(arguments, TimeSpan.FromSeconds(Math.Clamp(options.GitTimeoutSeconds, 1, 300))), cancellationToken);
    }

    private async Task<Result<TeamProject>> GetAuditProjectAsync(Guid teamId, Guid projectId, bool tracking, CancellationToken cancellationToken)
    {
        var roleResult = await RequireAuditRoleAsync(cancellationToken);
        if (roleResult.IsFailure) return Result<TeamProject>.Failure(roleResult.ErrorMessage!);

        var query = dbContext.TeamProjects.Where(project => project.Id == projectId && project.TeamId == teamId && !project.Team!.IsDeleted);
        var project = await (tracking ? query : query.AsNoTracking()).FirstOrDefaultAsync(cancellationToken);
        return project is null ? Result<TeamProject>.Failure("Project not found.") : Result<TeamProject>.Success(project);
    }

    private async Task<Result<TeamProject>> GetSyncableProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<TeamProject>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null) return Result<TeamProject>.Failure("Unauthorized.");
        if (user.IsBlacklisted) return Result<TeamProject>.Failure("Account is blacklisted.");

        var isAuditRole = user.Role is UserRole.ProblemSetter or UserRole.Root;
        if (!isAuditRole)
        {
            var isActiveOwner = await dbContext.TeamMembers.AsNoTracking().AnyAsync(member => member.TeamId == teamId
                && member.UserId == userId && member.Role == TeamMemberRole.Owner && member.IsActive
                && member.Team!.OwnerUserId == userId && !member.Team.IsDeleted, cancellationToken);
            if (!isActiveOwner) return Result<TeamProject>.Failure("Forbidden.");
        }

        var project = await dbContext.TeamProjects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TeamId == teamId && !item.Team!.IsDeleted, cancellationToken);
        return project is null ? Result<TeamProject>.Failure("Project not found.") : Result<TeamProject>.Success(project);
    }

    private async Task<Result<TeamProject>> GetReadableProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result<TeamProject>.Failure("Unauthorized.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null) return Result<TeamProject>.Failure("Unauthorized.");
        if (user.IsBlacklisted) return Result<TeamProject>.Failure("Account is blacklisted.");
        var hasAccess = user.Role is UserRole.ProblemSetter or UserRole.Root
            || await dbContext.TeamMembers.AsNoTracking().AnyAsync(member => member.TeamId == teamId
                && member.UserId == userId && member.IsActive && !member.Team!.IsDeleted, cancellationToken);
        if (!hasAccess) return Result<TeamProject>.Failure("Forbidden.");

        var project = await dbContext.TeamProjects.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TeamId == teamId && !item.Team!.IsDeleted, cancellationToken);
        return project is null ? Result<TeamProject>.Failure("Project not found.") : Result<TeamProject>.Success(project);
    }

    private async Task<Result> RequireAuditRoleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId) return Result.Failure("Unauthorized.");
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId && !item.IsDeleted, cancellationToken);
        if (user is null) return Result.Failure("Unauthorized.");
        if (user.IsBlacklisted) return Result.Failure("Account is blacklisted.");
        return user.Role is UserRole.ProblemSetter or UserRole.Root ? Result.Success() : Result.Failure("Forbidden.");
    }

    private async Task<Result<TeamProjectAuditDto>> RecordFailureAsync(TeamProject project, string message, CancellationToken cancellationToken)
    {
        project.LastSyncStatus = TeamProjectSyncStatus.Failed;
        project.LastSyncError = Sanitize(message, 500);
        auditWriter?.Stage(new SecurityAuditRecord(SecurityAuditActions.TeamGitSyncFailed, "TeamProject", project.Id.ToString(), SecurityAuditResults.Failed));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Team Git synchronization failed and its audit result could not be persisted. TeamProjectId={TeamProjectId}", project.Id);
        }
        return Result<TeamProjectAuditDto>.Failure(project.LastSyncError);
    }

    private static bool IsSha(string value) => value.Length is >= 7 and <= 64 && value.All(Uri.IsHexDigit);

    private static bool CanRetryWithoutFilter(string error)
    {
        return error.Contains("filter", StringComparison.OrdinalIgnoreCase)
            && (error.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
                || error.Contains("not support", StringComparison.OrdinalIgnoreCase)
                || error.Contains("not recognized", StringComparison.OrdinalIgnoreCase));
    }

    private static string Sanitize(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var builder = new StringBuilder(Math.Min(value.Length, maximumLength));
        foreach (var character in value)
        {
            if (character != '\0' && !char.IsControl(character)) builder.Append(character);
            if (builder.Length == maximumLength) break;
        }

        return builder.ToString();
    }

    private static TeamProjectAuditDto ToAuditDto(TeamProject project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        RepositoryUrl = project.RepositoryUrl,
        CreatedByUserId = project.CreatedByUserId,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt,
        LastSyncedAt = project.LastSyncedAt,
        LastSyncAttemptAt = project.LastSyncAttemptAt,
        LastSyncStatus = project.LastSyncStatus,
        LastSyncError = project.LastSyncError,
        DefaultBranch = project.DefaultBranch
    };

    private static TeamProjectGitHistoryDto ToHistoryDto(TeamProject project, IReadOnlyList<TeamGitCommitDto> commits) => new()
    {
        LastSyncStatus = project.LastSyncStatus,
        LastSyncedAt = project.LastSyncedAt,
        LastSyncError = project.LastSyncStatus == TeamProjectSyncStatus.Failed ? project.LastSyncError : null,
        Commits = commits
    };
}
