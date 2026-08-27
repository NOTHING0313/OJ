namespace OnlineJudge.Application.Leaderboards.Dtos;

public class RankHistoryDto
{
    public IReadOnlyList<RankHistoryDayDto> Days { get; set; } = [];
}

public class RankHistoryDayDto
{
    public DateOnly Date { get; set; }

    public IReadOnlyList<RankHistoryEntryDto> Entries { get; set; } = [];
}

public class RankHistoryEntryDto
{
    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public int Rank { get; set; }

    public int TotalScore { get; set; }

    public int CompletedTaskCount { get; set; }

    public bool IsCurrentUser { get; set; }
}
