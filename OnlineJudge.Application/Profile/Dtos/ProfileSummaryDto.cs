namespace OnlineJudge.Application.Profile.Dtos;

public class ProfileSummaryDto
{
    public ProfileUserDto User { get; set; } = new();

    public SubmissionSummaryDto SubmissionSummary { get; set; } = new();

    public ProblemSummaryDto ProblemSummary { get; set; } = new();

    public IReadOnlyList<LanguageSummaryDto> LanguageSummary { get; set; } = [];

    public ChallengeProfileSummaryDto ChallengeSummary { get; set; } = new();

    public IReadOnlyList<RecentSubmissionDto> RecentSubmissions { get; set; } = [];

    public IReadOnlyList<RecentChallengeCompletionDto> RecentChallengeCompletions { get; set; } = [];

    public IReadOnlyList<RecentFileReviewDto> RecentFileReviews { get; set; } = [];
}
