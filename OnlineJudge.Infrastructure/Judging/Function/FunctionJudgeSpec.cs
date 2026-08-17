namespace OnlineJudge.Infrastructure.Judging.Function;

internal sealed record FunctionJudgeSpec(
    string FunctionName,
    string ReturnType,
    IReadOnlyList<FunctionParameterSpec> Parameters);

internal sealed record FunctionParameterSpec(string Name, string Type);
