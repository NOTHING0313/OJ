using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengePeerReviewDto
{
    public ChallengePeerReviewStatus Status { get; set; }
    public int? OverallScore { get; set; }
    public string? Summary { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
