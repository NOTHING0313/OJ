namespace OnlineJudge.Application.Leaderboards.Requests;

public class AddLeaderboardSeasonProblemRequest
{
    public Guid ProblemId { get; set; }

    public int? BaseScore { get; set; }
}

public class AddLeaderboardSeasonProblemsRequest
{
    public List<Guid> ProblemIds { get; set; } = [];
}

public class RemoveLeaderboardSeasonProblemsRequest
{
    public List<Guid> ProblemIds { get; set; } = [];
}

public class UpdateLeaderboardSeasonProblemRequest
{
    public int BaseScore { get; set; }
}
