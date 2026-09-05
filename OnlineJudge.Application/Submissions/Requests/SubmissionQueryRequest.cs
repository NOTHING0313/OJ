using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Submissions.Requests;

public class SubmissionQueryRequest
{
    public bool? Mine { get; set; }

    public Guid? UserId { get; set; }

    public Guid? ProblemId { get; set; }

    public JudgeStatus? Status { get; set; }

    [System.ComponentModel.DataAnnotations.EnumDataType(typeof(SubmissionKind))]
    public SubmissionKind? SubmissionKind { get; set; }

    public JudgeLanguage? Language { get; set; }

    public string? ProblemKeyword { get; set; }

    public string? UserKeyword { get; set; }

    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
