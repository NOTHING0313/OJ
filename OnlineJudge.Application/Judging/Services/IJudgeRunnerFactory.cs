using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Judging.Services;

public interface IJudgeRunnerFactory
{
    IJudgeRunner GetRunner(JudgeLanguage language);
}
