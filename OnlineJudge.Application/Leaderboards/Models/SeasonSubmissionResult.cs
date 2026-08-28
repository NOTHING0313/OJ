using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Leaderboards.Models;

public sealed record SeasonSubmissionResult(
    Guid SubmissionId,
    Guid ProblemId,
    Guid UserId,
    JudgeLanguage Language,
    JudgeStatus Status,
    int? RuntimeMs,
    int? MemoryKb,
    DateTimeOffset CreatedAt,
    DateTimeOffset FinishedAt)
{
    public SeasonSubmissionResult(
        Guid submissionId,
        Guid problemId,
        Guid userId,
        JudgeLanguage language,
        JudgeStatus status,
        int? runtimeMs,
        int? memoryKb,
        DateTimeOffset finishedAt)
        : this(submissionId, problemId, userId, language, status, runtimeMs, memoryKb, finishedAt, finishedAt)
    {
    }
}
