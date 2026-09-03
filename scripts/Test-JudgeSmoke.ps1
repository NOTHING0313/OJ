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
        $detail = ""

        if ($null -ne $_.ErrorDetails) {
            $detail = [string]$_.ErrorDetails.Message
        }

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

try {
    Write-Host "========================================"
    Write-Host "OnlineJudge Judge Smoke Test"
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
        -Names @(
            "accessToken",
            "token",
            "jwtToken",
            "jwt"
        )

    if ($null -eq $token) {
        $token = Find-Jwt -Value $login
    }

    if ([string]::IsNullOrWhiteSpace([string]$token)) {
        throw "Login succeeded but no JWT token could be located in the response."
    }

    $headers = @{
        Authorization = "Bearer $token"
    }

    $me = Invoke-Api `
        -Method Get `
        -Path "/api/auth/me" `
        -Headers $headers

    $userId = Find-GuidValue `
        -Value $me `
        -PreferredNames @(
            "id",
            "userId"
        )

    if ($null -eq $userId) {
        throw "Unable to determine current root user id."
    }

    Write-Pass "Root Login"

    Write-Step "Detecting Submission Status Enum"

    $statusDefinition = Get-SubmissionStatusDefinition
    $statusMap = $statusDefinition.Map

    $acceptedCode = Get-RequiredStatusCode `
        -Map $statusMap `
        -Aliases @(
            "Accepted",
            "AC"
        )

    $wrongAnswerCode = Get-RequiredStatusCode `
        -Map $statusMap `
        -Aliases @(
            "WrongAnswer",
            "WA"
        )

    $compileErrorCode = Get-RequiredStatusCode `
        -Map $statusMap `
        -Aliases @(
            "CompileError",
            "CompilationError",
            "CE"
        )

    Write-Pass "$($statusDefinition.Name): $($statusMap.Keys -join ', ')"

    Write-Step "Creating Temporary Smoke Problem"

    $problem = Invoke-Api `
        -Method Post `
        -Path "/api/problems" `
        -Headers $headers `
        -Body @{
            title             = "Judge Smoke $runId"
            description       = "Automatic judge smoke test."
            inputDescription  = "Two integers a and b."
            outputDescription = "Print a + b."
            timeLimitMs       = 3000
            memoryLimitMb     = 512
            isPublished       = $false
            judgeMode         = 1
            allowedLanguagesMask = 0
            functionSpecJson  = $null
            starterCodeJson   = $null
        }

    $problemId = Find-GuidValue `
        -Value $problem `
        -PreferredNames @(
            "id",
            "problemId"
        )

    if ($null -eq $problemId) {
        throw "Temporary problem creation did not return an id."
    }

    Invoke-Api `
        -Method Post `
        -Path "/api/problems/$problemId/test-cases" `
        -Headers $headers `
        -Body @{
            input          = "2 3`n"
            expectedOutput = "5"
            argumentsJson  = $null
            expectedJson   = $null
            visibility     = 1
            score          = 50
        } |
        Out-Null

    Invoke-Api `
        -Method Post `
        -Path "/api/problems/$problemId/test-cases" `
        -Headers $headers `
        -Body @{
            input          = "-7 10`n"
            expectedOutput = "3"
            argumentsJson  = $null
            expectedJson   = $null
            visibility     = 2
            score          = 50
        } |
        Out-Null

    Invoke-Api `
        -Method Put `
        -Path "/api/problems/$problemId" `
        -Headers $headers `
        -Body @{
            title                = "Judge Smoke $runId"
            description          = "Automatic judge smoke test."
            inputDescription     = "Two integers a and b."
            outputDescription    = "Print a + b."
            timeLimitMs          = 3000
            memoryLimitMb        = 512
            isPublished          = $true
            judgeMode            = 1
            allowedLanguagesMask = 0
            functionSpecJson     = $null
            starterCodeJson      = $null
        } |
        Out-Null

    Write-Pass "Temporary Problem $problemId"

    $cppAccepted = @'
#include <iostream>

int main() {
    long long a, b;
    if (!(std::cin >> a >> b)) return 0;
    std::cout << a + b;
    return 0;
}
'@

    $cAccepted = @'
#include <stdio.h>

int main(void) {
    long long a, b;
    if (scanf("%lld%lld", &a, &b) != 2) return 0;
    printf("%lld", a + b);
    return 0;
}
'@

    $csharpAccepted = @'
using System;

public class Program
{
    public static void Main()
    {
        var line = Console.ReadLine();

        if (line == null)
        {
            return;
        }

        var parts = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );

        Console.Write(
            long.Parse(parts[0]) +
            long.Parse(parts[1])
        );
    }
}
'@

    $wrongAnswer = @'
#include <iostream>

int main() {
    long long a, b;
    if (!(std::cin >> a >> b)) return 0;
    std::cout << a - b;
    return 0;
}
'@

    $compileError = @'
#include <iostream>

int main( {
    return 0;
}
'@

    $results = @()

    $results += Run-JudgeCase `
        -Name "C++17 Accepted" `
        -Language 1 `
        -SourceCode $cppAccepted `
        -ExpectedStatus $acceptedCode `
        -Headers $headers `
        -ProblemId $problemId `
        -UserId $userId `
        -StatusMap $statusMap

    $results += Run-JudgeCase `
        -Name "C11 Accepted" `
        -Language 2 `
        -SourceCode $cAccepted `
        -ExpectedStatus $acceptedCode `
        -Headers $headers `
        -ProblemId $problemId `
        -UserId $userId `
        -StatusMap $statusMap

    $results += Run-JudgeCase `
        -Name "C# Accepted" `
        -Language 3 `
        -SourceCode $csharpAccepted `
        -ExpectedStatus $acceptedCode `
        -Headers $headers `
        -ProblemId $problemId `
        -UserId $userId `
        -StatusMap $statusMap

    $results += Run-JudgeCase `
        -Name "C++17 Wrong Answer" `
        -Language 1 `
        -SourceCode $wrongAnswer `
        -ExpectedStatus $wrongAnswerCode `
        -Headers $headers `
        -ProblemId $problemId `
        -UserId $userId `
        -StatusMap $statusMap

    $results += Run-JudgeCase `
        -Name "C++17 Compile Error" `
        -Language 1 `
        -SourceCode $compileError `
        -ExpectedStatus $compileErrorCode `
        -Headers $headers `
        -ProblemId $problemId `
        -UserId $userId `
        -StatusMap $statusMap

    $results |
        ConvertTo-Json -Depth 10 |
        Set-Content `
            -Path $reportPath `
            -Encoding UTF8

    Write-Host ""
    Write-Host "========================================"
    Write-Host "Judge Smoke Result"
    Write-Host "========================================"

    $results |
        Select-Object Test, Expected, Actual, Passed |
        Format-Table -AutoSize

    $failed = @(
        $results |
        Where-Object { -not $_.Passed }
    )

    if ($failed.Count -gt 0) {
        throw "$($failed.Count) judge smoke test(s) failed."
    }

    Write-Host "RESULT : PASS"
    Write-Host "REPORT : $reportPath"
    Write-Host "========================================"
}
finally {
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
