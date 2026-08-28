param(
    [switch]$AllowDifferentBase,
    [switch]$RecoverFailedV1
)

$ErrorActionPreference = "Stop"
$ExpectedBase = "dbe85a86aebf802edbedf7a6a15dd71a191e8b4d"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $RepoRoot

if (!(Test-Path (Join-Path $RepoRoot "OnlineJudge.sln"))) {
    throw "Please extract this ZIP into the OnlineJudge repository root, then run scripts\Apply-ProblemMetadataUx01.ps1."
}

$Head = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Git HEAD."
}

if ($Head -ne $ExpectedBase -and !$AllowDifferentBase) {
    throw "Unexpected base commit: $Head. Expected: $ExpectedBase. Re-run with -AllowDifferentBase only after manually confirming compatibility."
}

$TrackedTargets = @(
    "frontend/src/components/CodeEditor.tsx",
    "frontend/src/api/problemsApi.ts",
    "frontend/src/pages/ProblemDetailPage.tsx",
    "frontend/src/pages/AdminProblemEditorPage.tsx",
    "OnlineJudge.Domain/Entities/Problem.cs",
    "OnlineJudge.Application/Problems/Dtos/ProblemDetailDto.cs",
    "OnlineJudge.Application/Problems/Requests/CreateProblemRequest.cs",
    "OnlineJudge.Application/Problems/Requests/UpdateProblemRequest.cs",
    "OnlineJudge.Infrastructure/Persistence/Configurations/ProblemConfiguration.cs",
    "OnlineJudge.Infrastructure/Persistence/Migrations/OnlineJudgeDbContextModelSnapshot.cs",
    "OnlineJudge.Infrastructure/Problems/ProblemService.cs",
    "OnlineJudge.Infrastructure/Submissions/SubmissionService.cs"
)

$MigrationPath = "OnlineJudge.Infrastructure/Persistence/Migrations/20260828103000_AddProblemAllowedLanguagesMask.cs"
$TestPath = "OnlineJudge.Tests/Problems/ProblemMetadataUxTests.cs"

$KnownFailedV1DirtyTargets = @(
    "frontend/src/components/CodeEditor.tsx",
    "OnlineJudge.Domain/Entities/Problem.cs",
    "OnlineJudge.Application/Problems/Dtos/ProblemDetailDto.cs",
    "OnlineJudge.Application/Problems/Requests/CreateProblemRequest.cs",
    "OnlineJudge.Application/Problems/Requests/UpdateProblemRequest.cs",
    "OnlineJudge.Infrastructure/Persistence/Configurations/ProblemConfiguration.cs"
)

$DirtyTargets = @()
foreach ($Path in $TrackedTargets) {
    git diff --quiet -- $Path
    $UnstagedDirty = $LASTEXITCODE -ne 0

    git diff --cached --quiet -- $Path
    $StagedDirty = $LASTEXITCODE -ne 0

    if ($UnstagedDirty -or $StagedDirty) {
        $DirtyTargets += $Path
    }
}

if ($DirtyTargets.Count -gt 0) {
    if (!$RecoverFailedV1) {
        throw "Target files already contain modifications: $($DirtyTargets -join ', '). If this is the failed v1 overlay state, re-run with -RecoverFailedV1."
    }

    $UnexpectedDirtyTargets = @($DirtyTargets | Where-Object { $_ -notin $KnownFailedV1DirtyTargets })
    if ($UnexpectedDirtyTargets.Count -gt 0) {
        throw "Recovery refused because unexpected target files are dirty: $($UnexpectedDirtyTargets -join ', '). No files were restored."
    }

    if (Test-Path $MigrationPath) {
        throw "Recovery refused because migration already exists: $MigrationPath"
    }

    if (Test-Path $TestPath) {
        throw "Recovery refused because test file already exists: $TestPath"
    }

    Write-Host "Detected known failed-v1 partial state. Restoring only the six files v1 could modify before its first ProblemService patch..."
    foreach ($Path in $DirtyTargets) {
        git restore --staged --worktree -- $Path
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to restore known partial file: $Path"
        }
    }
}

foreach ($Path in $TrackedTargets) {
    git diff --quiet -- $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Target file has unstaged modifications after recovery: $Path"
    }

    git diff --cached --quiet -- $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Target file has staged modifications after recovery: $Path"
    }
}

if (Test-Path $MigrationPath) {
    throw "Target migration already exists: $MigrationPath"
}

if (Test-Path $TestPath) {
    throw "Target test file already exists: $TestPath"
}

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupRoot = Join-Path $RepoRoot "artifacts/overlay-backup/PROBLEM-METADATA-UX-01-$Stamp"

foreach ($Path in $TrackedTargets) {
    $Source = Join-Path $RepoRoot $Path
    $Destination = Join-Path $BackupRoot $Path
    New-Item -ItemType Directory -Force -Path (Split-Path $Destination) | Out-Null
    Copy-Item $Source $Destination
}

