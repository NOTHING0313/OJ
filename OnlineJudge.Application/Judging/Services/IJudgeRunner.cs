using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeRunner
{
    bool Supports(JudgeLanguage language);

    Task<JudgeResult> RunAsync(JudgeRequest request, CancellationToken cancellationToken = default);
}
