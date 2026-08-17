using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeSandbox
{
    Task<JudgeResult> RunAsync(JudgeRequest request, LanguageJudgeProfile profile, CancellationToken cancellationToken = default);
}
