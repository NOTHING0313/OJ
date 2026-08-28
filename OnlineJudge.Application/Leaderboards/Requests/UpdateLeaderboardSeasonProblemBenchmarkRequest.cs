namespace OnlineJudge.Application.Leaderboards.Requests;

public sealed class UpdateLeaderboardSeasonProblemBenchmarkRequest
{
    public int RuntimeBaselineMs { get; set; }

    public int MemoryBaselineKb { get; set; }
}
