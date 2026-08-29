using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengePeerReviewAdminDto
{
    public Guid AssignmentId { get; set; }
    public string ReviewerTeam { get; set; } = string.Empty;
    public string TargetTeam { get; set; } = string.Empty;
    public string TargetProjectName { get; set; } = string.Empty;
    public string TargetRepositoryUrl { get; set; } = string.Empty;
    public ChallengePeerReviewStatus? ReviewStatus { get; set; }
    public int? OverallScore { get; set; }
    public string? Summary { get; set; }
    public string? Strengths { get; set; }
    public string? Improvements { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public IReadOnlyList<string> ReviewerRoster { get; set; } = [];
}

public class ChallengePeerReviewAdminSummaryDto
{
    public int AssignmentCount { get; set; }
    public int SubmittedCount { get; set; }
    public IReadOnlyList<ChallengePeerReviewAdminDto> Assignments { get; set; } = [];
}
