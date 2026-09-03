namespace OnlineJudge.Tests.Auth;

public class SingleActiveSessionFrontendContractTests
{
    [Fact]
    public void ApiClient_HandlesAuthenticationErrorsOnceAndDoesNotTreatForbiddenAsLogout()
    {
        var client = Read("frontend", "src", "api", "httpClient.ts");

        Assert.Contains("response.status === 401", client, StringComparison.Ordinal);
        Assert.Contains("error.errorCode?.startsWith(\"AUTH_\")", client, StringComparison.Ordinal);
        Assert.Contains("authenticationErrorHandled", client, StringComparison.Ordinal);
        Assert.DoesNotContain("response.status === 403 &&", client, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClient_HandlesRateLimitsWithoutTriggeringAuthenticationLogout()
    {
        var client = Read("frontend", "src", "api", "httpClient.ts");

        Assert.Contains("retryAfterSeconds?: number", client, StringComparison.Ordinal);
        Assert.Contains("error.status === 429", client, StringComparison.Ordinal);
        Assert.Contains("请在 ${retryAfterSeconds} 秒后重试。", client, StringComparison.Ordinal);
        Assert.DoesNotContain("response.status === 429 && error.errorCode?.startsWith(\"AUTH_\")", client, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthContext_UsesServerSessionWithoutPersistingCredentialsAndSynchronizesTabs()
    {
        var context = Read("frontend", "src", "auth", "AuthContext.tsx");

        Assert.Contains("redirectGuard.current", context, StringComparison.Ordinal);
        Assert.Contains("new BroadcastChannel(\"onlinejudge-session\")", context, StringComparison.Ordinal);
        Assert.Contains("const user = await me()", context, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", context, StringComparison.Ordinal);
        Assert.DoesNotContain("accessToken", context, StringComparison.Ordinal);
        Assert.Contains("AUTH_SESSION_REPLACED", context, StringComparison.Ordinal);
        Assert.Contains("AUTH_TOKEN_EXPIRED", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_ShowsRequiredSessionAndExpirationMessages()
    {
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");

        Assert.Contains("账号已在其他设备登录，请重新登录。", login, StringComparison.Ordinal);
        Assert.Contains("登录状态已失效，请重新登录。", login, StringComparison.Ordinal);
        Assert.Contains("登录已过期，请重新登录。", login, StringComparison.Ordinal);
        Assert.Contains("密码已修改，请重新登录。", login, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", login, StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_UsesServerEndpointWithoutPerPageAuthenticationHandlers()
    {
        var authApi = Read("frontend", "src", "api", "authApi.ts");
        var source = Read("frontend", "src", "auth", "AuthContext.tsx")
            + Read("frontend", "src", "AppLayout.tsx");

        Assert.Contains("/api/auth/logout", authApi, StringComparison.Ordinal);
        Assert.Contains("/api/auth/session", authApi, StringComparison.Ordinal);
        Assert.Contains("/api/auth/login", authApi, StringComparison.Ordinal);
        Assert.Contains("await logoutRequest()", source, StringComparison.Ordinal);
        Assert.Contains("error.status !== 401", source, StringComparison.Ordinal);
        Assert.Contains("throw error", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AUTH_SESSION_REPLACED", Read("frontend", "src", "AppLayout.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticationEndpoints_HaveARequestBodyLimit()
    {
        var controller = Read("OnlineJudge.Api", "Controllers", "AuthController.cs");

        Assert.Contains("[RequestSizeLimit(16 * 1024)]", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiClient_UsesSameOriginCookiesAndCsrfForUnsafeCookieRequests()
    {
        var client = Read("frontend", "src", "api", "httpClient.ts");

        Assert.Contains("credentials: \"same-origin\"", client, StringComparison.Ordinal);
        Assert.Contains("X-CSRF-TOKEN", client, StringComparison.Ordinal);
        Assert.Contains("!headers.has(\"Authorization\")", client, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage.getItem(\"accessToken\")", client, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordReset_ClearsLocalSessionAndReturnsToLogin()
    {
        var page = Read("frontend", "src", "pages", "ForgotPasswordPage.tsx");

        Assert.Contains("await logout()", page, StringComparison.Ordinal);
        Assert.Contains("/login?reason=password-changed", page, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
