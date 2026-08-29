namespace OnlineJudge.Tests.Auth;

public class AuthStudioUxTests
{
    [Fact]
    public void RoutesAndExistingAuthCallsRemainUnchanged()
    {
        var main = Read("frontend", "src", "main.tsx");
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");
        var register = Read("frontend", "src", "pages", "RegisterPage.tsx");

        Assert.Contains("path=\"/login\" element={<LoginPage />}", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/register\" element={<RegisterPage />}", main, StringComparison.Ordinal);
        Assert.Contains("await login(account, password)", login, StringComparison.Ordinal);
        Assert.Contains("await register({", register, StringComparison.Ordinal);
        Assert.Contains("await sendRegisterEmailCode(email.trim())", register, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedLayoutOwnsStudioIdentityWithoutBusinessStatusRequests()
    {
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");
        var register = Read("frontend", "src", "pages", "RegisterPage.tsx");
        var layout = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx");

        Assert.Contains("<AuthStudioLayout", login, StringComparison.Ordinal);
        Assert.Contains("<AuthStudioLayout", register, StringComparison.Ordinal);
        Assert.Contains("<AuthParticleField />", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("getCurrentSeasonPublicSummary", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SeasonRefreshIntervalMs", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("getProblems", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("getChallenges", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthLayoutDoesNotRenderSeasonPresentation()
    {
        var layout = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx");

        Assert.DoesNotContain("STARTS IN", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVE", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("RESULTS PUBLIC", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-studio-season", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordLoadingAndErrorsRemainAccessible()
    {
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");
        var register = Read("frontend", "src", "pages", "RegisterPage.tsx");
        var password = Read("frontend", "src", "components", "PasswordInput.tsx");

        Assert.Contains("autoComplete=\"username\"", login, StringComparison.Ordinal);
        Assert.Contains("autoComplete=\"current-password\"", login, StringComparison.Ordinal);
        Assert.Contains("autoComplete=\"new-password\"", register, StringComparison.Ordinal);
        Assert.Contains("aria-label={isVisible ? \"隐藏密码\" : \"显示密码\"}", password, StringComparison.Ordinal);
        Assert.Contains("aria-busy={isSubmitting}", login, StringComparison.Ordinal);
        Assert.Contains("disabled={isSubmitting}", login, StringComparison.Ordinal);
        Assert.Contains("disabled={isSubmitting}", register, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", login, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", register, StringComparison.Ordinal);
    }

    [Fact]
    public void LabelsAreExplicitAndRoleSpecificEntrypointsAreAbsent()
    {
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");
        var register = Read("frontend", "src", "pages", "RegisterPage.tsx");

        Assert.Contains("htmlFor=\"login-account\"", login, StringComparison.Ordinal);
        Assert.Contains("htmlFor=\"login-password\"", login, StringComparison.Ordinal);
        Assert.Contains("htmlFor=\"register-email\"", register, StringComparison.Ordinal);
        Assert.DoesNotContain("Root", login + register, StringComparison.Ordinal);
        Assert.DoesNotContain("ProblemSetter", login + register, StringComparison.Ordinal);
        Assert.DoesNotContain("管理员登录", login + register, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthUiDoesNotRenderCredentialsOrInfrastructureDetails()
    {
        var source = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx")
            + Read("frontend", "src", "pages", "LoginPage.tsx")
            + Read("frontend", "src", "pages", "RegisterPage.tsx");

        Assert.DoesNotContain("accessToken", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JWT", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResponsiveAndReducedMotionRulesAreScoped()
    {
        var styles = Read("frontend", "src", "styles.css");

        Assert.Contains("@media (max-width: 1199px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
        Assert.Contains(".auth-studio-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".auth-studio-form", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualRevision_RemovesMarketingCopyAndPreservesOriginalLogo()
    {
        var layout = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx");

        Assert.DoesNotContain("STUDIO DEVELOPMENT TERMINAL", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("BUILD // COMPETE // EVOLVE", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("在同一套可靠的判题系统中构建代码", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SYSTEM STATUS", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("JUDGE SYSTEM", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SANDBOXED EXECUTION", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SEASON COMPETITION", layout, StringComparison.Ordinal);
        Assert.DoesNotContain(">CODE<", layout, StringComparison.Ordinal);
        Assert.DoesNotContain(">DESIGN<", layout, StringComparison.Ordinal);
        Assert.DoesNotContain(">SYSTEMS<", layout, StringComparison.Ordinal);
        Assert.DoesNotContain(">COMPETE<", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SYS.01", layout, StringComparison.Ordinal);
        Assert.Equal(1, Count(layout, "/brand/unrealstudio-logo.png"));
        Assert.DoesNotContain("data:image", layout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VisualRevision_UsesLogoMaskCanvasAndDarkAutofillWithoutHeavyDependencies()
    {
        var layout = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx");
        var particles = Read("frontend", "src", "components", "auth", "AuthParticleField.tsx");
        var styles = Read("frontend", "src", "styles.css");
        var package = Read("frontend", "package.json");

        Assert.Contains("AuthParticleField", layout, StringComparison.Ordinal);
        Assert.Contains("getContext(\"2d\"", particles, StringComparison.Ordinal);
        Assert.Contains("document.createElement(\"canvas\")", particles, StringComparison.Ordinal);
        Assert.Contains("getImageData", particles, StringComparison.Ordinal);
        Assert.Contains("LogoAssetPath = \"/brand/unrealstudio-logo.png\"", particles, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", particles, StringComparison.Ordinal);
        Assert.Contains(".auth-studio-particle-field", styles, StringComparison.Ordinal);
        Assert.Contains("radial-gradient", styles, StringComparison.Ordinal);
        Assert.Contains("input:-webkit-autofill", styles, StringComparison.Ordinal);
        Assert.Contains("box-shadow: 0 0 0 1000px #0a0d14 inset", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("webgl", layout + particles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("three", package, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pixi", package, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("particle", package, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("framer-motion", package, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gsap", package, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParticleField_HasBoundedLifecyclesAndRequiredRuntimeGuards()
    {
        var particles = Read("frontend", "src", "components", "auth", "AuthParticleField.tsx");

        Assert.Contains("LogoParticleCap = 620", particles, StringComparison.Ordinal);
        Assert.Contains("FreeParticleCap = 110", particles, StringComparison.Ordinal);
        Assert.Contains("MaxShards = 18", particles, StringComparison.Ordinal);
        Assert.Contains("MaxShardFragments = 12", particles, StringComparison.Ordinal);
        Assert.Contains("requestAnimationFrame", particles, StringComparison.Ordinal);
        Assert.Contains("cancelAnimationFrame", particles, StringComparison.Ordinal);
        Assert.Contains("document.hidden", particles, StringComparison.Ordinal);
        Assert.Contains("visibilitychange", particles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", particles, StringComparison.Ordinal);
        Assert.Contains("pointer: coarse", particles, StringComparison.Ordinal);
        Assert.Contains("max-width: 767px", particles, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", particles, StringComparison.Ordinal);
        Assert.Contains("RepulsionRadius = 128", particles, StringComparison.Ordinal);
        Assert.DoesNotContain("shadow", particles, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ripple", particles, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeaderboardRoutesRemainRegistered()
    {
        var main = Read("frontend", "src", "main.tsx");

        Assert.Contains("path=\"/leaderboards\"", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/leaderboards/users\"", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/leaderboards/history\"", main, StringComparison.Ordinal);
        Assert.Contains("<ProtectedRoute>", main, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static int Count(string source, string value) =>
        (source.Length - source.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
