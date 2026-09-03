param(
    [string]$ApiBase = "http://127.0.0.1:5101",
    [string]$Account = "UnrealStudio",
    [string]$Password = "Local-only root access phrase 2026!",
    [int]$TimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runRoot = Join-Path $root "artifacts\smoke\$runId"
$reportPath = Join-Path $runRoot "result.json"
$apiOut = Join-Path $runRoot "api.out.log"
$apiErr = Join-Path $runRoot "api.err.log"
$workerOut = Join-Path $runRoot "worker.out.log"
$workerErr = Join-Path $runRoot "worker.err.log"

$apiProcess = $null
$workerProcess = $null
$problemId = $null
$headers = $null
$results = @()

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
Set-Location $root

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host ">>> $message"
}

function Write-Pass([string]$message) {
    Write-Host "[PASS] $message"
}

function Test-Api {
    try {
        $response = Invoke-WebRequest -Uri "$ApiBase/openapi/v1.json" -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Test-Worker {
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Name -eq "dotnet.exe" -or $_.Name -eq "OnlineJudge.JudgeWorker.exe") -and
            $_.CommandLine -match "OnlineJudge\.JudgeWorker"
        }

    return @($processes).Count -gt 0
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
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
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
        $parameters.ContentType = "application/json; charset=utf-8"
        $parameters.Body = $Body | ConvertTo-Json -Depth 50 -Compress
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
            $found = Find-NamedValue -Value $Value[$key] -Names $Names
            if ($null -ne $found) {
                return $found
            }
        }

        return $null
    }

    if ($Value -is [pscustomobject]) {
        foreach ($name in $Names) {
            $property = $Value.PSObject.Properties | Where-Object { $_.Name -ieq $name } | Select-Object -First 1
            if ($null -ne $property) {
                return $property.Value
            }
        }

        foreach ($property in $Value.PSObject.Properties) {
            $found = Find-NamedValue -Value $property.Value -Names $Names
            if ($null -ne $found) {
                return $found
            }
        }

        return $null
    }

    if (($Value -is [System.Collections.IEnumerable]) -and -not ($Value -is [string])) {
        foreach ($item in $Value) {
            $found = Find-NamedValue -Value $item -Names $Names
            if ($null -ne $found) {
                return $found
            }
        }
    }

    return $null
}

function Find-Guid {
    param(
        [object]$Value,
        [string[]]$Names = @("id", "submissionId", "problemId")
    )

    $candidate = Find-NamedValue -Value $Value -Names $Names
    $guid = [guid]::Empty

    if ($null -ne $candidate -and [guid]::TryParse([string]$candidate, [ref]$guid)) {
        return $guid
    }

    return $null
}

function Convert-StatusName([object]$status) {
    if ($null -eq $status) {
        return ""
    }

    $text = [string]$status
    $number = 0

    if ([int]::TryParse($text, [ref]$number)) {
        return @{
            1 = "Pending"
            2 = "Judging"
            3 = "Accepted"
            4 = "WrongAnswer"
            5 = "TimeLimitExceeded"
            6 = "MemoryLimitExceeded"
            7 = "RuntimeError"
            8 = "CompileError"
            9 = "SystemError"
        }[$number]
    }

    return $text
}

function Wait-Submission([guid]$submissionId) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $last = $null

    while ((Get-Date) -lt $deadline) {
        $last = Invoke-Api -Method Get -Path "/api/submissions/$submissionId" -Headers $headers
        $status = Convert-StatusName (Find-NamedValue -Value $last -Names @("status"))

        if ($status -notin @("Pending", "Judging", "Queued", "Running", "Processing", "Compiling")) {
            return [pscustomobject]@{
                Status = $status
                Body = $last
            }
        }

        Start-Sleep -Milliseconds 800
    }

    $lastStatus = if ($null -eq $last) { "Unknown" } else { Convert-StatusName (Find-NamedValue -Value $last -Names @("status")) }
    throw "Submission $submissionId timed out. Last status: $lastStatus"
}

