namespace OnlineJudge.Domain.Entities;

public class ProblemCollaborator
{
    public Guid Id { get; set; }

    public Guid ProblemId { get; set; }

    public Guid UserId { get; set; }

    public Guid GrantedByUserId { get; set; }

    public bool CanEditProblem { get; set; }

    public bool CanManageTestCases { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Problem? Problem { get; set; }

    public User? User { get; set; }

    public User? GrantedByUser { get; set; }
}
