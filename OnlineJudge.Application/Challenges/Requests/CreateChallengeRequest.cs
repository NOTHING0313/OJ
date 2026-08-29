namespace OnlineJudge.Application.Challenges.Requests;

using OnlineJudge.Domain.Enums;

public class CreateChallengeRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public bool IsPublished { get; set; }

    public ChallengeParticipationMode ParticipationMode { get; set; } = ChallengeParticipationMode.Individual;

    public bool PeerReviewEnabled { get; set; }

    public DateTimeOffset? PeerReviewEndAt { get; set; }
}
