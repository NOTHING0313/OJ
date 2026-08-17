using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Profile.Dtos;

public class RecentSubmissionDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public string ProblemTitle { get; set; } = string.Empty;

    public JudgeLanguage Language { get; set; }

    public JudgeStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
