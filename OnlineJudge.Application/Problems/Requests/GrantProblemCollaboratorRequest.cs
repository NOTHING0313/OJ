namespace OnlineJudge.Application.Problems.Requests;

public class GrantProblemCollaboratorRequest
{
    public Guid UserId { get; set; }

    public bool CanEditProblem { get; set; }

    public bool CanManageTestCases { get; set; }
}
