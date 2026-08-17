namespace OnlineJudge.Domain.Enums;

public enum JudgeStatus
{
    Pending = 1,
    Judging = 2,
    Accepted = 3,
    WrongAnswer = 4,
    TimeLimitExceeded = 5,
    MemoryLimitExceeded = 6,
    RuntimeError = 7,
    CompileError = 8,
    SystemError = 9
}