function Submit-And-AssertAccepted {
    param(
        [string]$Name,
        [int]$Language,
        [string]$SourceCode
    )

    Write-Step $Name

    $created = Invoke-Api -Method Post -Path "/api/submissions" -Headers $headers -Body @{
        problemId = $problemId
        language = $Language
        sourceCode = $SourceCode
    }

    $submissionId = Find-Guid -Value $created -Names @("id", "submissionId")
    if ($null -eq $submissionId) {
        throw "$Name did not return a submission id."
    }

    $final = Wait-Submission -submissionId $submissionId
    $caseResults = @(Find-NamedValue -Value $final.Body -Names @("caseResults"))
    $caseStatuses = @($caseResults | ForEach-Object {
        Convert-StatusName (Find-NamedValue -Value $_ -Names @("status"))
    })

    $passed = $final.Status -eq "Accepted" -and
        $caseResults.Count -eq 2 -and
        @($caseStatuses | Where-Object { $_ -ne "Accepted" }).Count -eq 0

    if ($passed) {
        Write-Pass "$Name -> Accepted; Cases=2/2"
    }
    else {
        $errorMessage = Find-NamedValue -Value $final.Body -Names @("errorMessage")
        Write-Host "[FAIL] $Name -> $($final.Status); Cases=$($caseStatuses -join ','); Error=$errorMessage"
    }

    $script:results += [pscustomobject]@{
        Test = $Name
        SubmissionId = $submissionId
        Status = $final.Status
        CaseStatuses = $caseStatuses
        Passed = $passed
    }

    if (-not $passed) {
        throw "$Name custom struct E2E failed."
    }
}

$functionSpec = @{
    types = @(
        @{
            name = "Point3"
            fields = @(
                @{ name = "x"; type = "double" },
                @{ name = "y"; type = "double" },
                @{ name = "z"; type = "double" }
            )
        },
        @{
            name = "Triangle"
            fields = @(
                @{ name = "a"; type = "Point3" },
                @{ name = "b"; type = "Point3" },
                @{ name = "c"; type = "Point3" }
            )
        },
        @{
            name = "Segment3"
            fields = @(
                @{ name = "a"; type = "Point3" },
                @{ name = "b"; type = "Point3" }
            )
        }
    )
    functionName = "geometryScore"
    returnType = "double"
    parameters = @(
        @{ name = "triangles"; type = "Triangle[]" },
        @{ name = "segments"; type = "Segment3[]" }
    )
    supportedLanguages = @("cpp17", "csharp", "c11")
} | ConvertTo-Json -Depth 20 -Compress

$cpp17Source = @'
struct Point3 {
    double x;
    double y;
    double z;
};

struct Triangle {
    Point3 a;
    Point3 b;
    Point3 c;
};

struct Segment3 {
    Point3 a;
    Point3 b;
};

class Solution {
public:
    double geometryScore(vector<Triangle>& triangles, vector<Segment3>& segments) {
        double score = triangles.size() * 100.0 + segments.size() * 10.0;
        if (!triangles.empty()) {
            score += triangles[0].a.x + triangles[0].b.y + triangles[0].c.z;
        }
        if (!segments.empty()) {
            score += segments[0].a.x + segments[0].b.z;
        }
        return score;
    }
};
'@

$csharpSource = @'
public class Point3
{
    public double x;
    public double y;
    public double z;
}

public class Triangle
{
    public Point3 a = new();
    public Point3 b = new();
    public Point3 c = new();
}

public class Segment3
{
    public Point3 a = new();
    public Point3 b = new();
}

public class Solution
{
    public double GeometryScore(Triangle[] triangles, Segment3[] segments)
    {
        double score = triangles.Length * 100.0 + segments.Length * 10.0;
        if (triangles.Length > 0)
        {
            score += triangles[0].a.x + triangles[0].b.y + triangles[0].c.z;
        }
        if (segments.Length > 0)
        {
            score += segments[0].a.x + segments[0].b.z;
        }
        return score;
    }
}
'@

$c11Source = @'
typedef struct Point3 {
    double x;
    double y;
    double z;
} Point3;

typedef struct Triangle {
    Point3 a;
    Point3 b;
    Point3 c;
} Triangle;

typedef struct Segment3 {
    Point3 a;
    Point3 b;
} Segment3;

