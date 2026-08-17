using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Submissions.Dtos;

public class SubmissionDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public Guid? ChallengeTaskId { get; set; }

    public JudgeLanguage Language { get; set; }

    public string SourceCode { get; set; } = string.Empty;

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public IReadOnlyList<SubmissionCaseResultDto> CaseResults { get; set; } = [];
}
