<#
Collects bounded localhost stress metrics for an isolated Docker-backed run.
The API and Worker process IDs are explicit so unrelated development processes are never sampled.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 3600)]
    [int]$DurationSeconds,

    [ValidateRange(1, 60)]
    [int]$IntervalSeconds = 1,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Output,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^oj-stress-[a-z0-9-]+$')]
    [string]$PostgresContainer,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^oj-stress-[a-z0-9-]+$')]
    [string]$RedisContainer,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 2147483647)]
    [int]$ApiProcessId,

    [Parameter(Mandatory = $true)]
    [ValidateCount(1, 2)]
    [int[]]$WorkerProcessIds,

    [ValidateRange(1, 2)]
    [int]$WorkerConcurrency = 1,

    [string]$HealthUrl = 'http://127.0.0.1:15101/api/site-settings/appearance',

    [ValidateRange(15, 100)]
    [int]$MinimumMemoryAvailablePercent = 15,

    [ValidateRange(5, 100)]
    [int]$MinimumDiskAvailablePercent = 10
)

$ErrorActionPreference = 'Stop'

function Convert-DockerMemoryToBytes([string]$Value) {
    $current = ($Value -split '/')[0].Trim()
    if ($current -notmatch '^([0-9]+(?:\.[0-9]+)?)\s*([KMGT]?i?B)$') {
        return 0L
    }

    $number = [double]$Matches[1]
    $multiplier = switch ($Matches[2]) {
        'B' { 1L }
        'KB' { 1000L }
        'KiB' { 1024L }
        'MB' { 1000000L }
        'MiB' { 1048576L }
        'GB' { 1000000000L }
        'GiB' { 1073741824L }
        'TB' { 1000000000000L }
        'TiB' { 1099511627776L }
        default { 0L }
    }
    return [long]($number * $multiplier)
}

function Convert-DockerCpuToPercent([string]$Value) {
    $number = 0.0
    $text = $Value.Trim().TrimEnd('%')
    if (![double]::TryParse(
        $text,
        [System.Globalization.NumberStyles]::Float,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [ref]$number)) {
        return 0.0
    }

    return $number
}

function Get-DockerStatsSnapshot {
    $values = & docker stats --no-stream --format '{{.Name}}|{{.MemUsage}}|{{.CPUPerc}}'
    if ($LASTEXITCODE -ne 0) {
        throw 'docker stats failed'
    }

    $result = @{}
    foreach ($value in $values) {
        $parts = $value -split '\|', 3
        if ($parts.Count -eq 3) {
            $result[$parts[0]] = @{
                MemoryBytes = Convert-DockerMemoryToBytes $parts[1]
                CpuPercent = Convert-DockerCpuToPercent $parts[2]
            }
        }
    }
    return $result
}

function Get-ProcessTotals([int[]]$Ids) {
    $processes = @($Ids | ForEach-Object { Get-Process -Id $_ -ErrorAction Stop })
    return @{
        WorkingSetBytes = [long](($processes | Measure-Object -Property WorkingSet64 -Sum).Sum)
        PeakWorkingSetBytes = [long](($processes | Measure-Object -Property PeakWorkingSet64 -Sum).Sum)
        TotalProcessorTimeMs = [long](($processes | ForEach-Object { $_.TotalProcessorTime.TotalMilliseconds } | Measure-Object -Sum).Sum)
    }
}

function Get-ContainerEnvironmentValue([string]$ContainerName, [string]$VariableName) {
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        $value = & docker exec $ContainerName printenv $VariableName
        $dockerExitCode = $LASTEXITCODE
        $firstValue = $value | Select-Object -First 1
        if ($dockerExitCode -eq 0 -and ![string]::IsNullOrWhiteSpace($firstValue)) {
            return $firstValue.Trim()
        }
        Start-Sleep -Milliseconds 200
    }

    throw "isolated container environment variable $VariableName could not be discovered"
}

$postgresUser = Get-ContainerEnvironmentValue $PostgresContainer 'POSTGRES_USER'
$postgresDatabase = Get-ContainerEnvironmentValue $PostgresContainer 'POSTGRES_DB'

$outputPath = [System.IO.Path]::GetFullPath($Output)
$outputDirectory = Split-Path -Parent $outputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$drive = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$((Get-Location).Drive.Name):'"
$os = Get-CimInstance Win32_OperatingSystem
$totalMemoryBytes = [long]$os.TotalVisibleMemorySize * 1024
$started = [DateTimeOffset]::UtcNow
$rows = [System.Collections.Generic.List[object]]::new()