function Replace-Exact {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Old,
        [Parameter(Mandatory = $true)][string]$New,
        [int]$ExpectedCount = 1,
        [string]$Name = ""
    )

    $FullPath = Join-Path $RepoRoot $Path
    $Original = [System.IO.File]::ReadAllText($FullPath)
    $UseCrLf = $Original.Contains("`r`n")
    $Normalized = $Original.Replace("`r`n", "`n")
    $OldNormalized = $Old.Replace("`r`n", "`n")
    $NewNormalized = $New.Replace("`r`n", "`n")
    $Count = [regex]::Matches($Normalized, [regex]::Escape($OldNormalized)).Count

    if ($Count -ne $ExpectedCount) {
        $PatchName = if ([string]::IsNullOrWhiteSpace($Name)) { $Path } else { "$Path [$Name]" }
        throw "Patch context mismatch in $PatchName. Expected $ExpectedCount occurrence(s), found $Count."
    }

    $Updated = $Normalized.Replace($OldNormalized, $NewNormalized)
    if ($UseCrLf) {
        $Updated = $Updated.Replace("`n", "`r`n")
    }

    [System.IO.File]::WriteAllText($FullPath, $Updated, [System.Text.UTF8Encoding]::new($false))
}

function Write-NewFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $FullPath = Join-Path $RepoRoot $Path
    if (Test-Path $FullPath) {
        throw "Refusing to overwrite new file: $Path"
    }

    New-Item -ItemType Directory -Force -Path (Split-Path $FullPath) | Out-Null
    $Output = $Content.Replace("`r`n", "`n").Replace("`n", [Environment]::NewLine)
    [System.IO.File]::WriteAllText($FullPath, $Output, [System.Text.UTF8Encoding]::new($false))
}

# 1. Monaco Ctrl + mouse wheel zoom.
Replace-Exact "frontend/src/components/CodeEditor.tsx" @'
          minimap: { enabled: false },
          readOnly,
'@ @'
          minimap: { enabled: false },
          mouseWheelZoom: true,
          readOnly,
'@

# 2. Problem persistence contract.
Replace-Exact "OnlineJudge.Domain/Entities/Problem.cs" @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    /// <summary>
    /// Function judge signature and parameter metadata.
'@ @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    /// <summary>
    /// Bit mask of explicitly allowed judge languages. 0 means unrestricted.
    /// C++17 = 1, C11 = 2, C# = 4.
    /// </summary>
    public int AllowedLanguagesMask { get; set; }

    /// <summary>
    /// Function judge signature and parameter metadata.
'@

Replace-Exact "OnlineJudge.Infrastructure/Persistence/Configurations/ProblemConfiguration.cs" @'
        builder.Property(problem => problem.JudgeMode)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(JudgeMode.StandardInputOutput);

        builder.Property(problem => problem.FunctionSpecJson)
'@ @'
        builder.Property(problem => problem.JudgeMode)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(JudgeMode.StandardInputOutput);

        builder.Property(problem => problem.AllowedLanguagesMask)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(problem => problem.FunctionSpecJson)
'@

Replace-Exact "OnlineJudge.Application/Problems/Dtos/ProblemDetailDto.cs" @'
    public JudgeMode JudgeMode { get; set; }

    public string? FunctionSpecJson { get; set; }
'@ @'
    public JudgeMode JudgeMode { get; set; }

    public int AllowedLanguagesMask { get; set; }

    public int TotalScore { get; set; }

    public string? FunctionSpecJson { get; set; }
'@

Replace-Exact "OnlineJudge.Application/Problems/Requests/CreateProblemRequest.cs" @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public string? FunctionSpecJson { get; set; }
'@ @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public int AllowedLanguagesMask { get; set; }

    public string? FunctionSpecJson { get; set; }
'@

Replace-Exact "OnlineJudge.Application/Problems/Requests/UpdateProblemRequest.cs" @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public string? FunctionSpecJson { get; set; }
'@ @'
    public JudgeMode JudgeMode { get; set; } = JudgeMode.StandardInputOutput;

    public int AllowedLanguagesMask { get; set; }

    public string? FunctionSpecJson { get; set; }
'@

