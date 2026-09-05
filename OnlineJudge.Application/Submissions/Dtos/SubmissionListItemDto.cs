using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Submissions.Dtos;

public class SubmissionListItemDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public SubmissionKind SubmissionKind { get; set; }

    public JudgeLanguage? Language { get; set; }

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public SubmissionEvaluationDto Evaluation { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public int? ChoiceScore { get; set; }

    public int? ChoiceTotalScore { get; set; }
}
