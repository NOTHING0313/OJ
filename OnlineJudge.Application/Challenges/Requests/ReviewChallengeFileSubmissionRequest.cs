namespace OnlineJudge.Application.Challenges.Requests;

public class ReviewChallengeFileSubmissionRequest
{
    public int Score { get; set; }

    public string? Comment { get; set; }
}