double geometryScore(Triangle* triangles, int trianglesSize, Segment3* segments, int segmentsSize) {
    double score = trianglesSize * 100.0 + segmentsSize * 10.0;

    if (trianglesSize > 0) {
        score += triangles[0].a.x + triangles[0].b.y + triangles[0].c.z;
    }

    if (segmentsSize > 0) {
        score += segments[0].a.x + segments[0].b.z;
    }

    return score;
}
'@

$starterCode = @{
    cpp17 = $cpp17Source
    csharp = $csharpSource
    c11 = $c11Source
} | ConvertTo-Json -Depth 20 -Compress

$argumentsCase1 = @{
    triangles = @(
        @{
            a = @{ x = 1.0; y = 2.0; z = 3.0 }
            b = @{ x = 4.0; y = 5.0; z = 6.0 }
            c = @{ x = 7.0; y = 8.0; z = 9.0 }
        }
    )
    segments = @(
        @{
            a = @{ x = 10.0; y = 11.0; z = 12.0 }
            b = @{ x = 13.0; y = 14.0; z = 15.0 }
        }
    )
} | ConvertTo-Json -Depth 20 -Compress

$argumentsCase2 = @{
    triangles = @()
    segments = @()
} | ConvertTo-Json -Depth 20 -Compress

try {
    Write-Host "========================================"
    Write-Host "OnlineJudge Function Struct Smoke Test"
    Write-Host "========================================"

    Write-Step "Checking Docker"
    docker version *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not available."
    }
    Write-Pass "Docker"

    Write-Step "Starting PostgreSQL And Redis"
    docker compose up -d postgres redis
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to start PostgreSQL / Redis."
    }
    Write-Pass "PostgreSQL / Redis"

    Write-Step "Checking Judge Sandbox Images"
    $cppImage = docker image inspect onlinejudge-cpp17-sandbox:latest 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Missing image: onlinejudge-cpp17-sandbox:latest"
    }
    Write-Pass "onlinejudge-cpp17-sandbox:latest"

    $csharpImage = docker image inspect onlinejudge-csharp-sandbox:latest 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Missing image: onlinejudge-csharp-sandbox:latest"
    }
    Write-Pass "onlinejudge-csharp-sandbox:latest"

    Write-Step "Checking API"
    if (Test-Api) {
        Write-Pass "API Already Running"
    }
    else {
        $apiProcess = Start-Process dotnet `
            -ArgumentList @("run", "--no-build", "--project", "OnlineJudge.Api", "--urls", $ApiBase) `
            -WorkingDirectory $root `
            -RedirectStandardOutput $apiOut `
            -RedirectStandardError $apiErr `
            -WindowStyle Hidden `
            -PassThru

        Wait-Api 30
        Write-Pass "API Started By Smoke Test"
    }

    Write-Step "Checking JudgeWorker"
    if (Test-Worker) {
        Write-Pass "JudgeWorker Already Running"
    }
    else {
        $workerProcess = Start-Process dotnet `
            -ArgumentList @("run", "--no-build", "--project", "OnlineJudge.JudgeWorker") `
            -WorkingDirectory $root `
            -RedirectStandardOutput $workerOut `
            -RedirectStandardError $workerErr `
            -WindowStyle Hidden `
            -PassThru

        Start-Sleep -Seconds 2

        if ($workerProcess.HasExited) {
            $out = if (Test-Path $workerOut) { Get-Content $workerOut -Raw } else { "" }
            $err = if (Test-Path $workerErr) { Get-Content $workerErr -Raw } else { "" }
            throw "JudgeWorker exited during startup.`n$out`n$err"
        }

        Write-Pass "JudgeWorker Started By Smoke Test"
    }

    Write-Step "Logging In"
    $login = Invoke-Api -Method Post -Path "/api/auth/login" -Body @{
        account = $Account
        password = $Password
    }

    $token = Find-NamedValue -Value $login -Names @("accessToken")
    if ([string]::IsNullOrWhiteSpace([string]$token)) {
        throw "Login response does not contain accessToken."
    }

    $headers = @{ Authorization = "Bearer $token" }
    Write-Pass "Root Login"

    Write-Step "Creating Temporary Custom Struct Function Problem"
    $problemBody = @{
        title = "[SMOKE] Custom Struct Geometry $runId"
        description = "Temporary custom struct function judge smoke problem."
        inputDescription = ""
        outputDescription = ""
        timeLimitMs = 2000
        memoryLimitMb = 128
        isPublished = $false
        judgeMode = 2
        functionSpecJson = $functionSpec
        starterCodeJson = $starterCode
    }
    $createdProblem = Invoke-Api -Method Post -Path "/api/problems" -Headers $headers -Body $problemBody

    $problemId = Find-Guid -Value $createdProblem -Names @("id", "problemId")
    if ($null -eq $problemId) {
        throw "Problem creation did not return a problem id."
    }

    Write-Pass "Temporary Problem $problemId"

    Write-Step "Adding Custom Struct Test Cases"
    Invoke-Api -Method Post -Path "/api/problems/$problemId/test-cases" -Headers $headers -Body @{
        input = ""
        expectedOutput = ""
        argumentsJson = $argumentsCase1
        expectedJson = "150"
        visibility = 1
        score = 50
    } | Out-Null

    Invoke-Api -Method Post -Path "/api/problems/$problemId/test-cases" -Headers $headers -Body @{
        input = ""
        expectedOutput = ""
        argumentsJson = $argumentsCase2
        expectedJson = "0"
        visibility = 2
        score = 50
    } | Out-Null

    Write-Pass "2 Custom Struct Cases"

    Write-Step "Publishing Custom Struct Function Problem"
    $problemBody.isPublished = $true
    Invoke-Api -Method Put -Path "/api/problems/$problemId" -Headers $headers -Body $problemBody | Out-Null
    Write-Pass "Published Judge Revision"

    Submit-And-AssertAccepted -Name "C++17 Triangle[] + Segment3[]" -Language 1 -SourceCode $cpp17Source
    Submit-And-AssertAccepted -Name "C11 Triangle[] + Segment3[]" -Language 2 -SourceCode $c11Source
    Submit-And-AssertAccepted -Name "C# Triangle[] + Segment3[]" -Language 3 -SourceCode $csharpSource

    $allPassed = @($results | Where-Object { -not $_.Passed }).Count -eq 0 -and $results.Count -eq 3

    $report = [pscustomobject]@{
        Gate = "FUNCTION-SPEC-STRUCT-01"
        Passed = $allPassed
        ProblemId = $problemId
        Tests = $results
        Timestamp = (Get-Date).ToString("o")
    }

    $report | ConvertTo-Json -Depth 20 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "========================================"
    Write-Host "Function Struct Smoke Result"
    Write-Host "========================================"
    Write-Host "C++17 Triangle[] / Segment3[] : PASS"
    Write-Host "C11   Triangle[] / Segment3[] : PASS"
    Write-Host "C#    Triangle[] / Segment3[] : PASS"
    Write-Host "Nested Point3 values          : PASS"
    Write-Host "Empty custom arrays           : PASS"
    Write-Host ""
    Write-Host "RESULT : PASS"
    Write-Host "REPORT : $reportPath"
    Write-Host "========================================"
}
catch {
    $report = [pscustomobject]@{
        Gate = "FUNCTION-SPEC-STRUCT-01"
        Passed = $false
        ProblemId = $problemId
        Tests = $results
        Error = $_.Exception.Message
        Timestamp = (Get-Date).ToString("o")
    }

    $report | ConvertTo-Json -Depth 20 | Set-Content -Path $reportPath -Encoding UTF8

    Write-Host ""
    Write-Host "[FAIL] $($_.Exception.Message)"
    Write-Host "REPORT : $reportPath"
    exit 1
}
finally {
    if ($null -ne $problemId -and $null -ne $headers) {
        Write-Step "Cleaning Temporary Problem"
        try {
            Invoke-Api -Method Delete -Path "/api/problems/$problemId" -Headers $headers | Out-Null
            Write-Pass "Temporary Problem Cleanup"
        }
        catch {
            Write-Host "[WARN] Temporary Problem Cleanup Failed: $($_.Exception.Message)"
        }
    }

    Stop-StartedProcessTree -Process $workerProcess
    Stop-StartedProcessTree -Process $apiProcess
}
