<#
Starts the OnlineJudge local development environment:
- Docker services
- ASP.NET Core API
- JudgeWorker
- Vite frontend

Run from the repository root with:
powershell -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1
#>

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Root = Resolve-Path (Join-Path $ScriptDir "..")
$RootPath = $Root.Path

Set-Location $RootPath

Write-Host "Starting Docker services..." -ForegroundColor Cyan
docker compose up -d

Write-Host "Starting OnlineJudge.Api..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RootPath'; dotnet run --project .\OnlineJudge.Api"

Write-Host "Starting OnlineJudge.JudgeWorker..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RootPath'; dotnet run --project .\OnlineJudge.JudgeWorker"

Write-Host "Starting frontend dev server..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$RootPath\frontend'; npm.cmd run dev"

Write-Host ""
Write-Host "Development environment is starting." -ForegroundColor Green
Write-Host "API:      http://localhost:5101"
Write-Host "Frontend: http://localhost:5173"
Write-Host ""
Write-Host "If PowerShell blocks this script, run:" -ForegroundColor Yellow
Write-Host "powershell -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1"