# 3. Problem service: validate/persist language mask and expose full test-case score sum.
Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
public class ProblemService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser) : IProblemService
{
'@ @'
public class ProblemService(OnlineJudgeDbContext dbContext, ICurrentUser currentUser) : IProblemService
{
    private const int AllAllowedLanguagesMask = 0b111;
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
        var validation = ValidateProblemRequest(request.JudgeMode, request.FunctionSpecJson, request.StarterCodeJson);
'@ @'
        var validation = ValidateProblemRequest(request.JudgeMode, request.AllowedLanguagesMask, request.FunctionSpecJson, request.StarterCodeJson);
'@ -ExpectedCount 2

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
            IsPublished = request.IsPublished,
            JudgeMode = request.JudgeMode,
            FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null,
'@ @'
            IsPublished = request.IsPublished,
            JudgeMode = request.JudgeMode,
            AllowedLanguagesMask = request.AllowedLanguagesMask,
            FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null,
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
        problem.IsPublished = request.IsPublished;
        problem.JudgeMode = request.JudgeMode;
        problem.FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null;
'@ @'
        problem.IsPublished = request.IsPublished;
        problem.JudgeMode = request.JudgeMode;
        problem.AllowedLanguagesMask = request.AllowedLanguagesMask;
        problem.FunctionSpecJson = request.JudgeMode == JudgeMode.Function ? request.FunctionSpecJson : null;
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
    private static Result ValidateProblemRequest(JudgeMode judgeMode, string? functionSpecJson, string? starterCodeJson)
    {
        if (!Enum.IsDefined(judgeMode))
        {
            return Result.Failure("Unsupported judge mode.");
        }

        if (judgeMode == JudgeMode.StandardInputOutput)
'@ @'
    private static Result ValidateProblemRequest(JudgeMode judgeMode, int allowedLanguagesMask, string? functionSpecJson, string? starterCodeJson)
    {
        if (!Enum.IsDefined(judgeMode))
        {
            return Result.Failure("Unsupported judge mode.");
        }

        if (allowedLanguagesMask < 0 || (allowedLanguagesMask & ~AllAllowedLanguagesMask) != 0)
        {
            return Result.Failure("Unsupported allowed languages mask.");
        }

        if (judgeMode == JudgeMode.StandardInputOutput)
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
            IsPublished = problem.IsPublished,
            JudgeMode = problem.JudgeMode,
            FunctionSpecJson = problem.FunctionSpecJson,
            StarterCodeJson = problem.StarterCodeJson,
'@ @'
            IsPublished = problem.IsPublished,
            JudgeMode = problem.JudgeMode,
            AllowedLanguagesMask = problem.AllowedLanguagesMask,
            TotalScore = problem.TestCases.Sum(testCase => testCase.Score),
            FunctionSpecJson = problem.FunctionSpecJson,
            StarterCodeJson = problem.StarterCodeJson,
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
        var specResult = FunctionJudgeSpecParser.Parse(functionSpecJson);
        if (specResult.IsFailure)
        {
            return Result.Failure(specResult.ErrorMessage!);
        }

        return FunctionJudgeSpecParser.ValidateStarterCode(starterCodeJson);
'@ @'
        var specResult = FunctionJudgeSpecParser.Parse(functionSpecJson);
        if (specResult.IsFailure)
        {
            return Result.Failure(specResult.ErrorMessage!);
        }

        var languageValidation = ValidateFunctionAllowedLanguages(allowedLanguagesMask, functionSpecJson);
        if (languageValidation.IsFailure)
        {
            return languageValidation;
        }

        return FunctionJudgeSpecParser.ValidateStarterCode(starterCodeJson);
'@

Replace-Exact "OnlineJudge.Infrastructure/Problems/ProblemService.cs" @'
    private static Result ValidateTestCaseRequest(Problem problem, CreateTestCaseRequest request)
'@ @'
    private static Result ValidateFunctionAllowedLanguages(int allowedLanguagesMask, string? functionSpecJson)
    {
        if (allowedLanguagesMask == 0 || string.IsNullOrWhiteSpace(functionSpecJson))
        {
            return Result.Success();
        }

        try
        {
            using var document = JsonDocument.Parse(functionSpecJson);
            if (!document.RootElement.TryGetProperty("supportedLanguages", out var supportedLanguages)
                || supportedLanguages.ValueKind != JsonValueKind.Array)
            {
                return Result.Success();
            }

            var supportedMask = 0;
            foreach (var item in supportedLanguages.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                supportedMask |= item.GetString()?.ToLowerInvariant() switch
                {
                    "cpp17" => 0b001,
                    "c11" => 0b010,
                    "csharp" => 0b100,
                    _ => 0
                };
            }

            return (allowedLanguagesMask & ~supportedMask) == 0
                ? Result.Success()
                : Result.Failure("Allowed languages include a language not supported by the function spec.");
        }
        catch (JsonException)
        {
            return Result.Success();
        }
    }

    private static Result ValidateTestCaseRequest(Problem problem, CreateTestCaseRequest request)
'@

# 4. Submission service: enforce explicit problem restriction and FunctionSpec supportedLanguages server-side.
Replace-Exact "OnlineJudge.Infrastructure/Submissions/SubmissionService.cs" @'
using Microsoft.EntityFrameworkCore;
'@ @'
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
'@

Replace-Exact "OnlineJudge.Infrastructure/Submissions/SubmissionService.cs" @'
        if (problem is null)
        {
            return Result<SubmissionDto>.Failure("Problem not found.");
        }

        if (problem.JudgeMode == JudgeMode.Function
'@ @'
        if (problem is null)
        {
            return Result<SubmissionDto>.Failure("Problem not found.");
        }

        if (!IsLanguageAllowed(problem.AllowedLanguagesMask, request.Language))
        {
            return Result<SubmissionDto>.Failure("Selected language is not allowed for this problem.");
        }

        if (problem.JudgeMode == JudgeMode.Function && !IsFunctionLanguageSupported(problem.FunctionSpecJson, request.Language))
        {
            return Result<SubmissionDto>.Failure("Selected language is not supported by this function problem.");
        }

        if (problem.JudgeMode == JudgeMode.Function
'@

Replace-Exact "OnlineJudge.Infrastructure/Submissions/SubmissionService.cs" @'
    private async Task EnsureParticipantForChallengeTaskAsync(Guid challengeTaskId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
'@ @'
    private static bool IsLanguageAllowed(int allowedLanguagesMask, JudgeLanguage language)
    {
        if (allowedLanguagesMask == 0)
        {
            return true;
        }

        var languageMask = language switch
        {
            JudgeLanguage.Cpp17 => 1,
            JudgeLanguage.C11 => 2,
            JudgeLanguage.CSharp => 4,
            _ => 0
        };

        return languageMask != 0 && (allowedLanguagesMask & languageMask) != 0;
    }

    private static bool IsFunctionLanguageSupported(string? functionSpecJson, JudgeLanguage language)
    {
        if (string.IsNullOrWhiteSpace(functionSpecJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(functionSpecJson);
            if (!document.RootElement.TryGetProperty("supportedLanguages", out var supportedLanguages)
                || supportedLanguages.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            var languageKey = language switch
            {
                JudgeLanguage.Cpp17 => "cpp17",
                JudgeLanguage.C11 => "c11",
                JudgeLanguage.CSharp => "csharp",
                _ => string.Empty
            };

            return !string.IsNullOrEmpty(languageKey)
                && supportedLanguages.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), languageKey, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private async Task EnsureParticipantForChallengeTaskAsync(Guid challengeTaskId, Guid userId, DateTimeOffset joinedAt, CancellationToken cancellationToken)
'@

# 5. EF snapshot.
Replace-Exact "OnlineJudge.Infrastructure/Persistence/Migrations/OnlineJudgeDbContextModelSnapshot.cs" @'
            modelBuilder.Entity("OnlineJudge.Domain.Entities.Problem", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");
'@ @'
            modelBuilder.Entity("OnlineJudge.Domain.Entities.Problem", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<int>("AllowedLanguagesMask")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone");
'@

# Ensure the snapshot replacement landed in Problem, not another entity.
$Snapshot = [System.IO.File]::ReadAllText((Join-Path $RepoRoot "OnlineJudge.Infrastructure/Persistence/Migrations/OnlineJudgeDbContextModelSnapshot.cs"))
$ProblemStart = $Snapshot.IndexOf('modelBuilder.Entity("OnlineJudge.Domain.Entities.Problem", b =>')
$ProblemEnd = $Snapshot.IndexOf('modelBuilder.Entity("OnlineJudge.Domain.Entities.ProblemCollaborator", b =>')
$AllowedIndex = $Snapshot.IndexOf('b.Property<int>("AllowedLanguagesMask")')
if ($ProblemStart -lt 0 -or $ProblemEnd -lt 0 -or $AllowedIndex -lt $ProblemStart -or $AllowedIndex -gt $ProblemEnd) {
    throw "AllowedLanguagesMask snapshot property was not placed inside the Problem model block."
}

# 6. Frontend API contract.
Replace-Exact "frontend/src/api/problemsApi.ts" @'
export interface ProblemDetailDto {
  id: string;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  isPublished: boolean;
  judgeMode: JudgeMode;
  functionSpecJson?: string | null;
'@ @'
export interface ProblemDetailDto {
  id: string;
  title: string;
  description: string;
  inputDescription: string;
  outputDescription: string;
  timeLimitMs: number;
  memoryLimitMb: number;
  isPublished: boolean;
  judgeMode: JudgeMode;
  allowedLanguagesMask: number;
  totalScore: number;
  functionSpecJson?: string | null;
'@

Replace-Exact "frontend/src/api/problemsApi.ts" @'
  judgeMode: JudgeMode;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
}

export type UpdateProblemRequest = CreateProblemRequest;
'@ @'
  judgeMode: JudgeMode;
  allowedLanguagesMask: number;
  functionSpecJson?: string | null;
  starterCodeJson?: string | null;
}

export type UpdateProblemRequest = CreateProblemRequest;
'@

# 7. Admin editor language restriction UI.
Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
const baseFunctionTypes = ["int", "long", "double", "bool", "string", "int[]", "long[]", "double[]", "bool[]", "string[]", "int[][]", "ListNode<int>", "TreeNode<int>"];
const customFieldPrimitiveTypes = ["int", "long", "double", "bool", "string"];
'@ @'
const baseFunctionTypes = ["int", "long", "double", "bool", "string", "int[]", "long[]", "double[]", "bool[]", "string[]", "int[][]", "ListNode<int>", "TreeNode<int>"];
const customFieldPrimitiveTypes = ["int", "long", "double", "bool", "string"];
const allLanguageMask = 0b111;
const languageOptions = [
  { mask: 0b001, label: "C++" },
  { mask: 0b010, label: "C" },
  { mask: 0b100, label: "C#" }
] as const;
'@

Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
  const [isPublished, setIsPublished] = useState(false);
  const [judgeMode, setJudgeMode] = useState<JudgeMode>(1);
  const [functionName, setFunctionName] = useState("");
'@ @'
  const [isPublished, setIsPublished] = useState(false);
  const [judgeMode, setJudgeMode] = useState<JudgeMode>(1);
  const [isLanguageRestricted, setIsLanguageRestricted] = useState(false);
  const [allowedLanguagesMask, setAllowedLanguagesMask] = useState(allLanguageMask);
  const [functionName, setFunctionName] = useState("");
'@

Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
          setIsPublished(detail.isPublished);
          setJudgeMode(detail.judgeMode);
          applyFunctionConfig(detail);
'@ @'
          setIsPublished(detail.isPublished);
          setJudgeMode(detail.judgeMode);
          setIsLanguageRestricted(detail.allowedLanguagesMask !== 0);
          setAllowedLanguagesMask(detail.allowedLanguagesMask || allLanguageMask);
          applyFunctionConfig(detail);
'@

Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
    setIsSaving(true);
    setError(null);
    setNotice(null);

    const functionConfig = buildFunctionConfig();
'@ @'
    setIsSaving(true);
    setError(null);
    setNotice(null);

    const selectedAllowedLanguagesMask = isLanguageRestricted ? allowedLanguagesMask : 0;
    if (isLanguageRestricted && selectedAllowedLanguagesMask === 0) {
      setError("限定提交语言时至少选择一种语言");
      setIsSaving(false);
      return;
    }

    if (judgeMode === 2
      && (selectedAllowedLanguagesMask & 0b010) !== 0
      && hasC11UnsupportedType(returnType, parameters, customTypes)) {
      setError("当前函数签名不支持 C11，请取消 C 语言限制或调整函数类型");
      setIsSaving(false);
      return;
    }

    const functionConfig = buildFunctionConfig();
'@

Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
      isPublished,
      judgeMode,
      functionSpecJson: functionConfig.functionSpecJson,
'@ @'
      isPublished,
      judgeMode,
      allowedLanguagesMask: selectedAllowedLanguagesMask,
      functionSpecJson: functionConfig.functionSpecJson,
'@

Replace-Exact "frontend/src/pages/AdminProblemEditorPage.tsx" @'
        </label>

        {judgeMode === 1 ? (
'@ @'
        </label>

        <section className="content-block">
          <h2>提交语言</h2>
          <label className="checkbox-line">
            <input
              type="checkbox"
              checked={isLanguageRestricted}
              onChange={(event) => {
                setIsLanguageRestricted(event.target.checked);
                if (event.target.checked && allowedLanguagesMask === 0) {
                  setAllowedLanguagesMask(allLanguageMask);
                }
              }}
            />
            限定提交语言
          </label>
          {isLanguageRestricted && (
            <div className="form-row">
              {languageOptions.map(({ mask, label }) => (
                <label className="checkbox-line" key={mask}>
                  <input
                    type="checkbox"
                    checked={(allowedLanguagesMask & mask) !== 0}
                    onChange={(event) => setAllowedLanguagesMask((current) => event.target.checked ? current | mask : current & ~mask)}
                  />
                  {label}
                </label>
              ))}
            </div>
          )}
          <p className="quiet-note">未限定时允许所有判题语言；函数式题目仍会自动排除当前函数签名不支持的语言。</p>
        </section>

        {judgeMode === 1 ? (
'@

# 8. Problem detail: total score, explicit language tags, effective language selector.
Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
  const functionSpec = useMemo(() => parseFunctionSpec(problem?.functionSpecJson), [problem?.functionSpecJson]);
  const sampleTestCases = useMemo(() => problem?.testCases.filter((testCase) => testCase.visibility === 1) || [], [problem?.testCases]);
  const sampleScoreTotal = useMemo(() => sampleTestCases.reduce((total, testCase) => total + testCase.score, 0), [sampleTestCases]);
  const hasListNode = useMemo(() => hasFunctionSpecListNode(functionSpec), [functionSpec]);
'@ @'
  const functionSpec = useMemo(() => parseFunctionSpec(problem?.functionSpecJson), [problem?.functionSpecJson]);
  const sampleTestCases = useMemo(() => problem?.testCases.filter((testCase) => testCase.visibility === 1) || [], [problem?.testCases]);
  const availableLanguages = useMemo(
    () => getAvailableLanguages(problem?.allowedLanguagesMask ?? 0, functionSpec),
    [problem?.allowedLanguagesMask, functionSpec]
  );
  const explicitLanguageTags = useMemo(
    () => getLanguagesFromMask(problem?.allowedLanguagesMask ?? 0),
    [problem?.allowedLanguagesMask]
  );
  const hasListNode = useMemo(() => hasFunctionSpecListNode(functionSpec), [functionSpec]);
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
    getProblem(id)
      .then((detail) => {
        setProblem(detail);
        if (detail.judgeMode === 2) {
          const cachedLanguage = languageCacheKey ? Number(localStorage.getItem(languageCacheKey)) : 1;
          const parsedSpec = parseFunctionSpec(detail.functionSpecJson);
          const c11UnsupportedProblem = hasFunctionSpecListNode(parsedSpec) || hasFunctionSpecTreeNode(parsedSpec);
          setLanguage((cachedLanguage === 2 && !c11UnsupportedProblem) || cachedLanguage === 3 ? cachedLanguage : 1);
        }
      })
'@ @'
    getProblem(id)
      .then((detail) => {
        setProblem(detail);
        const cachedLanguage = languageCacheKey ? Number(localStorage.getItem(languageCacheKey)) : 1;
        const parsedSpec = parseFunctionSpec(detail.functionSpecJson);
        const languages = getAvailableLanguages(detail.allowedLanguagesMask, parsedSpec);
        const cachedJudgeLanguage = cachedLanguage as JudgeLanguage;
        setLanguage(languages.includes(cachedJudgeLanguage) ? cachedJudgeLanguage : languages[0] ?? 1);
      })
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
  useEffect(() => {
    if (!languageCacheKey) {
      return;
    }

    const cachedLanguage = Number(localStorage.getItem(languageCacheKey));
    if (problem?.judgeMode === 2) {
      setLanguage((cachedLanguage === 2 && !hasC11UnsupportedComplexType) || cachedLanguage === 3 ? cachedLanguage : 1);
      return;
    }

    if (cachedLanguage === 1 || cachedLanguage === 2 || cachedLanguage === 3) {
      setLanguage(cachedLanguage as JudgeLanguage);
    }
  }, [hasC11UnsupportedComplexType, languageCacheKey, problem?.judgeMode]);
'@ @'
  useEffect(() => {
    if (!languageCacheKey || availableLanguages.length === 0) {
      return;
    }

    setLanguage((current) => availableLanguages.includes(current) ? current : availableLanguages[0]);
  }, [availableLanguages, languageCacheKey]);
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
    if (problem?.judgeMode === 2 && hasC11UnsupportedComplexType && language === 2) {
      setError("C11 暂不支持链表或二叉树函数式判题。");
      return;
    }
'@ @'
    if (!availableLanguages.includes(language)) {
      setError("该题目不允许使用当前语言提交。");
      return;
    }

    if (problem?.judgeMode === 2 && hasC11UnsupportedComplexType && language === 2) {
      setError("C11 暂不支持链表或二叉树函数式判题。");
      return;
    }
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
            <h1>{problem.title}</h1>
            <p>
              {problem.timeLimitMs} ms / {problem.memoryLimitMb} MB / {sampleScoreTotal} 分
            </p>
'@ @'
            <h1>{problem.title}</h1>
            <div className="button-row">
              <span>{problem.timeLimitMs} ms / {problem.memoryLimitMb} MB / {problem.totalScore} 分</span>
              {explicitLanguageTags.map((tagLanguage) => (
                <span className="context-chip" key={tagLanguage}>{getProblemLanguageTag(tagLanguage)}</span>
              ))}
            </div>
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
            <select value={language} onChange={(event) => handleLanguageChange(Number(event.target.value) as JudgeLanguage)}>
              <option value={1}>C++17</option>
              <option value={2} disabled={problem.judgeMode === 2 && hasC11UnsupportedComplexType}>C11</option>
              <option value={3}>C#</option>
            </select>
'@ @'
            <select
              value={language}
              disabled={availableLanguages.length === 0}
              onChange={(event) => handleLanguageChange(Number(event.target.value) as JudgeLanguage)}
            >
              {availableLanguages.map((availableLanguage) => (
                <option key={availableLanguage} value={availableLanguage}>{getJudgeLanguageName(availableLanguage)}</option>
              ))}
            </select>
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
            <button className="button primary" type="submit" disabled={isSubmitting}>
'@ @'
            <button className="button primary" type="submit" disabled={isSubmitting || availableLanguages.length === 0}>
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
function parseFunctionSpec(functionSpecJson?: string | null):
  | { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> }
  | null {
'@ @'
function parseFunctionSpec(functionSpecJson?: string | null):
  | { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }>; supportedLanguages?: string[] }
  | null {
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
    const parsed = JSON.parse(functionSpecJson) as {
      functionName?: string;
      returnType?: string;
      parameters?: Array<{ name: string; type: string }>;
    };
'@ @'
    const parsed = JSON.parse(functionSpecJson) as {
      functionName?: string;
      returnType?: string;
      parameters?: Array<{ name: string; type: string }>;
      supportedLanguages?: string[];
    };
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
    return {
      functionName: parsed.functionName,
      returnType: parsed.returnType,
      parameters: parsed.parameters
    };
'@ @'
    return {
      functionName: parsed.functionName,
      returnType: parsed.returnType,
      parameters: parsed.parameters,
      supportedLanguages: Array.isArray(parsed.supportedLanguages) ? parsed.supportedLanguages : undefined
    };
'@

Replace-Exact "frontend/src/pages/ProblemDetailPage.tsx" @'
function formatJsonString(value?: string | null) {
'@ @'
function getAvailableLanguages(
  allowedLanguagesMask: number,
  functionSpec?: { returnType: string; parameters: Array<{ type: string }>; supportedLanguages?: string[] } | null
): JudgeLanguage[] {
  return ([1, 2, 3] as JudgeLanguage[]).filter((candidate) => {
    const explicitAllowed = allowedLanguagesMask === 0 || (allowedLanguagesMask & getJudgeLanguageMask(candidate)) !== 0;
    return explicitAllowed && isFunctionLanguageSupported(functionSpec, candidate);
  });
}

function getLanguagesFromMask(allowedLanguagesMask: number): JudgeLanguage[] {
  if (allowedLanguagesMask === 0) {
    return [];
  }

  return ([1, 2, 3] as JudgeLanguage[]).filter((candidate) => (allowedLanguagesMask & getJudgeLanguageMask(candidate)) !== 0);
}

function getJudgeLanguageMask(language: JudgeLanguage) {
  return language === 1 ? 0b001 : language === 2 ? 0b010 : 0b100;
}

function getJudgeLanguageName(language: JudgeLanguage) {
  return language === 1 ? "C++17" : language === 2 ? "C11" : "C#";
}

function getProblemLanguageTag(language: JudgeLanguage) {
  return language === 1 ? "C++" : language === 2 ? "C" : "C#";
}

function isFunctionLanguageSupported(
  functionSpec: { returnType: string; parameters: Array<{ type: string }>; supportedLanguages?: string[] } | null | undefined,
  language: JudgeLanguage
) {
  if (!functionSpec) {
    return true;
  }

  if (Array.isArray(functionSpec.supportedLanguages)) {
    const languageKey = language === 1 ? "cpp17" : language === 2 ? "c11" : "csharp";
    return functionSpec.supportedLanguages.some((item) => item.toLowerCase() === languageKey);
  }

  return language !== 2 || (!hasFunctionSpecListNode(functionSpec) && !hasFunctionSpecTreeNode(functionSpec));
}

function formatJsonString(value?: string | null) {
'@

# 9. New migration.
Write-NewFile $MigrationPath @'
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OnlineJudge.Infrastructure.Persistence;

#nullable disable

namespace OnlineJudge.Infrastructure.Persistence.Migrations;

[DbContext(typeof(OnlineJudgeDbContext))]
[Migration("20260828103000_AddProblemAllowedLanguagesMask")]
public class AddProblemAllowedLanguagesMask : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AllowedLanguagesMask",
            table: "Problems",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AllowedLanguagesMask", table: "Problems");
    }
}
'@

# 10. New focused tests.
Write-NewFile $TestPath @'
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OnlineJudge.Application.Common.CurrentUser;
using OnlineJudge.Application.Judging.Services;
using OnlineJudge.Application.Problems.Requests;
using OnlineJudge.Application.Submissions.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.Problems;
using OnlineJudge.Infrastructure.Submissions;

namespace OnlineJudge.Tests.Problems;

public class ProblemMetadataUxTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProblemDetail_Anonymous_UsesAllTestCaseScoresWithoutExposingHiddenCases()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        dbContext.TestCases.AddRange(
            TestCase(ids.ProblemId, TestCaseVisibility.Sample, 1, "sample"),
            TestCase(ids.ProblemId, TestCaseVisibility.Hidden, 99, "hidden"));
        await dbContext.SaveChangesAsync();

        var service = new ProblemService(dbContext, new TestCurrentUser(null, null, false));
        var result = await service.GetProblemAsync(ids.ProblemId);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.TotalScore);
        Assert.Equal(0b010, result.Value.AllowedLanguagesMask);
        var visible = Assert.Single(result.Value.TestCases);
        Assert.Equal(TestCaseVisibility.Sample, visible.Visibility);
        Assert.Equal(1, visible.Score);
    }

    [Fact]
    public async Task Submission_RestrictedProblem_RejectsDisallowedLanguage()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.Cpp17,
            SourceCode = "int main(){}"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Selected language is not allowed for this problem.", result.ErrorMessage);
        Assert.Empty(await dbContext.Submissions.ToListAsync());
    }

    [Fact]
    public async Task Submission_RestrictedProblem_AllowsSelectedLanguage()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(dbContext, JudgeMode.StandardInputOutput, allowedLanguagesMask: 0b010);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.C11,
            SourceCode = "int main(void){return 0;}"
        });

        Assert.True(result.IsSuccess);
        var submission = Assert.Single(await dbContext.Submissions.ToListAsync());
        Assert.Equal(JudgeLanguage.C11, submission.Language);
    }

    [Fact]
    public async Task Submission_FunctionProblem_RejectsLanguageOutsideFunctionSpec()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedProblem(
            dbContext,
            JudgeMode.Function,
            allowedLanguagesMask: 0,
            functionSpecJson: """
                {
                  "functionName": "solve",
                  "returnType": "TreeNode<int>",
                  "parameters": [{ "name": "root", "type": "TreeNode<int>" }],
                  "supportedLanguages": ["cpp17", "csharp"]
                }
                """);
        var service = CreateSubmissionService(dbContext, ids.AnswererId);

        var result = await service.CreateSubmissionAsync(new CreateSubmissionRequest
        {
            ProblemId = ids.ProblemId,
            Language = JudgeLanguage.C11,
            SourceCode = "int solve(){return 0;}"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Selected language is not supported by this function problem.", result.ErrorMessage);
        Assert.Empty(await dbContext.Submissions.ToListAsync());
    }

    [Fact]
    public async Task CreateProblem_FunctionRestrictionOutsideSupportedLanguages_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsers(dbContext);
        var service = new ProblemService(dbContext, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));

        var result = await service.CreateProblemAsync(new CreateProblemRequest
        {
            Title = "Function Language Restriction",
            Description = "Description",
            InputDescription = string.Empty,
            OutputDescription = string.Empty,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = JudgeMode.Function,
            AllowedLanguagesMask = 0b010,
            FunctionSpecJson = """
                {
                  "functionName": "solve",
                  "returnType": "TreeNode<int>",
                  "parameters": [{ "name": "root", "type": "TreeNode<int>" }],
                  "supportedLanguages": ["cpp17", "csharp"]
                }
                """,
            StarterCodeJson = """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}"""
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Allowed languages include a language not supported by the function spec.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateProblem_InvalidLanguageMask_IsRejected()
    {
        await using var dbContext = CreateDbContext();
        var ids = SeedUsers(dbContext);
        var service = new ProblemService(dbContext, new TestCurrentUser(ids.OwnerId, UserRole.ProblemSetter));

        var result = await service.CreateProblemAsync(new CreateProblemRequest
        {
            Title = "Invalid Mask",
            Description = "Description",
            InputDescription = "Input",
            OutputDescription = "Output",
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = JudgeMode.StandardInputOutput,
            AllowedLanguagesMask = 0b1000
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Unsupported allowed languages mask.", result.ErrorMessage);
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new OnlineJudgeDbContext(options);
    }

    private static (Guid OwnerId, Guid AnswererId) SeedUsers(OnlineJudgeDbContext dbContext)
    {
        var ownerId = Guid.NewGuid();
        var answererId = Guid.NewGuid();
        dbContext.Users.AddRange(
            User(ownerId, "owner", UserRole.ProblemSetter),
            User(answererId, "answerer", UserRole.Answerer));
        dbContext.SaveChanges();
        return (ownerId, answererId);
    }

    private static (Guid OwnerId, Guid AnswererId, Guid ProblemId) SeedProblem(
        OnlineJudgeDbContext dbContext,
        JudgeMode judgeMode,
        int allowedLanguagesMask,
        string? functionSpecJson = null)
    {
        var users = SeedUsers(dbContext);
        var problemId = Guid.NewGuid();
        dbContext.Problems.Add(new Problem
        {
            Id = problemId,
            Title = "Problem",
            Description = "Description",
            InputDescription = judgeMode == JudgeMode.StandardInputOutput ? "Input" : string.Empty,
            OutputDescription = judgeMode == JudgeMode.StandardInputOutput ? "Output" : string.Empty,
            TimeLimitMs = 1000,
            MemoryLimitMb = 128,
            IsPublished = true,
            JudgeMode = judgeMode,
            AllowedLanguagesMask = allowedLanguagesMask,
            FunctionSpecJson = judgeMode == JudgeMode.Function ? functionSpecJson : null,
            StarterCodeJson = judgeMode == JudgeMode.Function
                ? """{"cpp17":"class Solution {};","csharp":"public class Solution {}","c11":"int solve(){return 0;}"}"""
                : null,
            CreatedByUserId = users.OwnerId,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        });
        dbContext.SaveChanges();
        return (users.OwnerId, users.AnswererId, problemId);
    }

    private static TestCase TestCase(Guid problemId, TestCaseVisibility visibility, int score, string value)
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            ProblemId = problemId,
            Input = value,
            ExpectedOutput = value,
            Visibility = visibility,
            Score = score,
            CreatedAt = BaseTime.AddTicks(score)
        };
    }

    private static SubmissionService CreateSubmissionService(OnlineJudgeDbContext dbContext, Guid answererId)
    {
        return new SubmissionService(dbContext, new NoopJudgeQueue(), new TestCurrentUser(answererId, UserRole.Answerer));
    }

    private static User User(Guid id, string userName, UserRole role)
    {
        return new User
        {
            Id = id,
            UserName = userName,
            Email = $"{userName}@example.test",
            PasswordHash = "hash",
            Role = role,
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };
    }

    private sealed class TestCurrentUser(Guid? userId, UserRole? role, bool isAuthenticated = true) : ICurrentUser
    {
        public bool IsAuthenticated => isAuthenticated;
        public Guid? UserId => userId;
        public string? UserName => "test-user";
        public UserRole? Role => role;
    }

    private sealed class NoopJudgeQueue : IJudgeQueue
    {
        public Task EnqueueSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
'@

git diff --check
if ($LASTEXITCODE -ne 0) {
    throw "git diff --check failed. Review the generated diff before continuing."
}

Write-Host ""
Write-Host "========================================"
Write-Host "PROBLEM-METADATA-UX-01 APPLIED"
Write-Host "========================================"
Write-Host "Base       : $Head"
Write-Host "Backup     : $BackupRoot"
Write-Host "Migration  : $MigrationPath"
Write-Host "Tests      : $TestPath"
Write-Host ""
Write-Host "Implemented:"
Write-Host "  1. Monaco Ctrl + mouse-wheel zoom"
Write-Host "  2. Per-problem language restriction + detail language tags"
Write-Host "  3. Backend language enforcement + FunctionSpec supportedLanguages enforcement"
Write-Host "  4. Problem total score = sum of all Sample + Hidden TestCase.Score"
Write-Host "  5. Hidden test case contents remain filtered for ordinary users"
Write-Host ""
Write-Host "Next validation:"
Write-Host "  dotnet build OnlineJudge.sln"
Write-Host "  dotnet test OnlineJudge.sln --no-build"
Write-Host "  cd frontend"
Write-Host "  npm run build"
Write-Host "========================================"
