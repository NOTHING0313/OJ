param(
    [Parameter(Mandatory = $true)]
    [string]$BackupPath,

    [int]$PostgresPort = 55433,

    [string]$TargetMigration = "20260828090000_AddChallengePartialScoreProgress",

    [switch]$KeepContainer
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $root "artifacts\migration-verify\$runId"
$reportPath = Join-Path $runRoot "result.json"
$containerName = "oj-migration-verify-$runId"
$dbName = "oj_migration_verify"
$dbUser = "oj_verify"
$dbPassword = "oj_verify_$runId"
$containerBackupPath = "/tmp/oj-production-backup"
$previousConnectionString = $env:ConnectionStrings__DefaultConnection

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Set-Location $root

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host ">>> $message"
}

function Write-Pass([string]$message) {
    Write-Host "[PASS] $message"
}

function Invoke-ExternalChecked {
    param(
        [scriptblock]$Action,
        [string]$FailureMessage
    )

    & $Action

    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Invoke-Psql {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $output = $Sql | & docker exec -i $containerName `
        psql -X -qAt -v ON_ERROR_STOP=1 -U $dbUser -d $dbName

    if ($LASTEXITCODE -ne 0) {
        throw "psql command failed."
    }

    return @($output)
}

function Invoke-PsqlScalar {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $lines = @(Invoke-Psql -Sql $Sql)
    if ($lines.Count -eq 0) {
        return ""
    }

    return ([string]$lines[-1]).Trim()
}

function Test-TableExists([string]$TableName) {
    $escaped = $TableName.Replace("'", "''")
    $value = Invoke-PsqlScalar -Sql "SELECT CASE WHEN to_regclass('public.""$escaped""') IS NULL THEN '0' ELSE '1' END;"
    return $value -eq "1"
}

function Test-ColumnExists([string]$TableName, [string]$ColumnName) {
    $table = $TableName.Replace("'", "''")
    $column = $ColumnName.Replace("'", "''")
    $value = Invoke-PsqlScalar -Sql @"
SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = '$table'
  AND column_name = '$column';
"@
    return [int]$value -gt 0
}

function Get-Stats {
    $row = Invoke-PsqlScalar -Sql @'
SELECT
    COUNT(*)::text || '|' ||
    COUNT(*) FILTER (WHERE expected_is_completed)::text || '|' ||
    COUNT(*) FILTER (WHERE NOT expected_is_completed)::text || '|' ||
    COUNT(*) FILTER (WHERE task_type = 1)::text || '|' ||
    COUNT(*) FILTER (WHERE task_type = 2)::text
FROM "__MigrationAudit_ChallengeTaskCompletions";
'@

    $parts = $row -split '\|'
    return [pscustomobject]@{
        CompletionCount = [int64]$parts[0]
        ExpectedCompletedCount = [int64]$parts[1]
        ExpectedIncompleteCount = [int64]$parts[2]
        AlgorithmCompletionCount = [int64]$parts[3]
        FileCompletionCount = [int64]$parts[4]
    }
}

function Assert-Zero([string]$Name, [string]$Sql) {
    $value = [int64](Invoke-PsqlScalar -Sql $Sql)
    if ($value -ne 0) {
        throw "$Name failed. Mismatch count: $value"
    }

    Write-Pass "$Name"
}

$result = [ordered]@{
    Gate = "OJ-PREDEPLOY-MIGRATION-01"
    Passed = $false
    TargetMigration = $TargetMigration
    BackupPath = [System.IO.Path]::GetFullPath($BackupPath)
    BackupSha256 = $null
    Container = $containerName
    PostgresPort = $PostgresPort
    PreMigration = $null
    PostMigration = $null
    Error = $null
    Timestamp = (Get-Date).ToString("o")
}

try {
    Write-Host "========================================"
    Write-Host "OnlineJudge Predeploy Migration Verify"
    Write-Host "========================================"

    Write-Step "Validating Backup File"
    if (-not (Test-Path -LiteralPath $BackupPath -PathType Leaf)) {
        throw "Backup file does not exist: $BackupPath"
    }

    $resolvedBackup = (Resolve-Path -LiteralPath $BackupPath).Path
    $result.BackupPath = $resolvedBackup
    $result.BackupSha256 = (Get-FileHash -LiteralPath $resolvedBackup -Algorithm SHA256).Hash
    Write-Pass "Backup exists"
    Write-Host "BACKUP SHA256 : $($result.BackupSha256)"

    Write-Step "Checking Docker"
    Invoke-ExternalChecked -Action { docker version *> $null } -FailureMessage "Docker is not available."
    Write-Pass "Docker"

    Write-Step "Checking Verification Port"
    $occupied = Get-NetTCPConnection -LocalPort $PostgresPort -State Listen -ErrorAction SilentlyContinue
    if ($occupied) {
        throw "Port $PostgresPort is already in use. Choose another value with -PostgresPort."
    }
    Write-Pass "Port $PostgresPort available"

    Write-Step "Starting Isolated PostgreSQL 16"
    Invoke-ExternalChecked -Action {
        docker run -d `
            --name $containerName `
            -e "POSTGRES_DB=$dbName" `
            -e "POSTGRES_USER=$dbUser" `
            -e "POSTGRES_PASSWORD=$dbPassword" `
            -p "${PostgresPort}:5432" `
            postgres:16 *> $null
    } -FailureMessage "Failed to start isolated PostgreSQL container."

    $deadline = (Get-Date).AddSeconds(45)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        docker exec $containerName pg_isready -U $dbUser -d $dbName *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Seconds 1
    }

    if (-not $ready) {
        throw "Isolated PostgreSQL did not become ready."
    }
    Write-Pass "Isolated PostgreSQL ready"

    Write-Step "Copying Backup Into Isolated Container"
    Invoke-ExternalChecked -Action {
        docker cp $resolvedBackup "${containerName}:$containerBackupPath"
    } -FailureMessage "Failed to copy backup into verification container."
    Write-Pass "Backup copied"

    Write-Step "Restoring Backup"
    docker exec $containerName pg_restore --list $containerBackupPath *> $null
    $isArchive = $LASTEXITCODE -eq 0

    if ($isArchive) {
        Write-Host "Detected pg_dump archive/custom format."
        Invoke-ExternalChecked -Action {
            docker exec $containerName pg_restore `
                --exit-on-error `
                --no-owner `
                --no-privileges `
                -U $dbUser `
                -d $dbName `
                $containerBackupPath
        } -FailureMessage "pg_restore failed."
    }
    else {
        Write-Host "Archive detection failed; treating backup as plain SQL."
        Invoke-ExternalChecked -Action {
            docker exec $containerName psql `
                -X `
                -v ON_ERROR_STOP=1 `
                -U $dbUser `
                -d $dbName `
                -f $containerBackupPath
        } -FailureMessage "Plain SQL restore failed."
    }
    Write-Pass "Backup restored"

    Write-Step "Checking Pre-Migration Schema"
    foreach ($table in @("__EFMigrationsHistory", "ChallengeTaskCompletions", "ChallengeTasks", "ChallengeTaskFileSubmissions")) {
        if (-not (Test-TableExists $table)) {
            throw "Required table is missing from restored backup: $table"
        }
    }

    $targetCount = [int](Invoke-PsqlScalar -Sql @"
SELECT COUNT(*)
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '$TargetMigration';
"@)

    if ($targetCount -ne 0) {
        throw "The backup already contains migration $TargetMigration. Use a backup captured before this migration."
    }

    if (Test-ColumnExists "ChallengeTaskCompletions" "IsCompleted") {
        throw "Pre-migration backup already contains ChallengeTaskCompletions.IsCompleted."
    }

    if (Test-ColumnExists "ChallengeTaskCompletions" "UpdatedAt") {
        throw "Pre-migration backup already contains ChallengeTaskCompletions.UpdatedAt."
    }

    Write-Pass "Backup is from a valid pre-migration schema"

    Write-Step "Creating Immutable Pre-Migration Audit Snapshot"
    Invoke-Psql -Sql @'
DROP TABLE IF EXISTS "__MigrationAudit_ChallengeTaskCompletions";

CREATE TABLE "__MigrationAudit_ChallengeTaskCompletions" AS
SELECT
    completion."Id" AS id,
    completion."ChallengeId" AS challenge_id,
    completion."ChallengeTaskId" AS challenge_task_id,
    completion."UserId" AS user_id,
    completion."SubmissionId" AS submission_id,
    completion."CompletedAt" AS completed_at,
    completion."Score" AS score,
    task."TaskType" AS task_type,
    CASE
        WHEN task."TaskType" = 1 THEN TRUE
        WHEN EXISTS (
            SELECT 1
            FROM "ChallengeTaskFileSubmissions" AS file_submission
            WHERE file_submission."ChallengeTaskId" = completion."ChallengeTaskId"
              AND file_submission."UserId" = completion."UserId"
              AND file_submission."ReviewedAt" IS NOT NULL
        ) THEN TRUE
        ELSE FALSE
    END AS expected_is_completed
FROM "ChallengeTaskCompletions" AS completion
JOIN "ChallengeTasks" AS task
  ON task."Id" = completion."ChallengeTaskId";
'@ | Out-Null

    $preStats = Get-Stats
    $result.PreMigration = $preStats
    Write-Pass "Audit snapshot created"
    Write-Host "Completion rows     : $($preStats.CompletionCount)"
    Write-Host "Expected completed : $($preStats.ExpectedCompletedCount)"
    Write-Host "Expected incomplete: $($preStats.ExpectedIncompleteCount)"
    Write-Host "Algorithm rows      : $($preStats.AlgorithmCompletionCount)"
    Write-Host "File rows           : $($preStats.FileCompletionCount)"

    Write-Step "Applying Target EF Migration"
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed."
    }

    $env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=$PostgresPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"

    dotnet ef database update $TargetMigration `
        --project .\OnlineJudge.Infrastructure `
        --startup-project .\OnlineJudge.Api

    if ($LASTEXITCODE -ne 0) {
        throw "EF migration failed."
    }
    Write-Pass "Target migration applied"

    Write-Step "Validating Migration History And Columns"
    $targetCountAfter = [int](Invoke-PsqlScalar -Sql @"
SELECT COUNT(*)
FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '$TargetMigration';
"@)

    if ($targetCountAfter -ne 1) {
        throw "Target migration is not recorded exactly once in __EFMigrationsHistory."
    }

    if (-not (Test-ColumnExists "ChallengeTaskCompletions" "IsCompleted")) {
        throw "ChallengeTaskCompletions.IsCompleted was not created."
    }

    if (-not (Test-ColumnExists "ChallengeTaskCompletions" "UpdatedAt")) {
        throw "ChallengeTaskCompletions.UpdatedAt was not created."
    }

    Write-Pass "Migration history and new columns"

    Write-Step "Comparing Historical Rows"
    Assert-Zero -Name "No historical completion rows removed" -Sql @'
SELECT COUNT(*)
FROM "__MigrationAudit_ChallengeTaskCompletions" AS audit
LEFT JOIN "ChallengeTaskCompletions" AS completion ON completion."Id" = audit.id
WHERE completion."Id" IS NULL;
'@

    Assert-Zero -Name "No unexpected completion rows added" -Sql @'
SELECT COUNT(*)
FROM "ChallengeTaskCompletions" AS completion
LEFT JOIN "__MigrationAudit_ChallengeTaskCompletions" AS audit ON audit.id = completion."Id"
WHERE audit.id IS NULL;
'@

    Assert-Zero -Name "Immutable historical completion fields preserved" -Sql @'
SELECT COUNT(*)
FROM "ChallengeTaskCompletions" AS completion
JOIN "__MigrationAudit_ChallengeTaskCompletions" AS audit ON audit.id = completion."Id"
WHERE completion."ChallengeId" IS DISTINCT FROM audit.challenge_id
   OR completion."ChallengeTaskId" IS DISTINCT FROM audit.challenge_task_id
   OR completion."UserId" IS DISTINCT FROM audit.user_id
   OR completion."SubmissionId" IS DISTINCT FROM audit.submission_id
   OR completion."CompletedAt" IS DISTINCT FROM audit.completed_at
   OR completion."Score" IS DISTINCT FROM audit.score;
'@

    Assert-Zero -Name "UpdatedAt backfilled from CompletedAt" -Sql @'
SELECT COUNT(*)
FROM "ChallengeTaskCompletions"
WHERE "UpdatedAt" IS DISTINCT FROM "CompletedAt";
'@

    Assert-Zero -Name "IsCompleted matches migration backfill rule" -Sql @'
SELECT COUNT(*)
FROM "ChallengeTaskCompletions" AS completion
JOIN "__MigrationAudit_ChallengeTaskCompletions" AS audit ON audit.id = completion."Id"
WHERE completion."IsCompleted" IS DISTINCT FROM audit.expected_is_completed;
'@

    $postCount = [int64](Invoke-PsqlScalar -Sql 'SELECT COUNT(*) FROM "ChallengeTaskCompletions";')
    $postCompleted = [int64](Invoke-PsqlScalar -Sql 'SELECT COUNT(*) FROM "ChallengeTaskCompletions" WHERE "IsCompleted" = TRUE;')
    $postIncomplete = [int64](Invoke-PsqlScalar -Sql 'SELECT COUNT(*) FROM "ChallengeTaskCompletions" WHERE "IsCompleted" = FALSE;')

    $result.PostMigration = [pscustomobject]@{
        CompletionCount = $postCount
        CompletedCount = $postCompleted
        IncompleteCount = $postIncomplete
    }

    if ($postCount -ne $preStats.CompletionCount) {
        throw "Completion row count changed: before=$($preStats.CompletionCount), after=$postCount"
    }

    if ($postCompleted -ne $preStats.ExpectedCompletedCount) {
        throw "Completed row count mismatch: expected=$($preStats.ExpectedCompletedCount), actual=$postCompleted"
    }

    if ($postIncomplete -ne $preStats.ExpectedIncompleteCount) {
        throw "Incomplete row count mismatch: expected=$($preStats.ExpectedIncompleteCount), actual=$postIncomplete"
    }

    Write-Pass "Aggregate counts match"

    $result.Passed = $true
    $result.Timestamp = (Get-Date).ToString("o")
    $result | ConvertTo-Json -Depth 20 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "========================================"
    Write-Host "Predeploy Migration Verify Result"
    Write-Host "========================================"
    Write-Host "Backup restore                     : PASS"
    Write-Host "Pre-migration schema               : PASS"
    Write-Host "Historical row count preserved     : PASS"
    Write-Host "Score / IDs / CompletedAt preserved: PASS"
    Write-Host "UpdatedAt backfill                 : PASS"
    Write-Host "IsCompleted backfill               : PASS"
    Write-Host ""
    Write-Host "RESULT : PASS"
    Write-Host "REPORT : $reportPath"
    Write-Host "========================================"
}
catch {
    $result.Error = $_.Exception.Message
    $result.Timestamp = (Get-Date).ToString("o")
    $result | ConvertTo-Json -Depth 20 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "[FAIL] $($_.Exception.Message)"
    Write-Host "REPORT : $reportPath"
    exit 1
}
finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnectionString

    if (-not $KeepContainer) {
        Write-Step "Cleaning Isolated PostgreSQL"
        docker rm -f $containerName *> $null
        if ($LASTEXITCODE -eq 0) {
            Write-Pass "Verification container removed"
        }
    }
    else {
        Write-Host "KEEP CONTAINER : $containerName"
        Write-Host "CONNECTION     : Host=127.0.0.1;Port=$PostgresPort;Database=$dbName;Username=$dbUser;Password=$dbPassword"
    }
}
