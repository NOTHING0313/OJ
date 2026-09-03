param(
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipModelCheck,
    [switch]$SkipFrontend,
    [switch]$SkipE2E,
    [switch]$SkipDemoSmoke,
    [string]$Configuration = "Release",
    [string]$ApiBaseUrl = "http://localhost:5101"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $ScriptDir "..\..")
$script:Passed = 0
$script:Failed = 0
$script:StepIndex = 0
$script:TotalSteps = 0

if (-not $SkipBuild) { $script:TotalSteps++ }
if (-not $SkipTests) { $script:TotalSteps++ }
if (-not $SkipModelCheck) { $script:TotalSteps++ }
if (-not $SkipFrontend) { $script:TotalSteps++ }
if (-not $SkipE2E) { $script:TotalSteps++ }
if (-not $SkipDemoSmoke) { $script:TotalSteps++ }

function Invoke-CheckStep {
    param(
        [string]$Name,
        [scriptblock]$Action
    )

    $script:StepIndex++
    Write-Host "[$script:StepIndex/$script:TotalSteps] $Name..." -ForegroundColor Cyan
    try {
        & $Action
        $script:Passed++
        Write-Host "PASS $Name" -ForegroundColor Green
    }
    catch {
        $script:Failed++
        Write-Host "FAILED $Name" -ForegroundColor Red
        Write-Host $_.Exception.Message
    }

    Write-Host ""
}

function Invoke-Process {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = $Root
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$FilePath exited with code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Set-Location $Root

if (-not $SkipBuild) {
    Invoke-CheckStep -Name "Backend build" -Action {
        Invoke-Process -FilePath "dotnet" -Arguments @("build", ".\OnlineJudge.sln", "-c", $Configuration)
    }
}

if (-not $SkipTests) {
    Invoke-CheckStep -Name "Backend tests" -Action {
        Invoke-Process -FilePath "dotnet" -Arguments @("test", ".\OnlineJudge.sln", "-c", $Configuration)
    }
}

if (-not $SkipModelCheck) {
    Invoke-CheckStep -Name "EF Core model drift" -Action {
        Invoke-Process -FilePath "dotnet" -Arguments @("tool", "restore")
        Invoke-Process -FilePath "dotnet" -Arguments @(
            "ef",
            "migrations",
            "has-pending-model-changes",
            "--project",
            ".\OnlineJudge.Infrastructure\OnlineJudge.Infrastructure.csproj",
            "--startup-project",
            ".\OnlineJudge.Api\OnlineJudge.Api.csproj",
            "--configuration",
            $Configuration
        )
    }
}

if (-not $SkipFrontend) {
    Invoke-CheckStep -Name "Frontend quality gates" -Action {
        $npm = if (Get-Command "npm.cmd" -ErrorAction SilentlyContinue) { "npm.cmd" } else { "npm" }
        Invoke-Process -FilePath $npm -Arguments @("ci") -WorkingDirectory (Join-Path $Root "frontend")
        Invoke-Process -FilePath $npm -Arguments @("run", "lint") -WorkingDirectory (Join-Path $Root "frontend")
        Invoke-Process -FilePath $npm -Arguments @("run", "test") -WorkingDirectory (Join-Path $Root "frontend")
        Invoke-Process -FilePath $npm -Arguments @("run", "build") -WorkingDirectory (Join-Path $Root "frontend")
    }
}

if (-not $SkipE2E) {
    Invoke-CheckStep -Name "Function Mode E2E" -Action {
        Invoke-Process -FilePath "powershell" -Arguments @(
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            ".\scripts\e2e\function-mode-e2e.ps1",
            "-ApiBaseUrl",
            $ApiBaseUrl
        )
    }
}

if (-not $SkipDemoSmoke) {
    Invoke-CheckStep -Name "Demo script smoke check" -Action {
        $demoScript = Join-Path $Root "scripts\demo\seed-demo-data.ps1"
        if (-not (Test-Path $demoScript)) {
            throw "Demo seed script not found: $demoScript"
        }

        $errors = $null
        [System.Management.Automation.PSParser]::Tokenize((Get-Content $demoScript -Raw), [ref]$errors) | Out-Null
        if ($errors -and $errors.Count -gt 0) {
            throw "Demo seed script has PowerShell parse errors."
        }
    }
}

Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "Passed: $script:Passed"
Write-Host "Failed: $script:Failed"

if ($script:Failed -gt 0) {
    if (-not $SkipE2E) {
        Write-Host "If Function Mode E2E failed because the API was unavailable, start Docker, API, and JudgeWorker first." -ForegroundColor Yellow
    }

    exit 1
}

exit 0
