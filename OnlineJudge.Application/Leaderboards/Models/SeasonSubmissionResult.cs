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
    DateTimeOffset FinishedAt);
