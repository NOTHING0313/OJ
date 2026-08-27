using OnlineJudge.Application.Judging.Models;

namespace OnlineJudge.Infrastructure.Judging.Sandbox;

internal static class JudgeResourceLimits
{
    private const int MinimumContainerMemoryLimitMb = 16;

    public static int ResolveCompileMemoryLimitMb(LanguageJudgeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Math.Max(profile.CompileMemoryLimitMb, MinimumContainerMemoryLimitMb);
    }

    public static int ResolveRunMemoryLimitMb(int problemMemoryLimitMb)
    {
        return Math.Max(problemMemoryLimitMb, MinimumContainerMemoryLimitMb);
    }
}
