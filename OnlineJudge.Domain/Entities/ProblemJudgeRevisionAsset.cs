namespace OnlineJudge.Domain.Entities;

/// <summary>
/// Immutable association between a judge revision and an integrity-checked judge asset record.
/// </summary>
public class ProblemJudgeRevisionAsset
{
    public Guid ProblemJudgeRevisionId { get; set; }

    public Guid ProblemJudgeAssetId { get; set; }

    public int Order { get; set; }

    public ProblemJudgeRevision? ProblemJudgeRevision { get; set; }

    public ProblemJudgeAsset? ProblemJudgeAsset { get; set; }
}
