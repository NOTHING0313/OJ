namespace OnlineJudge.Domain.Entities;

public class SubmissionChoiceQuestionResult
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid RevisionQuestionId { get; set; }
    public bool IsCorrect { get; set; }
    public int Score { get; set; }
    public Submission? Submission { get; set; }
    public ProblemJudgeRevisionChoiceQuestion? RevisionQuestion { get; set; }
    public List<SubmissionChoiceSelection> Selections { get; set; } = [];
}
