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

    public SubmissionKind SubmissionKind { get; set; }

    public JudgeLanguage? Language { get; set; }

    public string? SourceCode { get; set; }

    public JudgeStatus Status { get; set; }

    public int? TimeUsedMs { get; set; }

    public int? MemoryUsedKb { get; set; }

    public SubmissionEvaluationDto Evaluation { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public IReadOnlyList<SubmissionCaseResultDto> CaseResults { get; set; } = [];

    public int? ChoiceScore { get; set; }

    public int? ChoiceTotalScore { get; set; }

    public bool? AnswersRevealed { get; set; }

    public ChoiceAnswerRevealPolicy? ChoiceAnswerRevealPolicy { get; set; }

    public DateTimeOffset? ChoiceAnswerRevealAt { get; set; }

    public IReadOnlyList<ChoiceQuestionResultDto> ChoiceQuestionResults { get; set; } = [];
}
