param(
    [string]$ApiBase = "http://127.0.0.1:5101",
    [int]$TimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $root "artifacts\smoke"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $artifactRoot $runId

$apiOut = Join-Path $runRoot "api.out.log"
$apiErr = Join-Path $runRoot "api.err.log"
$workerOut = Join-Path $runRoot "worker.out.log"
$workerErr = Join-Path $runRoot "worker.err.log"
$reportPath = Join-Path $runRoot "result.json"

$apiProcess = $null
$workerProcess = $null
$problemId = $null
$headers = $null

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Set-Location $root

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host ">>> $message"
}

function Write-Pass([string]$message) {
    Write-Host "[PASS] $message"
}

function Invoke-CommandChecked {
    param(
        [scriptblock]$Action,
        [string]$FailureMessage
    )

    & $Action

    if ($LASTEXITCODE -ne 0) {
        throw $FailureMessage
    }
}

function Test-Api {
    try {
        $response = Invoke-WebRequest `
            -Uri "$ApiBase/openapi/v1.json" `
            -UseBasicParsing `
            -TimeoutSec 3 `
            -ErrorAction Stop

        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Wait-Api([int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)

    while ((Get-Date) -lt $deadline) {
        if (Test-Api) {
            return
        }

        if ($null -ne $apiProcess -and $apiProcess.HasExited) {
            $out = if (Test-Path $apiOut) { Get-Content $apiOut -Raw } else { "" }
            $err = if (Test-Path $apiErr) { Get-Content $apiErr -Raw } else { "" }

            throw "API exited before becoming ready.`n$out`n$err"
        }

        Start-Sleep -Seconds 1
    }

    throw "API did not become ready within $seconds seconds."
}

function Test-Worker {
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Name -eq "dotnet.exe" -or $_.Name -eq "OnlineJudge.JudgeWorker.exe") -and
            $_.CommandLine -match "OnlineJudge\.JudgeWorker"
        }

    return @($processes).Count -gt 0
}

function Stop-StartedProcessTree {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    try {
        if (-not $Process.HasExited) {
            taskkill.exe /PID $Process.Id /T /F *> $null
        }
    }
    catch {}
}

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body = $null,

        [hashtable]$Headers = @{}
    )

    $parameters = @{
        Uri         = "$ApiBase$Path"
        Method      = $Method
        Headers     = $Headers
        TimeoutSec  = 20
        ErrorAction = "Stop"
    }

    if ($null -ne $Body) {
        $parameters.ContentType = "application/json"
        $parameters.Body = $Body | ConvertTo-Json -Depth 20 -Compress
    }

    try {
        return Invoke-RestMethod @parameters
    }
    catch {
        $detail = $_.ErrorDetails.Message

        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = ""
        }

        throw "API $Method $Path failed: $($_.Exception.Message)`n$detail"
    }
}

