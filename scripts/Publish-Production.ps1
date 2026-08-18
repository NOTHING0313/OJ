$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts"
$outputRoot = Join-Path $artifactRoot "production"
$apiOutput = Join-Path $outputRoot "api"
$workerOutput = Join-Path $outputRoot "worker"
$frontendOutput = Join-Path $outputRoot "frontend"
$sandboxOutput = Join-Path $outputRoot "sandbox"
$archivePath = Join-Path $artifactRoot "onlinejudge-release.tar.gz"
$hashPath = "$archivePath.sha256"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host ">>> $Command $($Arguments -join ' ')"

    & $Command @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE : $Command"
    }
}

Write-Host "========================================"
Write-Host "OnlineJudge Production Publisher"
Write-Host "========================================"

Set-Location $root

$gitStatus = git status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Git status."
}

if ($gitStatus) {
    throw "Working tree is not clean. Commit or revert changes before publishing."
}

$commit = (git rev-parse HEAD).Trim()
$branch = (git branch --show-current).Trim()

Write-Host "Root   : $root"
Write-Host "Branch : $branch"
Write-Host "Commit : $commit"

if (Test-Path $outputRoot) {
    Remove-Item $outputRoot -Recurse -Force
}

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

if (Test-Path $hashPath) {
    Remove-Item $hashPath -Force
}

New-Item -ItemType Directory -Path $apiOutput -Force | Out-Null
New-Item -ItemType Directory -Path $workerOutput -Force | Out-Null
New-Item -ItemType Directory -Path $frontendOutput -Force | Out-Null
New-Item -ItemType Directory -Path $sandboxOutput -Force | Out-Null

Write-Host ""
Write-Host "=== Restore EF Tool ==="

Invoke-Checked dotnet tool restore

Write-Host ""
Write-Host "=== Build And Test Backend ==="

Invoke-Checked dotnet build OnlineJudge.sln -c Release
Invoke-Checked dotnet test OnlineJudge.sln -c Release --no-build

Write-Host ""
Write-Host "=== Publish API For Linux x64 ==="

Invoke-Checked dotnet publish `
    OnlineJudge.Api/OnlineJudge.Api.csproj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -o $apiOutput

Write-Host ""
Write-Host "=== Publish JudgeWorker For Linux x64 ==="

Invoke-Checked dotnet publish `
    OnlineJudge.JudgeWorker/OnlineJudge.JudgeWorker.csproj `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -o $workerOutput

Write-Host ""
Write-Host "=== Remove Development Configuration From Production Artifacts ==="

$apiDevelopmentSettings = Join-Path $apiOutput "appsettings.Development.json"
$workerDevelopmentSettings = Join-Path $workerOutput "appsettings.Development.json"

if (Test-Path $apiDevelopmentSettings) {
    Remove-Item $apiDevelopmentSettings -Force
}

if (Test-Path $workerDevelopmentSettings) {
    Remove-Item $workerDevelopmentSettings -Force
}

Write-Host ""
Write-Host "=== Remove Local Uploaded Files From API Artifact ==="

$publishedUploads = Join-Path $apiOutput "wwwroot\uploads"

if (Test-Path $publishedUploads) {
    Remove-Item $publishedUploads -Recurse -Force
}

$publishedWebRoot = Join-Path $apiOutput "wwwroot"

if (!(Test-Path $publishedWebRoot)) {
    New-Item -ItemType Directory -Path $publishedWebRoot -Force | Out-Null
}

Write-Host ""
Write-Host "=== Build Frontend ==="

Push-Location (Join-Path $root "frontend")

try {
    Invoke-Checked npm ci
    Invoke-Checked npm run build
}
finally {
    Pop-Location
}

Copy-Item `
    (Join-Path $root "frontend\dist\*") `
    $frontendOutput `
    -Recurse `
    -Force

Write-Host ""
Write-Host "=== Build EF Core Migration Bundle ==="

$efBundlePath = Join-Path $outputRoot "efbundle"

Invoke-Checked dotnet ef migrations bundle `
    --project OnlineJudge.Infrastructure/OnlineJudge.Infrastructure.csproj `
    --startup-project OnlineJudge.Api/OnlineJudge.Api.csproj `
    --self-contained `
    -r linux-x64 `
    -o $efBundlePath `
    --force

Write-Host ""
Write-Host "=== Copy Judge Sandbox Definitions ==="

Copy-Item `
    (Join-Path $root "sandbox\*") `
    $sandboxOutput `
    -Recurse `
    -Force

Write-Host ""
Write-Host "=== Write Release Manifest ==="

$manifest = @"
OnlineJudge Production Release

Commit=$commit
Branch=$branch
Runtime=linux-x64
SelfContained=true
CreatedAt=$(Get-Date -Format "yyyy-MM-ddTHH:mm:ssK")
"@

Set-Content `
    -Path (Join-Path $outputRoot "release-manifest.txt") `
    -Value $manifest `
    -Encoding UTF8

Write-Host ""
Write-Host "=== Validate Production Artifact ==="

$forbiddenFiles = @(
    (Join-Path $apiOutput "appsettings.Development.json"),
    (Join-Path $workerOutput "appsettings.Development.json")
)

foreach ($file in $forbiddenFiles) {
    if (Test-Path $file) {
        throw "Development configuration leaked into production artifact: $file"
    }
}

if (!(Test-Path (Join-Path $apiOutput "OnlineJudge.Api"))) {
    throw "Linux API executable was not generated."
}

if (!(Test-Path (Join-Path $workerOutput "OnlineJudge.JudgeWorker"))) {
    throw "Linux JudgeWorker executable was not generated."
}

if (!(Test-Path $efBundlePath)) {
    throw "EF migration bundle was not generated."
}

if (!(Test-Path (Join-Path $frontendOutput "index.html"))) {
    throw "Frontend production build was not generated."
}

Write-Host ""
Write-Host "=== Create Release Archive ==="

$tarCommand = Get-Command tar -ErrorAction SilentlyContinue

if ($null -eq $tarCommand) {
    throw "tar command is unavailable on this Windows installation."
}

Invoke-Checked tar `
    -czf `
    $archivePath `
    -C `
    $outputRoot `
    .

Write-Host ""
Write-Host "=== Generate SHA256 ==="

$hash = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
$archiveName = Split-Path $archivePath -Leaf

Set-Content `
    -Path $hashPath `
    -Value "$hash  $archiveName" `
    -Encoding ASCII

Write-Host ""
Write-Host "========================================"
Write-Host "Production Release Complete"
Write-Host "========================================"
Write-Host "Commit : $commit"
Write-Host "Output : $outputRoot"
Write-Host "Archive: $archivePath"
Write-Host "SHA256 : $hash"
Write-Host "========================================"