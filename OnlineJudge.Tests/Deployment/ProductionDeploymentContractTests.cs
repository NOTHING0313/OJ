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
        Assert.Contains("name: ${POSTGRES_VOLUME_NAME:?POSTGRES_VOLUME_NAME is required}", compose, StringComparison.Ordinal);
        Assert.Contains("name: ${REDIS_VOLUME_NAME:?REDIS_VOLUME_NAME is required}", compose, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(compose, "external: true"));
        Assert.Contains("POSTGRES_VOLUME_NAME=onlinejudge_postgres_data", environment, StringComparison.Ordinal);
        Assert.Contains("REDIS_VOLUME_NAME=onlinejudge_redis_data", environment, StringComparison.Ordinal);
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
        Assert.Contains("server_name unrealstudiooj.top", nginx, StringComparison.Ordinal);
        Assert.Contains("server_name unrealstudiooj.top www.unrealstudiooj.top;", nginx, StringComparison.Ordinal);
        Assert.Contains("server_name www.unrealstudiooj.top;", nginx, StringComparison.Ordinal);
        Assert.Contains("listen 443 ssl http2", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("http2 on", nginx, StringComparison.Ordinal);
        Assert.Contains("/etc/letsencrypt/live/unrealstudiooj.top/fullchain.pem", nginx, StringComparison.Ordinal);
        Assert.Contains("return 301 https://unrealstudiooj.top$request_uri;", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("return 301 https://$host$request_uri;", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for", nginx, StringComparison.Ordinal);
        Assert.Contains("proxy_set_header X-Forwarded-Proto $scheme", nginx, StringComparison.Ordinal);
        Assert.Contains("location ^~ /brand/ {\n        try_files $uri =404;", nginx.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("try_files $uri $uri/ /index.html", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("unrealstudioonlinejudge.de5.net", nginx, StringComparison.Ordinal);
        Assert.DoesNotContain("limit_req", nginx, StringComparison.Ordinal);
    }

    [Fact]
    public void Publisher_IncludesAndValidatesDeploymentAssets()
    {
        var publisher = File.ReadAllText(Path.Combine(Root, "scripts", "Publish-Production.ps1"));

        Assert.Contains("Production Deployment Assets", publisher, StringComparison.Ordinal);
        Assert.Contains("brand\\unrealstudio-logo.png", publisher, StringComparison.Ordinal);
        Assert.Contains("compose.infrastructure.yml", publisher, StringComparison.Ordinal);
        Assert.Contains("onlinejudge-worker.service", publisher, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapAndHostVerification_UseProductionDomainAndValidateBrandAsset()
    {
        var bootstrap = Read("nginx", "onlinejudge-bootstrap.conf");
        var verifier = Read("scripts", "verify-host.sh");
        var readme = Read("README.md");

        Assert.Contains("server_name unrealstudiooj.top www.unrealstudiooj.top;", bootstrap, StringComparison.Ordinal);
        Assert.Contains("domain=\"${1:-unrealstudiooj.top}\"", verifier, StringComparison.Ordinal);
        Assert.Contains("alias_domain=\"www.$domain\"", verifier, StringComparison.Ordinal);
        Assert.Contains("openssl s_client", verifier, StringComparison.Ordinal);
        Assert.Contains("-alpn h2", verifier, StringComparison.Ordinal);
        Assert.Contains("ALPN protocol:", verifier, StringComparison.Ordinal);
        Assert.Contains("%{http_code}|%{redirect_url}", verifier, StringComparison.Ordinal);
        Assert.Contains("https://$domain$alias_path", verifier, StringComparison.Ordinal);
        Assert.Contains("frontend/brand/unrealstudio-logo.png", verifier, StringComparison.Ordinal);
        Assert.Contains("--write-out '%{content_type}'", verifier, StringComparison.Ordinal);
        Assert.Contains("image/png", verifier, StringComparison.Ordinal);
        Assert.Contains("read_volume_name POSTGRES_VOLUME_NAME", verifier, StringComparison.Ordinal);
        Assert.Contains("read_volume_name REDIS_VOLUME_NAME", verifier, StringComparison.Ordinal);
        Assert.Contains("verify_volume_mount postgres /var/lib/postgresql/data", verifier, StringComparison.Ordinal);
        Assert.Contains("verify_volume_mount redis /data", verifier, StringComparison.Ordinal);
        Assert.Contains("docker volume inspect", verifier, StringComparison.Ordinal);
        Assert.Contains("--cert-name unrealstudiooj.top", readme, StringComparison.Ordinal);
        Assert.Contains("-d unrealstudiooj.top -d www.unrealstudiooj.top", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("unrealstudioonlinejudge.de5.net", bootstrap + verifier, StringComparison.Ordinal);
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([DeploymentRoot, .. segments]));

    private static int CountOccurrences(string value, string search) =>
        value.Split(search, StringSplitOptions.None).Length - 1;
}
