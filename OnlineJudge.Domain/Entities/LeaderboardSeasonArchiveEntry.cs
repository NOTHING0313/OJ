namespace OnlineJudge.Domain.Entities;

public class LeaderboardSeasonArchiveEntry
{
    public Guid Id { get; set; }

    public Guid SeasonId { get; set; }

    public Guid UserId { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public bool WasAnonymous { get; set; }

    public int FinalRank { get; set; }

    public int FinalScore { get; set; }

    public int FinalBaseScore { get; set; }

    public int FinalTimeBonus { get; set; }

    public int FinalRuntimeBonus { get; set; }

    public int FinalMemoryBonus { get; set; }

    public int SolvedCount { get; set; }

    public DateTimeOffset LastScoreImprovedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public LeaderboardSeason? Season { get; set; }

    public User? User { get; set; }

    public List<LeaderboardSeasonArchiveProblemScore> ProblemScores { get; set; } = [];
}
