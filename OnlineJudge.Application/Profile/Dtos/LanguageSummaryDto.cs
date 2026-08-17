using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Profile.Dtos;

public class LanguageSummaryDto
{
    public JudgeLanguage Language { get; set; }

    public int SubmissionCount { get; set; }

    public int AcceptedCount { get; set; }
}
