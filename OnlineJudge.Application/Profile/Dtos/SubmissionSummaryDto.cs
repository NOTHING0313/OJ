namespace OnlineJudge.Application.Profile.Dtos;

public class SubmissionSummaryDto
{
    public int TotalSubmissionCount { get; set; }

    public int AcceptedSubmissionCount { get; set; }

    public int WrongAnswerCount { get; set; }

    public int CompileErrorCount { get; set; }

    public int RuntimeErrorCount { get; set; }

    public int SystemErrorCount { get; set; }

    public double AcceptedRate { get; set; }

    public DateTimeOffset? LastSubmittedAt { get; set; }
}
