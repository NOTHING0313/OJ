using Microsoft.Extensions.Configuration;

namespace OnlineJudge.JudgeWorker;

internal sealed class JudgeWorkerOptions
{
    public const string SectionName = "JudgeWorker";

    public const int MaximumConcurrency = 2;

    public int Concurrency { get; init; } = 1;

    public static JudgeWorkerOptions FromConfiguration(IConfiguration configuration)
    {
        var configured = configuration[$"{SectionName}:{nameof(Concurrency)}"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return new JudgeWorkerOptions();
        }

        if (!int.TryParse(configured, out var concurrency) || concurrency < 1 || concurrency > MaximumConcurrency)
        {
            throw new InvalidOperationException($"{SectionName} concurrency must be between 1 and {MaximumConcurrency}.");
        }

        return new JudgeWorkerOptions { Concurrency = concurrency };
    }
}
