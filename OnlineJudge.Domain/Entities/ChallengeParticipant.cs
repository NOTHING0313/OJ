namespace OnlineJudge.Domain.Entities;

public class ChallengeParticipant
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public Challenge? Challenge { get; set; }

    public User? User { get; set; }
}
