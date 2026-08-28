using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class Submission
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ChallengeTaskId { get; set; }

    public Guid? ChallengeTeamParticipantId { get; set; }

    public JudgeLanguage Language { get; set; }

    /// <summary>
    /// Source code submitted by the user for judging.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    /// <summary>
    /// Compile, runtime, or system error details returned by the judge pipeline.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public Problem? Problem { get; set; }

    public User? User { get; set; }

    public ChallengeTask? ChallengeTask { get; set; }

    public ChallengeTeamParticipant? ChallengeTeamParticipant { get; set; }

    public List<SubmissionCaseResult> CaseResults { get; set; } = [];
}
