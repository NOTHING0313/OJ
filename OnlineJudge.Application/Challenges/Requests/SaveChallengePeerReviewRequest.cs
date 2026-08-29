namespace OnlineJudge.Application.Challenges.Requests;

public class SaveChallengePeerReviewRequest
{
    public int? OverallScore { get; set; }
    public string? Summary { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
}
