param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$backupRoot = Join-Path $ProjectRoot "artifacts\patch-backup\compile-memory-01-$runId"

function Read-Utf8File {
    param([string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)

    return [pscustomobject]@{
        Text = $text
        Encoding = $encoding
    }
}

function Replace-ExactOnce {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Label
    )

    $first = $Text.IndexOf($Old, [System.StringComparison]::Ordinal)
    if ($first -lt 0) {
        throw "Patch target not found: $Label"
    }

    $second = $Text.IndexOf($Old, $first + $Old.Length, [System.StringComparison]::Ordinal)
    if ($second -ge 0) {
        throw "Patch target is ambiguous: $Label"
    }

    return $Text.Substring(0, $first) + $New + $Text.Substring($first + $Old.Length)
}

$targets = @(
    "OnlineJudge.Application\Judging\Models\LanguageJudgeProfile.cs",
    "OnlineJudge.Infrastructure\Judging\Sandbox\DockerJudgeSandbox.cs",
    "OnlineJudge.Infrastructure\Judging\Runners\Cpp17JudgeRunner.cs",
    "OnlineJudge.Infrastructure\Judging\Runners\C11JudgeRunner.cs",
    "OnlineJudge.Infrastructure\Judging\Runners\CSharpJudgeRunner.cs"
)

$files = @{}

foreach ($relativePath in $targets) {
    $fullPath = Join-Path $ProjectRoot $relativePath
    if (-not (Test-Path $fullPath)) {
        throw "Required file not found: $fullPath"
    }

    $loaded = Read-Utf8File $fullPath
    $files[$relativePath] = [pscustomobject]@{
        FullPath = $fullPath
        Text = $loaded.Text
        Encoding = $loaded.Encoding
    }
}

$profilePath = "OnlineJudge.Application\Judging\Models\LanguageJudgeProfile.cs"
$profile = $files[$profilePath]
$profile.Text = Replace-ExactOnce `
    -Text $profile.Text `
    -Old @'
    public string CompileCommand { get; set; } = string.Empty;

    public string RunCommand { get; set; } = string.Empty;
'@ `
    -New @'
    public string CompileCommand { get; set; } = string.Empty;

    public int CompileMemoryLimitMb { get; set; } = 512;

    public string RunCommand { get; set; } = string.Empty;
'@ `
    -Label "LanguageJudgeProfile.CompileMemoryLimitMb"

$cppPath = "OnlineJudge.Infrastructure\Judging\Runners\Cpp17JudgeRunner.cs"
$cpp = $files[$cppPath]
$cpp.Text = Replace-ExactOnce `
    -Text $cpp.Text `
    -Old @'
        CompileCommand = "g++ main.cpp -std=c++17 -O2 -pipe -s -o main",
        RunCommand = "./main",
'@ `
    -New @'
        CompileCommand = "g++ main.cpp -std=c++17 -O2 -pipe -s -o main",
        CompileMemoryLimitMb = 512,
        RunCommand = "./main",
'@ `
    -Label "Cpp17 compile memory"

$c11Path = "OnlineJudge.Infrastructure\Judging\Runners\C11JudgeRunner.cs"
$c11 = $files[$c11Path]
$c11.Text = Replace-ExactOnce `
    -Text $c11.Text `
    -Old @'
        CompileCommand = "gcc main.c -std=c11 -O2 -pipe -s -o main",
        RunCommand = "./main",
'@ `
    -New @'
        CompileCommand = "gcc main.c -std=c11 -O2 -pipe -s -o main",
        CompileMemoryLimitMb = 512,
        RunCommand = "./main",
'@ `
    -Label "C11 compile memory"

$csharpPath = "OnlineJudge.Infrastructure\Judging\Runners\CSharpJudgeRunner.cs"
$csharp = $files[$csharpPath]
$csharp.Text = Replace-ExactOnce `
    -Text $csharp.Text `
    -Old @'
        CompileCommand = "dotnet build Main.csproj -c Release -o out --nologo --verbosity quiet",
        RunCommand = "dotnet out/Main.dll",
'@ `
    -New @'
        CompileCommand = "dotnet build Main.csproj -c Release -o out --nologo --verbosity quiet",
        CompileMemoryLimitMb = 1024,
        RunCommand = "dotnet out/Main.dll",
'@ `
    -Label "CSharp compile memory"

$sandboxPath = "OnlineJudge.Infrastructure\Judging\Sandbox\DockerJudgeSandbox.cs"
$sandbox = $files[$sandboxPath]
$sandbox.Text = Replace-ExactOnce `
    -Text $sandbox.Text `
    -Old @'
            var compileResult = await RunDockerCommandAsync(
                tempDirectory,
                request.MemoryLimitMb,
                profile.DockerImageName,
                profile.CompileCommand,
'@ `
    -New @'
            var compileResult = await RunDockerCommandAsync(
                tempDirectory,
                JudgeResourceLimits.ResolveCompileMemoryLimitMb(profile),
                profile.DockerImageName,
                profile.CompileCommand,
'@ `
    -Label "Docker compile memory source"

$sandbox.Text = Replace-ExactOnce `
    -Text $sandbox.Text `
    -Old @'
            var runResult = await RunDockerCommandAsync(
                tempDirectory,
                request.MemoryLimitMb,
                profile.DockerImageName,
'@ `
    -New @'
            var runResult = await RunDockerCommandAsync(
                tempDirectory,
                JudgeResourceLimits.ResolveRunMemoryLimitMb(request.MemoryLimitMb),
                profile.DockerImageName,
'@ `
    -Label "Docker run memory source"

# All patch targets were validated in memory before anything is written.
foreach ($relativePath in $targets) {
    $entry = $files[$relativePath]
    $backupPath = Join-Path $backupRoot $relativePath
    $backupDir = Split-Path -Parent $backupPath
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Copy-Item $entry.FullPath $backupPath -Force
}

foreach ($relativePath in $targets) {
    $entry = $files[$relativePath]
    [System.IO.File]::WriteAllText($entry.FullPath, $entry.Text, $entry.Encoding)
}

Write-Host "[PASS] Compile/runtime memory limits separated."
Write-Host "BACKUP : $backupRoot"
Write-Host "NEXT   : dotnet build OnlineJudge.sln"
