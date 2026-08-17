<#
Builds the Windows double-click launcher for the OnlineJudge local development environment.

Run from the repository root with:
powershell -ExecutionPolicy Bypass -File .\scripts\build-launcher.ps1
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $ScriptDir "..")
$RootPath = $Root.Path
$ProjectPath = Join-Path $RootPath "tools\OJLauncher\OJLauncher.csproj"
$OutputPath = Join-Path $RootPath "artifacts\OJLauncher"

if (-not (Test-Path $ProjectPath)) {
    throw "OJLauncher project was not found: $ProjectPath"
}

Set-Location $RootPath

Write-Host "Publishing OJLauncher..." -ForegroundColor Cyan
Write-Host "Project: $ProjectPath"
Write-Host "Output:  $OutputPath"

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    -p:PublishSingleFile=true `
    -p:SelfContained=$selfContainedValue `
    -o $OutputPath

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host ""
Write-Host "OJLauncher published successfully." -ForegroundColor Green
Write-Host "Executable: $(Join-Path $OutputPath 'OJLauncher.exe')"
Write-Host ""
Write-Host "Recommended usage:" -ForegroundColor Yellow
Write-Host "1. Keep the executable under artifacts\OJLauncher, or create a desktop shortcut pointing to it."
Write-Host "2. If you copy OJLauncher.exe elsewhere, make sure it can still find scripts\start-dev.ps1 by walking up parent folders."
