param(
    [string]$BackupPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$target = Join-Path $root "OnlineJudge.Infrastructure\Challenges\ChallengeService.cs"
if (-not (Test-Path $target)) { throw "ChallengeService.cs not found: $target" }

if ([string]::IsNullOrWhiteSpace($BackupPath)) {
    $backupRoot = Join-Path $root "artifacts\patch-backup"
    if (Test-Path $backupRoot) {
        $candidate = Get-ChildItem $backupRoot -Directory -Filter "challenge-score-consistency-02-*" |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "OnlineJudge.Infrastructure\Challenges\ChallengeService.cs" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        if ($candidate) { $BackupPath = $candidate }
    }
}

if ([string]::IsNullOrWhiteSpace($BackupPath) -or -not (Test-Path $BackupPath)) {
    throw "Original ChallengeService backup was not found. Pass -BackupPath explicitly. Expected under artifacts\patch-backup\challenge-score-consistency-02-*"
}

$repairBackupRoot = Join-Path $root ("artifacts\patch-backup\challenge-score-consistency-02-repair-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
$brokenBackup = Join-Path $repairBackupRoot "broken\OnlineJudge.Infrastructure\Challenges\ChallengeService.cs"
$restoredBackup = Join-Path $repairBackupRoot "restored-base\OnlineJudge.Infrastructure\Challenges\ChallengeService.cs"
New-Item -ItemType Directory -Path (Split-Path -Parent $brokenBackup) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $restoredBackup) -Force | Out-Null
Copy-Item $target $brokenBackup -Force
Copy-Item $BackupPath $target -Force
Copy-Item $target $restoredBackup -Force

$bytes = [System.IO.File]::ReadAllBytes($target)
$hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
$utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
$offset = if ($hasBom) { 3 } else { 0 }
$text = $utf8Strict.GetString($bytes, $offset, $bytes.Length - $offset)
$useCrLf = $text.Contains("`r`n")
$text = $text.Replace("`r`n", "`n")

$oldLeaderboard = @'
        var groupedCompletions = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.ChallengeId == challengeId)
            .GroupBy(completion => completion.UserId)
'@
$newLeaderboard = @'
        var groupedCompletions = await dbContext.ChallengeTaskCompletions
            .AsNoTracking()
            .Where(completion => completion.ChallengeId == challengeId && (completion.Score > 0 || completion.IsCompleted))
            .GroupBy(completion => completion.UserId)
'@

$oldRank = @'
        var rankedCompletionUsers = rankedUsers.Where(entry => entry.CompletedTaskCount > 0).ToList();
        var rankMap = rankedCompletionUsers
'@
$newRank = @'
        var rankedScoredUsers = rankedUsers.Where(entry => entry.TotalScore > 0 || entry.CompletedTaskCount > 0).ToList();
        var rankMap = rankedScoredUsers
'@

if (-not $text.Contains($oldLeaderboard)) {
    throw "Restored ChallengeService leaderboard block does not match the expected base. The restored file was left untouched at: $target"
}
if (-not $text.Contains($oldRank)) {
    throw "Restored ChallengeService progress-rank block does not match the expected base. The restored file was left untouched at: $target"
}

$text = $text.Replace($oldLeaderboard, $newLeaderboard)
$text = $text.Replace($oldRank, $newRank)

if (-not $text.Contains($newLeaderboard) -or -not $text.Contains($newRank)) {
    Copy-Item $restoredBackup $target -Force
    throw "Patch verification failed. Original source was restored."
}

if ($useCrLf) { $text = $text.Replace("`n", "`r`n") }
$outEncoding = [System.Text.UTF8Encoding]::new($hasBom)
[System.IO.File]::WriteAllText($target, $text, $outEncoding)

Write-Host "[PASS] Corrupted ChallengeService restored from backup."
Write-Host "[PASS] Score consistency patch reapplied with explicit UTF-8 handling."
Write-Host "SOURCE BACKUP : $BackupPath"
Write-Host "BROKEN COPY   : $brokenBackup"
Write-Host "RESTORED BASE : $restoredBackup"
Write-Host "NEXT          : dotnet build OnlineJudge.sln"
