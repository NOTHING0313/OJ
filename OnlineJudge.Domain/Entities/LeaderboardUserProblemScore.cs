namespace OnlineJudge.Domain.Entities;

public class LeaderboardUserProblemScore
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid SeasonProblemId { get; set; }

    public Guid ProblemId { get; set; }

    public Guid UserId { get; set; }

    public int BestBaseScore { get; set; }

    public bool IsFullScore { get; set; }

    public DateTimeOffset? FirstFullScoreAt { get; set; }

    public Guid? FirstFullSubmissionId { get; set; }

    public Guid? BestPerformanceSubmissionId { get; set; }

    public OnlineJudge.Domain.Enums.JudgeLanguage? BestPerformanceLanguage { get; set; }

    public int? BestRuntimeMs { get; set; }

    public int? BestMemoryKb { get; set; }

    public DateTimeOffset? BestPerformanceFinishedAt { get; set; }

    public DateTimeOffset LastScoreImprovedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public LeaderboardSeasonProblem? SeasonProblem { get; set; }

    public Problem? Problem { get; set; }

    public User? User { get; set; }

    public Submission? BestPerformanceSubmission { get; set; }

    public Submission? FirstFullSubmission { get; set; }
}
