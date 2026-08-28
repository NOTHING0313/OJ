namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonProblem
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid ProblemId { get; set; }

    public int BaseScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public Problem? Problem { get; set; }

    public List<LeaderboardUserProblemScore> UserScores { get; set; } = [];

    public List<LeaderboardSeasonProblemBenchmark> Benchmarks { get; set; } = [];
}
