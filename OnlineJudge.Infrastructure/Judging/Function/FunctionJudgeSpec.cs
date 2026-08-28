namespace OnlineJudge.Infrastructure.Judging.Function;

internal sealed record FunctionJudgeSpec(
    string FunctionName,
    string ReturnType,
    IReadOnlyList<FunctionParameterSpec> Parameters)
{
    public IReadOnlyList<FunctionCustomTypeSpec> Types { get; init; } = [];

    public FunctionCustomTypeSpec? FindCustomType(string name)
    {
        return Types.FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.Ordinal));
    }
}

internal sealed record FunctionParameterSpec(string Name, string Type);

internal sealed record FunctionCustomTypeSpec(
    string Name,
    IReadOnlyList<FunctionCustomTypeFieldSpec> Fields);

internal sealed record FunctionCustomTypeFieldSpec(string Name, string Type);
