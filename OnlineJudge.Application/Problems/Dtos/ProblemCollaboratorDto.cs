namespace OnlineJudge.Application.Problems.Dtos;

public class ProblemCollaboratorDto
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public Guid GrantedByUserId { get; set; }

    public string GrantedByUserName { get; set; } = string.Empty;

    public bool CanEditProblem { get; set; }

    public bool CanManageTestCases { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
