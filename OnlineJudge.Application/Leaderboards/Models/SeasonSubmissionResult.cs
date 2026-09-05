using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Leaderboards.Models;

public sealed record SeasonSubmissionResult(
    Guid SubmissionId,
    Guid ProblemId,
    Guid UserId,
    JudgeLanguage? Language,
    JudgeStatus Status,
    int? RuntimeMs,
    int? MemoryKb,
    DateTimeOffset CreatedAt,
    DateTimeOffset FinishedAt,
    SubmissionKind SubmissionKind)
{
    public SeasonSubmissionResult(
        Guid submissionId,
        Guid problemId,
        Guid userId,
        JudgeLanguage language,
        JudgeStatus status,
        int? runtimeMs,
        int? memoryKb,
        DateTimeOffset createdAt,
        DateTimeOffset finishedAt)
        : this(submissionId, problemId, userId, language, status, runtimeMs, memoryKb, createdAt, finishedAt, SubmissionKind.Code)
    {
    }

    public SeasonSubmissionResult(
        Guid submissionId,
        Guid problemId,
        Guid userId,
        JudgeLanguage language,
        JudgeStatus status,
        int? runtimeMs,
        int? memoryKb,
        DateTimeOffset finishedAt)
        : this(submissionId, problemId, userId, language, status, runtimeMs, memoryKb, finishedAt, finishedAt, SubmissionKind.Code)
    {
    }

    public static SeasonSubmissionResult ForChoice(
        Guid submissionId,
        Guid problemId,
        Guid userId,
        JudgeStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset finishedAt) =>
        new(submissionId, problemId, userId, null, status, null, null, createdAt, finishedAt, SubmissionKind.Choice);
}
