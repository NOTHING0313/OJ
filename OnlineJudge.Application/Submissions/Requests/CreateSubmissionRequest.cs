using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Submissions.Requests;

public class CreateSubmissionRequest
{
    public Guid ProblemId { get; set; }

    public Guid UserId { get; set; }

    public Guid? ChallengeTaskId { get; set; }

    public JudgeLanguage Language { get; set; }

    public string SourceCode { get; set; } = string.Empty;
}
