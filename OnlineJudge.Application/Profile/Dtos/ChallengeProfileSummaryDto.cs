namespace OnlineJudge.Application.Profile.Dtos;

public class ChallengeProfileSummaryDto
{
    public int ParticipatedChallengeCount { get; set; }

    public int CompletedTaskCount { get; set; }

    public int TotalScore { get; set; }

    public DateTimeOffset? LastCompletedAt { get; set; }
}
