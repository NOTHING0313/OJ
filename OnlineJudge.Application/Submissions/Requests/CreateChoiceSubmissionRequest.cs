namespace OnlineJudge.Application.Submissions.Requests;

public class CreateChoiceSubmissionRequest
{
    public Guid ProblemId { get; set; }
    public Guid ProblemJudgeRevisionId { get; set; }
    public IReadOnlyList<ChoiceQuestionAnswerRequest> Answers { get; set; } = [];
}

public class ChoiceQuestionAnswerRequest
{
    public Guid QuestionId { get; set; }
    public IReadOnlyList<Guid> OptionIds { get; set; } = [];
}
