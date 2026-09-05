<#
.SYNOPSIS
Start local infrastructure, migrations, API, Worker and Vite.
.EXAMPLE
.\scripts\start-dev.ps1 -CheckOnly
.EXAMPLE
.\scripts\start-dev.ps1 -RestoreDependencies
#>
[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$RestoreDependencies,
    [switch]$SkipMigrations,
    [switch]$SkipApi,
    [switch]$SkipWorker,
    [switch]$SkipFrontend,
    [ValidateRange(10,300)][int]$TimeoutSeconds = 90
)
$ErrorActionPreference = 'Stop'
$RootPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$LogDirectory = Join-Path $RootPath ('logs/dev-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$Started = @()

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE). Startup stopped." }
}
function Wait-Ready([string]$Name, [scriptblock]$Probe) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if (& $Probe) { Write-Host "$Name ready."; return }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    throw "$Name not ready within $TimeoutSeconds seconds. Logs: $LogDirectory"
}
function Start-ServiceProcess([string]$Name, [string]$Directory, [string]$Command) {
    # Encode the program to preserve literal paths containing spaces/apostrophes.
    $quoted = "'" + $Directory.Replace("'", "''") + "'"
    $program = "Set-Location -LiteralPath $quoted" + [Environment]::NewLine + "& $Command" + [Environment]::NewLine + 'exit $LASTEXITCODE'
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($program))
    $process = Start-Process powershell.exe -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-NonInteractive','-EncodedCommand',$encoded) -RedirectStandardOutput (Join-Path $LogDirectory "$Name.out.log") -RedirectStandardError (Join-Path $LogDirectory "$Name.err.log")
    Write-Host "$Name started (supervisor PID $($process.Id))."
    return $process
}
Push-Location $RootPath
try {
    foreach ($command in @('docker','dotnet','npm.cmd','powershell.exe')) {
        if (-not (Get-Command $command -ErrorAction SilentlyContinue)) { throw "Missing command: $command" }
    }
    $profile = (Get-Content 'OnlineJudge.Api/Properties/launchSettings.json' -Raw | ConvertFrom-Json).profiles.http.applicationUrl
    if ($profile -ne 'http://localhost:5101') { throw 'API http profile changed; review this script.' }
    $occupied = @()
    foreach ($port in @(5101,5173)) {
        if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { $occupied += $port }
    }
    $workers = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -match '^(dotnet|OnlineJudge.JudgeWorker)(\.exe)?$' -and
        ($_.CommandLine -match 'OnlineJudge\.JudgeWorker' -or $_.Name -eq 'OnlineJudge.JudgeWorker.exe')
    })
    Write-Host "Project: $RootPath"
    Write-Host "Occupied ports: $($occupied -join ', '); Worker processes: $($workers.Count)"
    Invoke-Checked 'docker' @('compose','config','--quiet')
    Invoke-Checked 'docker' @('info','--format','{{.ServerVersion}}')
    if ($CheckOnly) {
        Write-Host 'Read-only checks passed. No installs, builds, migrations or services started.'
        return
    }
    if ((-not $SkipApi -and 5101 -in $occupied) -or (-not $SkipFrontend -and 5173 -in $occupied) -or (-not $SkipWorker -and $workers.Count -gt 0)) {
        throw 'Service already running. Stop it yourself or use -SkipApi/-SkipFrontend/-SkipWorker. No process was stopped.'
    }
    if (-not $RestoreDependencies -and -not $SkipFrontend -and -not (Test-Path 'frontend/node_modules/.bin/vite.cmd')) {
        throw 'Frontend dependencies missing. Use -RestoreDependencies when dependency installation is authorized.'
    }
    if ($RestoreDependencies) {
        Invoke-Checked 'dotnet' @('tool','restore')
        Invoke-Checked 'dotnet' @('restore','OnlineJudge.sln')
        if (-not $SkipFrontend) {
            Push-Location (Join-Path $RootPath 'frontend')
            try { Invoke-Checked 'npm.cmd' @('ci') } finally { Pop-Location }
        }
    }
    # Build before background services to avoid shared-output locks.
    if (-not $SkipApi -or -not $SkipMigrations) { Invoke-Checked 'dotnet' @('build','OnlineJudge.Api','--no-restore') }
    if (-not $SkipWorker) { Invoke-Checked 'dotnet' @('build','OnlineJudge.JudgeWorker','--no-restore') }
    Invoke-Checked 'docker' @('compose','up','-d')
    Wait-Ready 'PostgreSQL' { & docker compose exec -T postgres pg_isready -U oj_user -d online_judge *> $null; $LASTEXITCODE -eq 0 }
    Wait-Ready 'Redis' { $reply = & docker compose exec -T redis redis-cli ping 2>$null; $LASTEXITCODE -eq 0 -and $reply -eq 'PONG' }
    if (-not $SkipMigrations) {
        Invoke-Checked 'dotnet' @('ef','database','update','--project','OnlineJudge.Infrastructure','--startup-project','OnlineJudge.Api','--no-build')
    }
    New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
    if (-not $SkipApi) {
        $Started += Start-ServiceProcess 'api' $RootPath 'dotnet run --project OnlineJudge.Api --no-build --no-restore --launch-profile http'
        Wait-Ready 'API port 5101' { [bool](Get-NetTCPConnection -LocalPort 5101 -State Listen -ErrorAction SilentlyContinue) }
    }
    if (-not $SkipWorker) {
        $worker = Start-ServiceProcess 'worker' $RootPath 'dotnet run --project OnlineJudge.JudgeWorker --no-build --no-restore'
        $Started += $worker
        Wait-Ready 'Worker consumer' {
            if ($worker.HasExited) { throw 'Worker exited during startup.' }
            [bool](Select-String -Path (Join-Path $LogDirectory 'worker.out.log') -Pattern 'Judge worker consumer started' -Quiet -ErrorAction SilentlyContinue)
        }
    }
    if (-not $SkipFrontend) {
        $Started += Start-ServiceProcess 'frontend' (Join-Path $RootPath 'frontend') 'npm.cmd run dev -- --host 127.0.0.1 --strictPort'
        Wait-Ready 'Vite' {
            try { (Invoke-WebRequest 'http://127.0.0.1:5173' -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false }
        }
    }
    Write-Host 'Startup complete. API: http://localhost:5101; Frontend: http://localhost:5173'
    Write-Host "Logs: $LogDirectory"
} catch {
    Write-Warning "Startup failed. Started supervisor PIDs: $(($Started | ForEach-Object { $_.Id }) -join ', '). Existing services and database are left intact."
    throw
} finally { Pop-Location }
