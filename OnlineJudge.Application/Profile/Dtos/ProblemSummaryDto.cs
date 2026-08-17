namespace OnlineJudge.Application.Profile.Dtos;

public class ProblemSummaryDto
{
    public int AcceptedProblemCount { get; set; }

    public IReadOnlyList<AcceptedProblemDto> RecentAcceptedProblems { get; set; } = [];
}
