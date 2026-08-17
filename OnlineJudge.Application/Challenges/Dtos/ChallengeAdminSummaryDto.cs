namespace OnlineJudge.Application.Challenges.Dtos;

public class ChallengeAdminSummaryDto
{
    public Guid ChallengeId { get; set; }

    public string ChallengeTitle { get; set; } = string.Empty;

    public int TotalTaskCount { get; set; }

    /// <summary>
    /// 进入过该挑战或已有完成记录的用户数。
    /// </summary>
    public int ParticipantCount { get; set; }

    public int TotalCompletionCount { get; set; }

    public IReadOnlyList<ChallengeAdminUserProgressDto> Users { get; set; } = [];

    public IReadOnlyList<ChallengeAdminTaskProgressDto> Tasks { get; set; } = [];
}
