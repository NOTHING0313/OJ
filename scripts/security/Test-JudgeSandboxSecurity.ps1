param(
    [string]$ApiBase = "http://127.0.0.1:5101",
    [int]$LeakRuns = 50,
    [int]$PostgreSqlPort = 5433,
    [int]$RedisPort = 6379
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$apiUri = [Uri]$ApiBase
if ($apiUri.Host -notin @("127.0.0.1", "localhost", "::1")) {
    throw "This security smoke is LOCAL ONLY."
}

$cppImage = "onlinejudge-cpp17-sandbox:latest"
$csharpImage = "onlinejudge-csharp-sandbox:latest"
$runId = [Guid]::NewGuid().ToString("N")
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "onlinejudge-sandbox-security-$runId"
$managedLabel = "onlinejudge.managed=true"
$kindLabel = "onlinejudge.kind=judge"

function Invoke-DockerChecked([string[]]$Arguments, [string]$Failure) {
    & docker @Arguments | Out-Null
    if ($LASTEXITCODE -ne 0) { throw $Failure }
}

function Remove-TestContainer([string]$Name) {
    & docker rm -f $Name 2>$null | Out-Null
}

function Invoke-IsolatedContainer {
    param(
        [string]$Image,
        [string]$Workspace,
        [string]$Command,
        [int]$MemoryMb = 64,
        [int]$TimeoutSeconds = 8,
        [switch]$ExpectTimeout,
        [switch]$ReadOnlyWorkspace
    )

    $name = "oj-security-$([Guid]::NewGuid().ToString('N'))"
    $workspaceMount = if ($ReadOnlyWorkspace) { "${Workspace}:/workspace:ro" } else { "${Workspace}:/workspace" }
    $arguments = @(
        "create", "--name", $name,
        "--network", "none",
        "--ipc", "none",
        "--memory", "${MemoryMb}m",
        "--memory-swap", "${MemoryMb}m",
        "--cpus", "1",
        "--pids-limit", "64",
        "--security-opt", "no-new-privileges",
        "--cap-drop", "ALL",
        "--user", "judge",
        "--ulimit", "fsize=67108864:67108864",
        "--read-only",
        "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,size=64m",
        "--label", $managedLabel,
        "--label", $kindLabel,
        "--env", "HOME=/tmp",
        "--env", "DOTNET_CLI_HOME=/tmp/dotnet",
        "--env", "NUGET_PACKAGES=/tmp/nuget",
        "-v", $workspaceMount,
        "-w", "/workspace",
        $Image, "bash", "-lc", $Command
    )

    try {
        Invoke-DockerChecked $arguments "Container creation failed."
        Invoke-DockerChecked @("start", $name) "Container start failed."
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        do {
            $running = (& docker inspect --format "{{.State.Running}}" $name 2>$null).Trim()
            if ($running -eq "false") { break }
            Start-Sleep -Milliseconds 100
        } while ((Get-Date) -lt $deadline)

        if ($running -eq "true") {
            if (-not $ExpectTimeout) { throw "Container exceeded its smoke timeout." }
            return @{ TimedOut = $true; ExitCode = $null; OomKilled = $false }
        }

        $exitCode = [int](& docker inspect --format "{{.State.ExitCode}}" $name)
        $oomKilled = [bool]::Parse((& docker inspect --format "{{.State.OOMKilled}}" $name).Trim())
        if ($ExpectTimeout) { throw "Expected the hostile process to require timeout cleanup." }
        return @{ TimedOut = $false; ExitCode = $exitCode; OomKilled = $oomKilled }
    }
    finally {
        Remove-TestContainer $name
    }
}

function Assert-NoManagedContainers {
    $remaining = @(& docker ps -aq --filter "label=$managedLabel" --filter "label=$kindLabel")
    if ($remaining.Count -ne 0 -and -not [string]::IsNullOrWhiteSpace(($remaining -join ""))) {
        throw "Managed judge containers leaked after smoke execution."
    }
}

function Test-LocalTcpPort([int]$Port) {
    $client = [Net.Sockets.TcpClient]::new()
    try {
        $connect = $client.ConnectAsync("127.0.0.1", $Port)
        return $connect.Wait(2000) -and $client.Connected
    }
    finally {
        $client.Dispose()
    }
}

New-Item -ItemType Directory -Path $tempRoot | Out-Null
try {
    Invoke-DockerChecked @("info") "Docker is unavailable."
    foreach ($image in @($cppImage, $csharpImage)) {
        Invoke-DockerChecked @("image", "inspect", $image) "Required sandbox image is missing: $image"
    }

    $normal = Join-Path $tempRoot "normal"
    New-Item -ItemType Directory -Path $normal | Out-Null
    Set-Content -LiteralPath (Join-Path $normal "main.cpp") -Value "#include <iostream>`nint main(){std::cout << `"OK`";}" -NoNewline
    $cpp = Invoke-IsolatedContainer $cppImage $normal "g++ main.cpp -std=c++17 -O2 -o main && ./main"
    if ($cpp.ExitCode -ne 0) { throw "Normal C++ sandbox regression failed." }

    Set-Content -LiteralPath (Join-Path $normal "main.c") -Value "#include <stdio.h>`nint main(void){puts(`"OK`");return 0;}" -NoNewline
    $c = Invoke-IsolatedContainer $cppImage $normal "gcc main.c -std=c11 -O2 -o main-c && ./main-c"
    if ($c.ExitCode -ne 0) { throw "Normal C sandbox regression failed." }

    Set-Content -LiteralPath (Join-Path $normal "Main.csproj") -Value '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>' -NoNewline
    Set-Content -LiteralPath (Join-Path $normal "Program.cs") -Value 'System.Console.Write("OK");' -NoNewline
    $csharp = Invoke-IsolatedContainer $csharpImage $normal "dotnet build Main.csproj -c Release -o out --nologo --verbosity quiet && dotnet out/Main.dll" 1024 30
    if ($csharp.ExitCode -ne 0) { throw "Normal C# sandbox regression failed." }

    $network = Invoke-IsolatedContainer $cppImage $normal '! (exec 3<>/dev/tcp/1.1.1.1/80) && ! (exec 3<>/dev/tcp/host.docker.internal/5433) && ! (exec 3<>/dev/tcp/host.docker.internal/6380)'
    if ($network.ExitCode -ne 0) { throw "Network isolation failed." }

    $fork = Invoke-IsolatedContainer $cppImage $normal 'for i in $(seq 1 256); do sleep 3 & done; wait' 64 2 -ExpectTimeout
    if (-not $fork.TimedOut) { throw "Fork bomb containment failed." }

    $loop = Invoke-IsolatedContainer $cppImage $normal 'while true; do :; done' 64 2 -ExpectTimeout
    if (-not $loop.TimedOut) { throw "Infinite-loop containment failed." }

    $memory = Invoke-IsolatedContainer $cppImage $normal 'x=$(head -c 268435456 /dev/zero); test -n "$x"' 64 8
    if (-not $memory.OomKilled -and $memory.ExitCode -eq 0) { throw "Memory bomb was not contained." }

    $output = Invoke-IsolatedContainer $cppImage $normal 'yes OUTPUT | head -c 5242880' 64 8
    if ($output.ExitCode -ne 0) { throw "Output spam container failed unexpectedly." }

    $fileSpam = Invoke-IsolatedContainer $cppImage $normal 'dd if=/dev/zero of=/workspace/file-spam.bin bs=1M count=16 status=none' 64 8 -ReadOnlyWorkspace
    if ($fileSpam.ExitCode -eq 0 -or (Test-Path -LiteralPath (Join-Path $normal "file-spam.bin"))) {
        throw "Read-only runtime workspace did not block file spam."
    }

    $tempFileSpam = Invoke-IsolatedContainer $cppImage $normal 'dd if=/dev/zero of=/tmp/file-spam.bin bs=1M count=80 status=none' 64 8 -ReadOnlyWorkspace
    if ($tempFileSpam.ExitCode -eq 0) { throw "Temporary filesystem/file-size quota did not block file spam." }

    $workspaceA = Join-Path $tempRoot "submission-a"
    $workspaceB = Join-Path $tempRoot "submission-b"
    New-Item -ItemType Directory -Path $workspaceA, $workspaceB | Out-Null
    Set-Content -LiteralPath (Join-Path $workspaceA "secret.txt") -Value "submission-a-only"
    $cross = Invoke-IsolatedContainer $cppImage $workspaceB 'test ! -e /workspace/secret.txt && test ! -e /var/run/docker.sock && test ! -e /host' -ReadOnlyWorkspace
    if ($cross.ExitCode -ne 0) { throw "Cross-submission or host filesystem isolation failed." }

    for ($index = 0; $index -lt $LeakRuns; $index++) {
        $leak = Invoke-IsolatedContainer $cppImage $workspaceB "true" -ReadOnlyWorkspace
        if ($leak.ExitCode -ne 0) { throw "Leak-run container failed at iteration $index." }
    }
    Assert-NoManagedContainers

    Invoke-DockerChecked @("info") "Docker became unhealthy during sandbox smoke."
    try {
        $api = Invoke-WebRequest -UseBasicParsing -Uri "$ApiBase/api/site-settings/appearance" -TimeoutSec 5
        if ($api.StatusCode -ne 200) { throw "API health check failed." }
    }
    catch {
        throw "Local API health check failed after sandbox smoke."
    }

    if (-not (Get-Process -Name "OnlineJudge.JudgeWorker" -ErrorAction SilentlyContinue)) {
        throw "Local JudgeWorker is not running after sandbox smoke."
    }
    if (-not (Test-LocalTcpPort $PostgreSqlPort)) { throw "Local PostgreSQL health check failed after sandbox smoke." }
    if (-not (Test-LocalTcpPort $RedisPort)) { throw "Local Redis health check failed after sandbox smoke." }

    Write-Output "JUDGE_SANDBOX_SECURITY_SMOKE=PASS"
    Write-Output "LEAK_RUNS=$LeakRuns"
    Write-Output "RUNTIME_WORKSPACE_READ_ONLY=PASS"
    Write-Output "TEMP_FILE_SPAM_QUOTA=PASS"
    Write-Output "FILE_SPAM_DISK_QUOTA=PASS"
}
finally {
    $resolved = [IO.Path]::GetFullPath($tempRoot)
    $expectedPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolved.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $resolved).StartsWith("onlinejudge-sandbox-security-")) {
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse }
    }
}
