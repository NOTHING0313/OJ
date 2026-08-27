Write-Host "This v1.0 patch script is retired because Windows PowerShell 5.1 could corrupt UTF-8 source files." -ForegroundColor Yellow
Write-Host "Run instead:" -ForegroundColor Yellow
Write-Host "powershell -ExecutionPolicy Bypass -File .\scripts\Repair-ChallengeScoreConsistency02.ps1" -ForegroundColor Cyan
exit 2