do {
    $sampleStarted = [DateTimeOffset]::UtcNow
    $api = Get-ProcessTotals @($ApiProcessId)
    $workers = Get-ProcessTotals $WorkerProcessIds
    $os = Get-CimInstance Win32_OperatingSystem
    $cpuPercent = [double](Get-CimInstance Win32_Processor | Measure-Object -Property LoadPercentage -Average).Average
    $disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$($drive.DeviceID)'"
    $availableMemoryBytes = [long]$os.FreePhysicalMemory * 1024
    $diskAvailableBytes = [long]$disk.FreeSpace

    $queueSql = 'SELECT COUNT(*) FILTER (WHERE "Status" = 1), COUNT(*) FILTER (WHERE "Status" = 2), COUNT(*) FILTER (WHERE "Status" = 3), COUNT(*) FILTER (WHERE "Status" = 4), COALESCE(EXTRACT(EPOCH FROM (CURRENT_TIMESTAMP - MIN("CreatedAt") FILTER (WHERE "Status" = 1)))::bigint, 0) FROM "JudgeJobs";'
    $queueText = & docker exec $PostgresContainer psql -U $postgresUser -d $postgresDatabase -AtF '|' -c $queueSql
    if ($LASTEXITCODE -ne 0) {
        throw 'isolated PostgreSQL queue query failed'
    }
    $queue = (($queueText | Select-Object -First 1) -split '\|')
    if ($queue.Count -ne 5) {
        throw 'isolated PostgreSQL queue query returned an unexpected shape'
    }

    try {
        $healthStatus = (Invoke-WebRequest -Uri $HealthUrl -Method Get -UseBasicParsing -TimeoutSec 3).StatusCode
    }
    catch {
        $healthStatus = 0
    }
    $dockerStats = Get-DockerStatsSnapshot
    $postgresMemoryBytes = if ($dockerStats.ContainsKey($PostgresContainer)) { [long]$dockerStats[$PostgresContainer].MemoryBytes } else { 0L }
    $redisMemoryBytes = if ($dockerStats.ContainsKey($RedisContainer)) { [long]$dockerStats[$RedisContainer].MemoryBytes } else { 0L }
    $judgeStats = @($dockerStats.GetEnumerator() | Where-Object { $_.Key -match '^oj-[0-9a-f]{32}$' } | ForEach-Object { $_.Value })
    $judgeContainerCount = $judgeStats.Count
    $judgeContainerMemoryBytes = if ($judgeContainerCount -gt 0) {
        [long](($judgeStats | ForEach-Object { $_.MemoryBytes } | Measure-Object -Sum).Sum)
    }
    else { 0L }
    $judgeContainerCpuPercent = if ($judgeContainerCount -gt 0) {
        [double](($judgeStats | ForEach-Object { $_.CpuPercent } | Measure-Object -Sum).Sum)
    }
    else { 0.0 }

    $rows.Add([pscustomobject]@{
        TimestampUtc = $sampleStarted.ToString('O')
        CpuPercent = $cpuPercent
        MemoryAvailableBytes = $availableMemoryBytes
        DiskAvailableBytes = $diskAvailableBytes
        ApiWorkingSetBytes = $api.WorkingSetBytes
        ApiPeakWorkingSetBytes = $api.PeakWorkingSetBytes
        ApiTotalProcessorTimeMs = $api.TotalProcessorTimeMs
        WorkerProcessCount = $WorkerProcessIds.Count
        WorkerConcurrency = $WorkerConcurrency
        WorkerWorkingSetBytes = $workers.WorkingSetBytes
        WorkerPeakWorkingSetBytes = $workers.PeakWorkingSetBytes
        WorkerTotalProcessorTimeMs = $workers.TotalProcessorTimeMs
        PostgresMemoryBytes = $postgresMemoryBytes
        RedisMemoryBytes = $redisMemoryBytes
        JudgeContainerCount = $judgeContainerCount
        JudgeContainerMemoryBytes = $judgeContainerMemoryBytes
        JudgeContainerCpuPercent = $judgeContainerCpuPercent
        PendingJobs = [long]$queue[0]
        LeasedJobs = [long]$queue[1]
        CompletedJobs = [long]$queue[2]
        DeadLetteredJobs = [long]$queue[3]
        OldestPendingAgeSeconds = [long]$queue[4]
        HealthHttpStatus = $healthStatus
    })
    $rows | Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding utf8

    if ($healthStatus -ne 200) {
        throw "STOP_STRESS: API health returned HTTP $healthStatus"
    }
    if (($availableMemoryBytes * 100 / $totalMemoryBytes) -lt $MinimumMemoryAvailablePercent) {
        throw "STOP_STRESS: host memory availability fell below $MinimumMemoryAvailablePercent percent"
    }
    if (($diskAvailableBytes * 100 / [long]$disk.Size) -lt $MinimumDiskAvailablePercent) {
        throw "STOP_STRESS: workspace disk availability fell below $MinimumDiskAvailablePercent percent"
    }

    $elapsed = ([DateTimeOffset]::UtcNow - $started).TotalSeconds
    if ($elapsed -lt $DurationSeconds) {
        Start-Sleep -Seconds ([Math]::Min($IntervalSeconds, $DurationSeconds - $elapsed))
    }
} while (([DateTimeOffset]::UtcNow - $started).TotalSeconds -lt $DurationSeconds)

Write-Output $outputPath
