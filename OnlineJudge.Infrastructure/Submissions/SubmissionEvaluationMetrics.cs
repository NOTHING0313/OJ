using OnlineJudge.Application.Submissions.Dtos;
using OnlineJudge.Domain.Entities;

namespace OnlineJudge.Infrastructure.Submissions;

internal static class SubmissionEvaluationMetrics
{
    public static SubmissionEvaluationDto FromCaseResults(IEnumerable<SubmissionCaseResult> caseResults)
    {
        ArgumentNullException.ThrowIfNull(caseResults);

        var values = caseResults.ToList();
        var times = values.Where(item => item.TimeUsedMs.HasValue).Select(item => item.TimeUsedMs!.Value).ToList();
        var memories = values.Where(item => item.MemoryUsedKb.HasValue).Select(item => item.MemoryUsedKb!.Value).ToList();

        return new SubmissionEvaluationDto
        {
            MaxTimeUsedMs = times.Count == 0 ? null : times.Max(),
            AverageCaseTimeUsedMs = times.Count == 0 ? null : Round(times.Average(value => (decimal)value)),
            MaxMemoryUsedKb = memories.Count == 0 ? null : memories.Max(),
            AverageCaseMemoryUsedKb = memories.Count == 0 ? null : Round(memories.Average(value => (decimal)value))
        };
    }

    public static void RoundAverages(SubmissionEvaluationDto evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        evaluation.AverageCaseTimeUsedMs = Round(evaluation.AverageCaseTimeUsedMs);
        evaluation.AverageCaseMemoryUsedKb = Round(evaluation.AverageCaseMemoryUsedKb);
    }

    private static decimal? Round(decimal? value) => value.HasValue ? Round(value.Value) : null;

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
