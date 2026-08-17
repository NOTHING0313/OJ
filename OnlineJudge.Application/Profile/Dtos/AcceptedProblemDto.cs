namespace OnlineJudge.Application.Profile.Dtos;

public class AcceptedProblemDto
{
    public Guid ProblemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset AcceptedAt { get; set; }
}