function Find-NamedValue {
    param(
        [object]$Value,
        [string[]]$Names
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($name in $Names) {
            foreach ($key in $Value.Keys) {
                if ([string]$key -ieq $name) {
                    return $Value[$key]
                }
            }
        }

        foreach ($key in $Value.Keys) {
            $result = Find-NamedValue -Value $Value[$key] -Names $Names

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if ($Value -is [pscustomobject]) {
        foreach ($name in $Names) {
            $property = $Value.PSObject.Properties |
                Where-Object { $_.Name -ieq $name } |
                Select-Object -First 1

            if ($null -ne $property) {
                return $property.Value
            }
        }

        foreach ($property in $Value.PSObject.Properties) {
            $result = Find-NamedValue -Value $property.Value -Names $Names

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if (($Value -is [System.Collections.IEnumerable]) -and -not ($Value -is [string])) {
        foreach ($item in $Value) {
            $result = Find-NamedValue -Value $item -Names $Names

            if ($null -ne $result) {
                return $result
            }
        }
    }

    return $null
}

function Find-Jwt {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string]) {
        if ($Value -match "^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$") {
            return $Value
        }

        return $null
    }

    if ($Value -is [pscustomobject]) {
        foreach ($property in $Value.PSObject.Properties) {
            $result = Find-Jwt -Value $property.Value

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            $result = Find-Jwt -Value $Value[$key]

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if (($Value -is [System.Collections.IEnumerable]) -and -not ($Value -is [string])) {
        foreach ($item in $Value) {
            $result = Find-Jwt -Value $item

            if ($null -ne $result) {
                return $result
            }
        }
    }

    return $null
}

function Find-GuidValue {
    param(
        [object]$Value,
        [string[]]$PreferredNames = @()
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($PreferredNames.Count -gt 0) {
        $preferred = Find-NamedValue -Value $Value -Names $PreferredNames

        if ($null -ne $preferred) {
            $guid = [guid]::Empty

            if ([guid]::TryParse([string]$preferred, [ref]$guid)) {
                return $guid
            }
        }
    }

    if ($Value -is [string]) {
        $guid = [guid]::Empty

        if ([guid]::TryParse($Value, [ref]$guid)) {
            return $guid
        }

        return $null
    }

    if ($Value -is [pscustomobject]) {
        foreach ($property in $Value.PSObject.Properties) {
            $result = Find-GuidValue -Value $property.Value

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            $result = Find-GuidValue -Value $Value[$key]

            if ($null -ne $result) {
                return $result
            }
        }

        return $null
    }

    if (($Value -is [System.Collections.IEnumerable]) -and -not ($Value -is [string])) {
        foreach ($item in $Value) {
            $result = Find-GuidValue -Value $item

            if ($null -ne $result) {
                return $result
            }
        }
    }

    return $null
}

function Get-SubmissionStatusDefinition {
    $domainPath = Join-Path $root "OnlineJudge.Domain"
    $files = Get-ChildItem $domainPath -Filter "*.cs" -Recurse

    foreach ($file in $files) {
        $text = Get-Content $file.FullName -Raw

        $enumMatches = [regex]::Matches(
            $text,
            "enum\s+(?<name>[A-Za-z_]\w*)\s*\{(?<body>.*?)\}",
            [System.Text.RegularExpressions.RegexOptions]::Singleline
        )

        foreach ($enumMatch in $enumMatches) {
            $body = $enumMatch.Groups["body"].Value

            if ($body -notmatch "\bAccepted\b") {
                continue
            }

            $body = [regex]::Replace(
                $body,
                "/\*.*?\*/",
                "",
                [System.Text.RegularExpressions.RegexOptions]::Singleline
            )

            $body = [regex]::Replace($body, "//.*", "")

            $map = @{}
            $current = -1

            foreach ($part in ($body -split ",")) {
                $item = $part.Trim()

                if ($item -match "^(?<enumName>[A-Za-z_]\w*)\s*(?:=\s*(?<enumValue>-?\d+))?") {
                    $name = $Matches["enumName"]

                    if (-not [string]::IsNullOrWhiteSpace($Matches["enumValue"])) {
                        $current = [int]$Matches["enumValue"]
                    }
                    else {
                        $current++
                    }

                    $map[$name] = $current
                }
            }

            if ($map.ContainsKey("Accepted")) {
                return [pscustomobject]@{
                    Name = $enumMatch.Groups["name"].Value
                    Map  = $map
                }
            }
        }
    }

    throw "Unable to locate a submission status enum containing Accepted."
}

function Get-RequiredStatusCode {
    param(
        [hashtable]$Map,
        [string[]]$Aliases
    )

    foreach ($alias in $Aliases) {
        if ($Map.ContainsKey($alias)) {
            return [int]$Map[$alias]
        }
    }

    throw "Unable to find status: $($Aliases -join ' / '). Detected: $($Map.Keys -join ', ')"
}

function Get-StatusName {
    param(
        [hashtable]$Map,
        [int]$Code
    )

    foreach ($entry in $Map.GetEnumerator()) {
        if ([int]$entry.Value -eq $Code) {
            return [string]$entry.Key
        }
    }

    return "Unknown($Code)"
}

function Resolve-StatusCode {
    param(
        [object]$Raw,
        [hashtable]$Map
    )

    if (
        $Raw -is [byte] -or
        $Raw -is [int16] -or
        $Raw -is [int32] -or
        $Raw -is [int64]
    ) {
        return [int]$Raw
    }

    $text = [string]$Raw
    $number = 0

    if ([int]::TryParse($text, [ref]$number)) {
        return $number
    }

    if ($Map.ContainsKey($text)) {
        return [int]$Map[$text]
    }

    throw "Unknown submission status value: $text"
}

function Test-TerminalStatus([string]$Name) {
    return $Name -notmatch "(?i)pending|queued|queueing|judging|running|processing|compiling"
}

function Wait-Submission {
    param(
        [guid]$SubmissionId,
        [hashtable]$Headers,
        [hashtable]$StatusMap
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "Unknown"

    while ((Get-Date) -lt $deadline) {
        $submission = Invoke-Api `
            -Method Get `
            -Path "/api/submissions/$SubmissionId" `
            -Headers $Headers

        $rawStatus = Find-NamedValue `
            -Value $submission `
            -Names @("status")

        if ($null -eq $rawStatus) {
            throw "Submission $SubmissionId response does not contain a status property."
        }

        $code = Resolve-StatusCode `
            -Raw $rawStatus `
            -Map $StatusMap

        $name = Get-StatusName `
            -Map $StatusMap `
            -Code $code

        $lastStatus = $name

        if (Test-TerminalStatus $name) {
            return [pscustomobject]@{
                Code     = $code
                Name     = $name
                Response = $submission
            }
        }

        Start-Sleep -Milliseconds 800
    }

    throw "Submission $SubmissionId timed out. Last status: $lastStatus"
}

function Run-JudgeCase {
    param(
        [string]$Name,
        [int]$Language,
        [string]$SourceCode,
        [int]$ExpectedStatus,
        [hashtable]$Headers,
        [guid]$ProblemId,
        [guid]$UserId,
        [hashtable]$StatusMap
    )

    Write-Step $Name

    $created = Invoke-Api `
        -Method Post `
        -Path "/api/submissions" `
        -Headers $Headers `
        -Body @{
            problemId       = $ProblemId
            userId          = $UserId
            challengeTaskId = $null
            language        = $Language
            sourceCode      = $SourceCode
        }

    $submissionId = Find-GuidValue `
        -Value $created `
        -PreferredNames @("id", "submissionId")

    if ($null -eq $submissionId) {
        throw "$Name did not return a submission id."
    }

    $final = Wait-Submission `
        -SubmissionId $submissionId `
        -Headers $Headers `
        -StatusMap $StatusMap

    $expectedName = Get-StatusName `
        -Map $StatusMap `
        -Code $ExpectedStatus

    $passed = $final.Code -eq $ExpectedStatus

    if ($passed) {
        Write-Pass "$Name -> $($final.Name)"
    }
    else {
        Write-Host "[FAIL] $Name -> $($final.Name), expected $expectedName"
    }

    return [pscustomobject]@{
        Test         = $Name
        SubmissionId = $submissionId
        Expected     = $expectedName
        Actual       = $final.Name
        Passed       = $passed
    }
}


$challengeId = $null
$taskId = $null

function Run-ChallengeJudgeCase {
    param(
        [string]$Name,
        [string]$SourceCode,
        [int]$ExpectedStatus,
        [hashtable]$Headers,
        [guid]$ProblemId,
        [guid]$ChallengeTaskId,
        [guid]$UserId,
        [hashtable]$StatusMap
    )

    Write-Step $Name

    $created = Invoke-Api `
        -Method Post `
        -Path "/api/submissions" `
        -Headers $Headers `
        -Body @{
            problemId       = $ProblemId
            userId          = $UserId
            challengeTaskId = $ChallengeTaskId
            language        = 1
            sourceCode      = $SourceCode
        }

    $submissionId = Find-GuidValue `
        -Value $created `
        -PreferredNames @("id", "submissionId")

    if ($null -eq $submissionId) {
        throw "$Name did not return a submission id."
    }

    $final = Wait-Submission `
        -SubmissionId $submissionId `
        -Headers $Headers `
        -StatusMap $StatusMap

    $expectedName = Get-StatusName `
        -Map $StatusMap `
        -Code $ExpectedStatus

    if ($final.Code -ne $ExpectedStatus) {
        throw "$Name -> $($final.Name), expected $expectedName."
    }

    Write-Pass "$Name -> $($final.Name)"

    return [pscustomobject]@{
        Name         = $Name
        SubmissionId = $submissionId
        Status       = $final.Name
        Response     = $final.Response
    }
}

function Assert-FullCaseCollection {
    param(
        [object]$Submission,
        [int]$ExpectedCount,
        [int]$ExpectedAcceptedCount,
        [hashtable]$StatusMap
    )

    $caseResults = @($Submission.caseResults)

    if ($caseResults.Count -ne $ExpectedCount) {
        throw "Expected $ExpectedCount case results, actual $($caseResults.Count). CollectAllCaseResults is not working as expected."
    }

    $acceptedCount = @(
        $caseResults |
        Where-Object {
            $code = Resolve-StatusCode -Raw $_.status -Map $StatusMap
            (Get-StatusName -Map $StatusMap -Code $code) -eq "Accepted"
        }
    ).Count

    if ($acceptedCount -ne $ExpectedAcceptedCount) {
        throw "Expected $ExpectedAcceptedCount accepted cases, actual $acceptedCount."
    }

    Write-Pass "Collected all $ExpectedCount case results; Accepted=$acceptedCount"
}

function Assert-ChallengeScoreState {
    param(
        [guid]$ChallengeId,
        [guid]$TaskId,
        [guid]$UserId,
        [hashtable]$Headers,
        [int]$ExpectedEarnedScore,
        [bool]$ExpectedCompleted,
        [int]$ExpectedCompletedTaskCount
    )

    $detail = Invoke-Api `
        -Method Get `
        -Path "/api/challenges/$ChallengeId" `
        -Headers $Headers

    $task = @($detail.tasks) |
        Where-Object { [string]$_.id -eq [string]$TaskId } |
        Select-Object -First 1

    if ($null -eq $task) {
        throw "Challenge detail does not contain task $TaskId."
    }

    if ([int]$task.earnedScore -ne $ExpectedEarnedScore) {
        throw "Task EarnedScore expected $ExpectedEarnedScore, actual $($task.earnedScore)."
    }

    if ([bool]$task.isCompleted -ne $ExpectedCompleted) {
        throw "Task IsCompleted expected $ExpectedCompleted, actual $($task.isCompleted)."
    }

    if ([int]$detail.completedTaskCount -ne $ExpectedCompletedTaskCount) {
        throw "Challenge CompletedTaskCount expected $ExpectedCompletedTaskCount, actual $($detail.completedTaskCount)."
    }

    $leaderboard = Invoke-Api `
        -Method Get `
        -Path "/api/challenges/$ChallengeId/leaderboard" `
        -Headers $Headers

    $entry = @($leaderboard.entries) |
        Where-Object { [string]$_.userId -eq [string]$UserId } |
        Select-Object -First 1

    if ($null -eq $entry) {
        throw "Leaderboard does not contain current user."
    }

    if ([int]$entry.totalScore -ne $ExpectedEarnedScore) {
        throw "Leaderboard TotalScore expected $ExpectedEarnedScore, actual $($entry.totalScore)."
    }

    if ([int]$entry.completedTaskCount -ne $ExpectedCompletedTaskCount) {
        throw "Leaderboard CompletedTaskCount expected $ExpectedCompletedTaskCount, actual $($entry.completedTaskCount)."
    }

    Write-Pass "Challenge Score=$ExpectedEarnedScore / 300; Completed=$ExpectedCompleted; CompletedTaskCount=$ExpectedCompletedTaskCount"
}

try {
    Write-Host "========================================"
    Write-Host "OnlineJudge Challenge Score Smoke Test"
    Write-Host "========================================"

    Write-Step "Checking Docker"

    Invoke-CommandChecked `
        -Action { docker version *> $null } `
        -FailureMessage "Docker daemon is unavailable."

    Write-Pass "Docker"

    Write-Step "Starting PostgreSQL And Redis"

    Push-Location $root
    try {
        docker compose up -d postgres redis
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up failed."
        }
    }
    finally {
        Pop-Location
    }

    Start-Sleep -Seconds 3
    Write-Pass "PostgreSQL / Redis"

    Write-Step "Applying Development Database Migrations"

    $oldAspNetEnvironment = $env:ASPNETCORE_ENVIRONMENT
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Development"

        Invoke-CommandChecked `
            -Action { dotnet tool restore } `
            -FailureMessage "dotnet tool restore failed."

        Invoke-CommandChecked `
            -Action {
                dotnet ef database update `
                    --project ".\OnlineJudge.Infrastructure\OnlineJudge.Infrastructure.csproj" `
                    --startup-project ".\OnlineJudge.Api\OnlineJudge.Api.csproj"
            } `
            -FailureMessage "Development database migration failed."
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $oldAspNetEnvironment
    }

    Write-Pass "Development Database Migrations"

    Write-Step "Checking Judge Sandbox Images"

    foreach ($image in @(
        "onlinejudge-cpp17-sandbox:latest",
        "onlinejudge-csharp-sandbox:latest"
    )) {
        docker image inspect $image *> $null

        if ($LASTEXITCODE -ne 0) {
            throw "Required sandbox image is missing: $image"
        }

        Write-Pass $image
    }

    Write-Step "Checking API"

    if (-not (Test-Api)) {
        $oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $oldUrls = $env:ASPNETCORE_URLS

        try {
            $env:ASPNETCORE_ENVIRONMENT = "Development"
            $env:ASPNETCORE_URLS = $ApiBase

            $apiProcess = Start-Process `
                -FilePath "dotnet" `
                -ArgumentList @(
                    "run",
                    "--project",
                    ".\OnlineJudge.Api\OnlineJudge.Api.csproj",
                    "--no-launch-profile"
                ) `
                -WorkingDirectory $root `
                -RedirectStandardOutput $apiOut `
                -RedirectStandardError $apiErr `
                -WindowStyle Hidden `
                -PassThru
        }
        finally {
            $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
            $env:ASPNETCORE_URLS = $oldUrls
        }

        Wait-Api 45
        Write-Pass "API Started By Smoke Test"
    }
    else {
        Write-Pass "Existing API"
    }

    Write-Step "Checking JudgeWorker"

    if (-not (Test-Worker)) {
        $oldEnvironment = $env:DOTNET_ENVIRONMENT

        try {
            $env:DOTNET_ENVIRONMENT = "Development"

            $workerProcess = Start-Process `
                -FilePath "dotnet" `
                -ArgumentList @(
                    "run",
                    "--project",
                    ".\OnlineJudge.JudgeWorker\OnlineJudge.JudgeWorker.csproj",
                    "--no-launch-profile"
                ) `
                -WorkingDirectory $root `
                -RedirectStandardOutput $workerOut `
                -RedirectStandardError $workerErr `
                -WindowStyle Hidden `
                -PassThru
        }
        finally {
            $env:DOTNET_ENVIRONMENT = $oldEnvironment
        }

        Start-Sleep -Seconds 3

        if ($workerProcess.HasExited) {
            $out = if (Test-Path $workerOut) { Get-Content $workerOut -Raw } else { "" }
            $err = if (Test-Path $workerErr) { Get-Content $workerErr -Raw } else { "" }

            throw "JudgeWorker exited during startup.`n$out`n$err"
        }

        Write-Pass "JudgeWorker Started By Smoke Test"
    }
    else {
        Write-Pass "Existing JudgeWorker"
    }

    Write-Step "Loading Development Root Account"

    $developmentSettingsPath = Join-Path `
        $root `
        "OnlineJudge.Api\appsettings.Development.json"

    $developmentSettings = Get-Content `
        $developmentSettingsPath `
        -Raw |
        ConvertFrom-Json

    if ($null -eq $developmentSettings.RootAccount) {
        throw "RootAccount is missing from appsettings.Development.json."
    }

    $account = [string]$developmentSettings.RootAccount.UserName
    $password = [string]$developmentSettings.RootAccount.Password

    if (
        [string]::IsNullOrWhiteSpace($account) -or
        [string]::IsNullOrWhiteSpace($password)
    ) {
        throw "Development root account is incomplete."
    }

    Write-Step "Logging In"

    $login = Invoke-Api `
        -Method Post `
        -Path "/api/auth/login" `
        -Body @{
            account  = $account
            password = $password
        }

    $token = Find-NamedValue `
        -Value $login `
        -Names @("accessToken", "token", "jwtToken", "jwt")

    if ($null -eq $token) {
        $token = Find-Jwt -Value $login
    }

    if ([string]::IsNullOrWhiteSpace([string]$token)) {
        throw "Login succeeded but no JWT token could be located in the response."
    }

    $headers = @{ Authorization = "Bearer $token" }

    $me = Invoke-Api `
        -Method Get `
        -Path "/api/auth/me" `
        -Headers $headers

    $userId = Find-GuidValue `
        -Value $me `
        -PreferredNames @("id", "userId")

    if ($null -eq $userId) {
        throw "Unable to determine current root user id."
    }

    Write-Pass "Root Login"

    Write-Step "Detecting Submission Status Enum"

    $statusDefinition = Get-SubmissionStatusDefinition
    $statusMap = $statusDefinition.Map
    $acceptedCode = Get-RequiredStatusCode -Map $statusMap -Aliases @("Accepted", "AC")
    $wrongAnswerCode = Get-RequiredStatusCode -Map $statusMap -Aliases @("WrongAnswer", "WA")

    Write-Pass "$($statusDefinition.Name): $($statusMap.Keys -join ', ')"

    Write-Step "Creating 100-Point Temporary Problem"

    $problem = Invoke-Api `
        -Method Post `
        -Path "/api/problems" `
        -Headers $headers `
        -Body @{
            title             = "Challenge Score Smoke $runId"
            description       = "Automatic challenge partial-score smoke test."
            inputDescription  = "One integer x."
            outputDescription = "Print x * 10."
            timeLimitMs       = 3000
            memoryLimitMb     = 512
            isPublished       = $true
            judgeMode         = 1
            functionSpecJson  = $null
            starterCodeJson   = $null
        }

    $problemId = Find-GuidValue `
        -Value $problem `
        -PreferredNames @("id", "problemId")

    if ($null -eq $problemId) {
        throw "Temporary problem creation did not return an id."
    }

    foreach ($case in @(
        @{ Input = "1`n"; Output = "10"; Score = 10; Visibility = 1 },
        @{ Input = "2`n"; Output = "20"; Score = 20; Visibility = 2 },
        @{ Input = "3`n"; Output = "30"; Score = 30; Visibility = 2 },
        @{ Input = "4`n"; Output = "40"; Score = 40; Visibility = 2 }
    )) {
        Invoke-Api `
            -Method Post `
            -Path "/api/problems/$problemId/test-cases" `
            -Headers $headers `
            -Body @{
                input          = $case.Input
                expectedOutput = $case.Output
                argumentsJson  = $null
                expectedJson   = $null
                visibility     = $case.Visibility
                score          = $case.Score
            } |
            Out-Null
    }

    Write-Pass "Temporary Problem $problemId; TestCase Score Total=100"

    Write-Step "Creating Temporary Challenge"

    $now = [DateTimeOffset]::UtcNow

    $challenge = Invoke-Api `
        -Method Post `
        -Path "/api/challenges" `
        -Headers $headers `
        -Body @{
            title       = "Challenge Score Smoke $runId"
            description = "Automatic partial-score validation."
            startAt     = $now.AddMinutes(-5).ToString("o")
            endAt       = $now.AddHours(2).ToString("o")
            isPublished = $true
        }

    $challengeId = Find-GuidValue `
        -Value $challenge `
        -PreferredNames @("id", "challengeId")

    if ($null -eq $challengeId) {
        throw "Temporary challenge creation did not return an id."
    }

    $task = Invoke-Api `
        -Method Post `
        -Path "/api/challenges/$challengeId/tasks" `
        -Headers $headers `
        -Body @{
            title              = "Partial Score Task"
            description        = "100-point testcase score maps to 300 challenge points."
            taskType           = 1
            difficulty         = 1
            boardX             = 0
            boardY             = 0
            algorithmProblemId = $problemId
            score              = 300
            isPublished        = $true
        }

    $taskId = Find-GuidValue `
        -Value $task `
        -PreferredNames @("id", "taskId")

    if ($null -eq $taskId) {
        throw "Temporary challenge task creation did not return an id."
    }

    Invoke-Api `
        -Method Post `
        -Path "/api/challenges/$challengeId/join" `
        -Headers $headers |
        Out-Null

    Write-Pass "Temporary Challenge $challengeId; Task=$taskId; TaskScore=300"

    $partial50 = @'
#include <iostream>

int main() {
    int x;
    if (!(std::cin >> x)) return 0;

    if (x == 1) std::cout << 10;
    else if (x == 4) std::cout << 40;
    else std::cout << -1;

    return 0;
}
'@

    $lower30 = @'
#include <iostream>

int main() {
    int x;
    if (!(std::cin >> x)) return 0;

    if (x == 1) std::cout << 10;
    else if (x == 2) std::cout << 20;
    else std::cout << -1;

    return 0;
}
'@

    $accepted = @'
#include <iostream>

int main() {
    int x;
    if (!(std::cin >> x)) return 0;
    std::cout << x * 10;
    return 0;
}
'@

    $report = @()

    $partial = Run-ChallengeJudgeCase `
        -Name "Partial Score 50/100" `
        -SourceCode $partial50 `
        -ExpectedStatus $wrongAnswerCode `
        -Headers $headers `
        -ProblemId $problemId `
        -ChallengeTaskId $taskId `
        -UserId $userId `
        -StatusMap $statusMap

    Assert-FullCaseCollection `
        -Submission $partial.Response `
        -ExpectedCount 4 `
        -ExpectedAcceptedCount 2 `
        -StatusMap $statusMap

    Assert-ChallengeScoreState `
        -ChallengeId $challengeId `
        -TaskId $taskId `
        -UserId $userId `
        -Headers $headers `
        -ExpectedEarnedScore 150 `
        -ExpectedCompleted $false `
        -ExpectedCompletedTaskCount 0

    $report += [pscustomobject]@{
        Stage = "Partial50"
        SubmissionStatus = "WrongAnswer"
        ExpectedScore = 150
        ExpectedCompleted = $false
    }

    $lower = Run-ChallengeJudgeCase `
        -Name "Lower Score 30/100" `
        -SourceCode $lower30 `
        -ExpectedStatus $wrongAnswerCode `
        -Headers $headers `
        -ProblemId $problemId `
        -ChallengeTaskId $taskId `
        -UserId $userId `
        -StatusMap $statusMap

    Assert-FullCaseCollection `
        -Submission $lower.Response `
        -ExpectedCount 4 `
        -ExpectedAcceptedCount 2 `
        -StatusMap $statusMap

    Assert-ChallengeScoreState `
        -ChallengeId $challengeId `
        -TaskId $taskId `
        -UserId $userId `
        -Headers $headers `
        -ExpectedEarnedScore 150 `
        -ExpectedCompleted $false `
        -ExpectedCompletedTaskCount 0

    Write-Pass "BestScore protection: 90-point attempt did not overwrite 150"

    $report += [pscustomobject]@{
        Stage = "Lower30"
        SubmissionStatus = "WrongAnswer"
        ExpectedScore = 150
        ExpectedCompleted = $false
    }

    $full = Run-ChallengeJudgeCase `
        -Name "Accepted 100/100" `
        -SourceCode $accepted `
        -ExpectedStatus $acceptedCode `
        -Headers $headers `
        -ProblemId $problemId `
        -ChallengeTaskId $taskId `
        -UserId $userId `
        -StatusMap $statusMap

    Assert-FullCaseCollection `
        -Submission $full.Response `
        -ExpectedCount 4 `
        -ExpectedAcceptedCount 4 `
        -StatusMap $statusMap

    Assert-ChallengeScoreState `
        -ChallengeId $challengeId `
        -TaskId $taskId `
        -UserId $userId `
        -Headers $headers `
        -ExpectedEarnedScore 300 `
        -ExpectedCompleted $true `
        -ExpectedCompletedTaskCount 1

    $report += [pscustomobject]@{
        Stage = "Accepted100"
        SubmissionStatus = "Accepted"
        ExpectedScore = 300
        ExpectedCompleted = $true
    }

    $report |
        ConvertTo-Json -Depth 10 |
        Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "========================================"
    Write-Host "Challenge Score Smoke Result"
    Write-Host "========================================"
    Write-Host "50/100  -> 150/300, Completed=false : PASS"
    Write-Host "30/100  -> Best remains 150/300     : PASS"
    Write-Host "100/100 -> 300/300, Completed=true  : PASS"
    Write-Host "CollectAllCaseResults               : PASS"
    Write-Host "Leaderboard partial score linkage   : PASS"
    Write-Host ""
    Write-Host "RESULT : PASS"
    Write-Host "REPORT : $reportPath"
    Write-Host "========================================"
}
finally {
    if ($null -ne $challengeId -and $null -ne $headers) {
        try {
            Write-Step "Cleaning Temporary Challenge"

            Invoke-Api `
                -Method Delete `
                -Path "/api/challenges/$challengeId" `
                -Headers $headers |
                Out-Null

            Write-Pass "Temporary Challenge Cleanup"
        }
        catch {
            Write-Warning "Temporary challenge cleanup failed: $($_.Exception.Message)"
        }
    }

    if ($null -ne $problemId -and $null -ne $headers) {
        try {
            Write-Step "Cleaning Temporary Problem"

            Invoke-Api `
                -Method Delete `
                -Path "/api/problems/$problemId" `
                -Headers $headers |
                Out-Null

            Write-Pass "Temporary Problem Cleanup"
        }
        catch {
            Write-Warning "Temporary problem cleanup failed: $($_.Exception.Message)"
        }
    }

    Stop-StartedProcessTree -Process $workerProcess
    Stop-StartedProcessTree -Process $apiProcess
}
