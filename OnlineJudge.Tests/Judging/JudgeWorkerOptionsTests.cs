using Microsoft.Extensions.Configuration;
using OnlineJudge.JudgeWorker;

namespace OnlineJudge.Tests.Judging;

public class JudgeWorkerOptionsTests
{
    [Fact]
    public void MissingConcurrency_DefaultsToOne()
    {
        var configuration = new ConfigurationBuilder().Build();

        var options = JudgeWorkerOptions.FromConfiguration(configuration);

        Assert.Equal(1, options.Concurrency);
    }

    [Fact]
    public void ConcurrencyTwo_IsAccepted()
    {
        var configuration = Configuration("2");

        var options = JudgeWorkerOptions.FromConfiguration(configuration);

        Assert.Equal(2, options.Concurrency);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("invalid")]
    public void OutOfRangeOrInvalidConcurrency_FailsStartup(string configured)
    {
        var configuration = Configuration(configured);

        var exception = Assert.Throws<InvalidOperationException>(() => JudgeWorkerOptions.FromConfiguration(configuration));

        Assert.Contains("between 1 and 2", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(string concurrency) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JudgeWorker:Concurrency"] = concurrency
            })
            .Build();
}
