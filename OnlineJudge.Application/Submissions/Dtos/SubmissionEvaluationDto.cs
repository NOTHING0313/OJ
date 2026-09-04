namespace OnlineJudge.Application.Submissions.Dtos;

/// <summary>
/// Aggregated resource measurements for the test cases executed by a submission.
/// </summary>
public class SubmissionEvaluationDto
{
    public int? MaxTimeUsedMs { get; set; }

    public decimal? AverageCaseTimeUsedMs { get; set; }

    public int? MaxMemoryUsedKb { get; set; }

    public decimal? AverageCaseMemoryUsedKb { get; set; }
}
