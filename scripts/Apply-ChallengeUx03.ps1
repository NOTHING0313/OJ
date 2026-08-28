param(
    [switch]$AllowDifferentBase
)

$ErrorActionPreference = "Stop"
$ExpectedBase = "f8e3105b75852ccb02d11e9a9a29994fca0a00d2"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $RepoRoot

if (!(Test-Path (Join-Path $RepoRoot "OnlineJudge.sln"))) {
    throw "Please extract this ZIP into the OnlineJudge repository root."
}

$Head = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Git HEAD."
}

if ($Head -ne $ExpectedBase -and !$AllowDifferentBase) {
    throw "Unexpected base commit: $Head. Expected: $ExpectedBase."
}

$Target = "frontend/src/styles.css"

git diff --quiet -- $Target
if ($LASTEXITCODE -ne 0) {
    throw "Target file has unstaged modifications: $Target"
}

git diff --cached --quiet -- $Target
if ($LASTEXITCODE -ne 0) {
    throw "Target file has staged modifications: $Target"
}

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupRoot = Join-Path $RepoRoot "artifacts/overlay-backup/CHALLENGE-UX-03-$Stamp"
$Source = Join-Path $RepoRoot $Target
$Destination = Join-Path $BackupRoot $Target
New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
Copy-Item $Source $Destination

function Replace-Exact {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Old,
        [Parameter(Mandatory = $true)][string]$New,
        [int]$ExpectedCount = 1
    )

    $FullPath = Join-Path $RepoRoot $Path
    $Original = [System.IO.File]::ReadAllText($FullPath)
    $UseCrLf = $Original.Contains("`r`n")
    $Normalized = $Original.Replace("`r`n", "`n")
    $OldNormalized = $Old.Replace("`r`n", "`n")
    $NewNormalized = $New.Replace("`r`n", "`n")
    $Count = [regex]::Matches($Normalized, [regex]::Escape($OldNormalized)).Count

    if ($Count -ne $ExpectedCount) {
        throw "Patch context mismatch in $Path. Expected $ExpectedCount occurrence(s), found $Count."
    }

    $Updated = $Normalized.Replace($OldNormalized, $NewNormalized)
    if ($UseCrLf) {
        $Updated = $Updated.Replace("`n", "`r`n")
    }

    [System.IO.File]::WriteAllText($FullPath, $Updated, [System.Text.UTF8Encoding]::new($false))
}

Replace-Exact "frontend/src/styles.css" @'
.selected-task-description-v8 {
  max-height: 124px;
  overflow: auto;
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 7px;
  background: rgba(255, 255, 255, 0.025);
  color: #aeb6ca;
  font-size: 0.82rem;
  line-height: 1.65;
  padding: 10px 11px;
}
'@ @'
.selected-task-description-v8 {
  max-height: 132px;
  overflow: auto;
  scrollbar-gutter: stable;
  border: 1px solid rgba(132, 140, 255, 0.16);
  border-radius: 10px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.04), rgba(255, 255, 255, 0.02));
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.03);
  color: #b8c0d4;
  font-size: 0.82rem;
  line-height: 1.72;
  padding: 10px 12px;
  word-break: break-word;
  scrollbar-width: thin;
  scrollbar-color: rgba(132, 140, 255, 0.58) rgba(255, 255, 255, 0.03);
}

.selected-task-description-v8::-webkit-scrollbar {
  width: 8px;
}

.selected-task-description-v8::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.03);
  border-radius: 999px;
  margin: 6px 0;
}

.selected-task-description-v8::-webkit-scrollbar-thumb {
  background: rgba(132, 140, 255, 0.46);
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.08);
}

.selected-task-description-v8:hover::-webkit-scrollbar-thumb {
  background: rgba(151, 158, 255, 0.72);
}
'@

git diff --check
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check failed."
}

Write-Host ""
Write-Host "========================================"
Write-Host "CHALLENGE-UX-03 APPLIED"
Write-Host "========================================"
Write-Host "Base   : $Head"
Write-Host "Backup : $BackupRoot"
Write-Host ""
Write-Host "Implemented:"
Write-Host "  1. Selected task description scrollbar now matches the dark theme"
Write-Host "  2. Description card border/background/spacing refined"
Write-Host "  3. Frontend-only CSS polish, no backend or database changes"
Write-Host ""
Write-Host "Validate:"
Write-Host "  cd frontend"
Write-Host "  npm run build"
Write-Host "========================================"
