namespace OnlineJudge.Tests.Deployment;

public sealed class ProductionDeploymentContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    private static readonly string DeploymentRoot = Path.Combine(Root, "deploy", "production");

    [Fact]
    public void Infrastructure_BindsStatefulServicesToLoopbackAndExternalizesSecrets()
    {
        var compose = Read("compose.infrastructure.yml");
        var environment = Read("env", "infrastructure.env.example");

        Assert.Contains("127.0.0.1:${POSTGRES_BIND_PORT:-5432}:5432", compose, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:${REDIS_BIND_PORT:-6379}:6379", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("oj_password", compose, StringComparison.Ordinal);
        Assert.Contains("POSTGRES_PASSWORD=CHANGE_ME", environment, StringComparison.Ordinal);
        Assert.Contains("healthcheck:", compose, StringComparison.Ordinal);
        Assert.Contains("restart: unless-stopped", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiAndWorker_UsePersistentRootsAndKeepDockerPrivilegeOutOfApi()
    {
        var apiEnvironment = Read("env", "api.env.example");
        var workerEnvironment = Read("env", "worker.env.example");
        var apiUnit = Read("systemd", "onlinejudge-api.service");
        var workerUnit = Read("systemd", "onlinejudge-worker.service");

        Assert.Contains("Storage__UploadImagesRoot=/var/lib/onlinejudge/uploads", apiEnvironment, StringComparison.Ordinal);
        Assert.Contains("Storage__ChallengeFilesRoot=/var/lib/onlinejudge/challenge-files", apiEnvironment, StringComparison.Ordinal);
        Assert.Contains("JudgeAssets__StorageRoot=/var/lib/onlinejudge/judge-assets", apiEnvironment, StringComparison.Ordinal);
        Assert.Contains("TMPDIR=/var/lib/onlinejudge/worker-tmp", workerEnvironment, StringComparison.Ordinal);
        Assert.Contains("SupplementaryGroups=onlinejudge-assets", apiUnit, StringComparison.Ordinal);
        Assert.DoesNotContain("SupplementaryGroups=docker", apiUnit, StringComparison.Ordinal);
        Assert.Contains("SupplementaryGroups=docker onlinejudge-assets", workerUnit, StringComparison.Ordinal);
        Assert.Contains("PrivateTmp=false", workerUnit, StringComparison.Ordinal);
    }

    [Fact]
    public void Nginx_PreservesHttpsAndTrustedProxyContractWithoutRegistrationLimits()
    {
        var nginx = Read("nginx", "onlinejudge.conf");

        Assert.Contains("server 127.0.0.1:5101", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-Proto $scheme", nginx, StringComparison.Ordinal);
        Assert.Contains("try_files $uri $uri/ /index.html", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("limit_req", nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void Publisher_IncludesAndValidatesDeploymentAssets()
    {
        var publisher = File.ReadAllText(Path.Combine(Root, "scripts", "Publish-Production.ps1"));

        Assert.Contains("Production Deployment Assets", publisher, StringComparison.Ordinal);
        Assert.Contains("compose.infrastructure.yml", publisher, StringComparison.Ordinal);
        Assert.Contains("onlinejudge-worker.service", publisher, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([DeploymentRoot, .. segments]));
}
