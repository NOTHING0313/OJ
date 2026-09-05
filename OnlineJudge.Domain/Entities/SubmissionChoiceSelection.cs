namespace OnlineJudge.Domain.Entities;

public class SubmissionChoiceSelection
{
    public Guid QuestionResultId { get; set; }
    public Guid RevisionOptionId { get; set; }
    public SubmissionChoiceQuestionResult? QuestionResult { get; set; }
    public ProblemJudgeRevisionChoiceOption? RevisionOption { get; set; }
}
