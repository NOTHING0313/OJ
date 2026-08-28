using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Domain.Entities;

public class Challenge
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full challenge description shown before entering the board.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public Guid CreatedByUserId { get; set; }

    public bool IsPublished { get; set; }

    public ChallengeParticipationMode ParticipationMode { get; set; } = ChallengeParticipationMode.Individual;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }

    public List<ChallengeTask> Tasks { get; set; } = [];

    public List<ChallengeTaskCompletion> Completions { get; set; } = [];

    public List<ChallengeParticipant> Participants { get; set; } = [];

    public List<ChallengeTeamParticipant> TeamParticipants { get; set; } = [];

    public List<ChallengeTeamRosterMember> TeamRosterMembers { get; set; } = [];

    public List<ChallengeTeamTaskCompletion> TeamTaskCompletions { get; set; } = [];

    public List<ChallengeTaskAnswer> Answers { get; set; } = [];

    public List<ChallengeTaskFileSubmission> FileSubmissions { get; set; } = [];
}
