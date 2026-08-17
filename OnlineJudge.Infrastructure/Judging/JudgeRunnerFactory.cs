using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Infrastructure.Judging;

public class JudgeRunnerFactory(IEnumerable<IJudgeRunner> runners) : IJudgeRunnerFactory
{
    public IJudgeRunner GetRunner(JudgeLanguage language)
    {
        return runners.FirstOrDefault(runner => runner.Supports(language))
            ?? throw new InvalidOperationException($"No judge runner registered for language '{language}'.");
    }
}
