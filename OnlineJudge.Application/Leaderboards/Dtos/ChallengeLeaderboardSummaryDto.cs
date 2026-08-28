namespace OnlineJudge.Application.Leaderboards.Dtos;

using OnlineJudge.Domain.Enums;

public class ChallengeLeaderboardSummaryDto
{
    public Guid ChallengeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int TotalTaskCount { get; set; }

    public int ParticipantCount { get; set; }

    public int TeamCount { get; set; }

    public ChallengeParticipationMode ParticipationMode { get; set; }

    public int CompletedUserCount { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public bool IsPublished { get; set; }

    public IReadOnlyList<ChallengeLeaderboardTopEntryDto> TopEntries { get; set; } = [];
}
