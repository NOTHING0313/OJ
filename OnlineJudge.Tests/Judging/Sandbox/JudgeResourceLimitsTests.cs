using OnlineJudge.Application.Judging.Models;
using OnlineJudge.Infrastructure.Judging.Sandbox;

namespace OnlineJudge.Tests.Judging.Sandbox;

public class JudgeResourceLimitsTests
{
    [Fact]
    public void ResolveCompileMemoryLimitMb_UsesLanguageCompileLimit()
    {
        var profile = new LanguageJudgeProfile { CompileMemoryLimitMb = 512 };

        Assert.Equal(512, JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile));
        Assert.Equal(64, JudgeResourceLimits.ResolveRunMemoryLimitMb(64));
    }

    [Fact]
    public void ResolveCompileMemoryLimitMb_DoesNotUseProblemRunLimit()
    {
        var profile = new LanguageJudgeProfile { CompileMemoryLimitMb = 1024 };

        Assert.Equal(1024, JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile));
        Assert.Equal(32, JudgeResourceLimits.ResolveRunMemoryLimitMb(32));
    }

    [Fact]
    public void ResolveLimits_KeepMinimumContainerMemory()
    {
        var profile = new LanguageJudgeProfile { CompileMemoryLimitMb = 0 };

        Assert.Equal(16, JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile));
        Assert.Equal(16, JudgeResourceLimits.ResolveRunMemoryLimitMb(0));
    }
}
