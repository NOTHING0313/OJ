param(
    [string]$ApiBaseUrl = "http://localhost:5101",
    [string]$RootAccount = "UnrealStudio",
    [string]$RootPassword = "UnrealStudio",
    [string]$DemoPassword = "123456",
    [switch]$SkipUsers,
    [switch]$SkipSubmissions,
    [switch]$SkipFileUploadDemo,
    [switch]$InteractiveEmailCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Keep these values in sync with OnlineJudge.Domain.Enums.
$LanguageCpp17 = 1
$LanguageC11 = 2
$JudgeModeStandardInputOutput = 1
$JudgeModeFunction = 2
$VisibilitySample = 1
$VisibilityHidden = 2
$RoleProblemSetter = 2
$RoleRoot = 3
$TaskTypeAlgorithm = 1
$TaskTypeFileUpload = 2
$DifficultyKnight = 2
$DifficultyBishop = 3
$DifficultyRook = 4

$StatusNames = @{
    1 = "Pending"
    2 = "Judging"
    3 = "Accepted"
    4 = "WrongAnswer"
    5 = "TimeLimitExceeded"
    6 = "MemoryLimitExceeded"
    7 = "RuntimeError"
    8 = "CompileError"
    9 = "SystemError"
}

$TerminalStatuses = @(
    "Accepted",
    "WrongAnswer",
    "TimeLimitExceeded",
    "MemoryLimitExceeded",
    "RuntimeError",
    "CompileError",
    "SystemError"
)

$script:RootToken = $null
$script:Created = 0
$script:Reused = 0
$script:Warnings = 0

function Write-Info {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "OK $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    $script:Warnings++
    Write-Host "WARN $Message" -ForegroundColor Yellow
}

function Get-PropertyValue {
    param(
        [object]$Object,
        [string[]]$Names
    )

    if ($null -eq $Object) {
        return $null
    }

    foreach ($property in $Object.PSObject.Properties) {
        foreach ($name in $Names) {
            if ($property.Name -ieq $name) {
                return $property.Value
            }
        }
    }

    return $null
}

function ConvertFrom-JsonOrString {
    param([string]$Content)

    if ([string]::IsNullOrWhiteSpace($Content)) {
        return $null
    }

    try {
        return $Content | ConvertFrom-Json
    }
    catch {
        return $Content
    }
}

function ConvertTo-CompactJson {
    param([object]$Value)
    return ($Value | ConvertTo-Json -Depth 80 -Compress)
}

function Normalize-JsonText {
    param([string]$Json)

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return ""
    }

    try {
        return ConvertTo-CompactJson -Value ($Json | ConvertFrom-Json)
    }
    catch {
        return $Json.Trim()
    }
}

function Read-ErrorResponseContent {
    param([object]$Exception)

    if ($Exception.ErrorDetails -and $Exception.ErrorDetails.Message) {
        return $Exception.ErrorDetails.Message
    }

    $response = $Exception.Exception.Response
    if ($null -eq $response) {
        return $Exception.Exception.Message
    }

    try {
        $stream = $response.GetResponseStream()
        if ($null -eq $stream) {
            return $Exception.Exception.Message
        }

        $reader = New-Object System.IO.StreamReader($stream)
        return $reader.ReadToEnd()
    }
    catch {
        return $Exception.Exception.Message
    }
}

function Get-ErrorMessage {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    if ($Value -is [string]) {
        return $Value
    }

    $message = Get-PropertyValue -Object $Value -Names @("message", "Message", "error", "Error", "errorMessage", "ErrorMessage")
    if ($message) {
        return [string]$message
    }

    return ConvertTo-CompactJson -Value $Value
}

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null,
        [string]$Token = $script:RootToken
    )

    $baseUrl = $ApiBaseUrl.TrimEnd("/")
    $relativePath = $Path.TrimStart("/")
    $uri = "$baseUrl/$relativePath"
    $headers = @{}
    if (-not [string]::IsNullOrWhiteSpace($Token)) {
        $headers["Authorization"] = "Bearer $Token"
    }

    $jsonBody = $null
    if ($null -ne $Body) {
        $jsonBody = $Body | ConvertTo-Json -Depth 80
    }

    try {
        $parameters = @{
            Method = $Method
            Uri = $uri
            Headers = $headers
            UseBasicParsing = $true
            ErrorAction = "Stop"
        }

        if ($null -ne $jsonBody) {
            $parameters["Body"] = $jsonBody
            $parameters["ContentType"] = "application/json; charset=utf-8"
        }

        $response = Invoke-WebRequest @parameters
        $raw = [string]$response.Content
        return [pscustomobject]@{
            Ok = $true
            StatusCode = [int]$response.StatusCode
            Body = ConvertFrom-JsonOrString -Content $raw
            RawBody = $raw
            ErrorMessage = ""
        }
    }
    catch {
        $statusCode = 0
        if ($_.Exception.Response) {
            try {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
            catch {
                $statusCode = 0
            }
        }

        $raw = Read-ErrorResponseContent -Exception $_
        $parsed = ConvertFrom-JsonOrString -Content $raw
        return [pscustomobject]@{
            Ok = $false
            StatusCode = $statusCode
            Body = $parsed
            RawBody = $raw
            ErrorMessage = Get-ErrorMessage -Value $parsed
        }
    }
}

function Require-Ok {
    param(
        [object]$Response,
        [string]$Action
    )

    if (-not $Response.Ok) {
        throw "$Action failed. HTTP $($Response.StatusCode): $($Response.ErrorMessage)"
    }

    return $Response.Body
}

function Login {
    param(
        [string]$Account,
        [string]$Password
    )

    $response = Invoke-ApiJson -Method "POST" -Path "/api/auth/login" -Body @{
        account = $Account
        password = $Password
    } -Token $null

    $body = Require-Ok -Response $response -Action "Login $Account"
    $token = Get-PropertyValue -Object $body -Names @("accessToken", "AccessToken")
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "Login $Account succeeded but accessToken was not returned."
    }

    return $body
}

function Convert-StatusName {
    param([object]$Status)

    if ($null -eq $Status) {
        return ""
    }

    if ($Status -is [int] -or $Status -is [long]) {
        return $StatusNames[[int]$Status]
    }

    $text = [string]$Status
    $number = 0
    if ([int]::TryParse($text, [ref]$number)) {
        return $StatusNames[$number]
    }

    return $text
}

function Get-Users {
    param([string]$Keyword = "")

    $path = "/api/users?page=1&pageSize=100"
    if (-not [string]::IsNullOrWhiteSpace($Keyword)) {
        $path = "$path&keyword=$([uri]::EscapeDataString($Keyword))"
    }

    $response = Invoke-ApiJson -Method "GET" -Path $path
    $body = Require-Ok -Response $response -Action "Get users"
    $items = Get-PropertyValue -Object $body -Names @("items", "Items")
    if ($null -eq $items) {
        return @()
    }

    return @($items)
}

function Find-UserByName {
    param([string]$UserName)

    $users = Get-Users -Keyword $UserName
    foreach ($user in $users) {
        $name = Get-PropertyValue -Object $user -Names @("userName", "UserName")
        if ($name -eq $UserName) {
            return $user
        }
    }

    return $null
}

function Ensure-DemoUser {
    param(
        [string]$UserName,
        [string]$Email,
        [switch]$AsProblemSetter
    )

    $existing = Find-UserByName -UserName $UserName
    if ($null -ne $existing) {
        $script:Reused++
        Write-Ok "User exists: $UserName"
        $user = $existing
    }
    else {
        Write-Info "Create user: $UserName"
        $sendResponse = Invoke-ApiJson -Method "POST" -Path "/api/auth/register/send-code" -Body @{ email = $Email } -Token $null
        $sendBody = Require-Ok -Response $sendResponse -Action "Send register code for $Email"
        $code = Get-PropertyValue -Object $sendBody -Names @("debugCode", "DebugCode")

        if ([string]::IsNullOrWhiteSpace($code)) {
            if (-not $InteractiveEmailCode) {
                throw "Register code for $Email was not returned. Re-run with -InteractiveEmailCode and enter the email code manually."
            }

            $code = Read-Host "Enter email code for $Email"
        }

        $registerResponse = Invoke-ApiJson -Method "POST" -Path "/api/auth/register" -Body @{
            userName = $UserName
            email = $Email
            password = $DemoPassword
            emailCode = $code
        } -Token $null
        Require-Ok -Response $registerResponse -Action "Register $UserName" | Out-Null

        Start-Sleep -Milliseconds 250
        $user = Find-UserByName -UserName $UserName
        if ($null -eq $user) {
            throw "User $UserName was created but could not be found from /api/users."
        }

        $script:Created++
        Write-Ok "Created user: $UserName"
    }

    if ($AsProblemSetter) {
        $role = Get-PropertyValue -Object $user -Names @("role", "Role")
        if ([int]$role -ne $RoleProblemSetter -and [int]$role -ne $RoleRoot) {
            $id = Get-PropertyValue -Object $user -Names @("id", "Id")
            $promoteResponse = Invoke-ApiJson -Method "POST" -Path "/api/users/$id/promote-to-problem-setter"
            $user = Require-Ok -Response $promoteResponse -Action "Promote $UserName"
            Write-Ok "Promoted user to ProblemSetter: $UserName"
        }
    }

    return $user
}

function Get-Problems {
    $response = Invoke-ApiJson -Method "GET" -Path "/api/problems"
    $body = Require-Ok -Response $response -Action "Get problems"
    return @($body)
}

function Get-ProblemDetail {
    param([string]$ProblemId)

    $response = Invoke-ApiJson -Method "GET" -Path "/api/problems/$ProblemId"
    return Require-Ok -Response $response -Action "Get problem $ProblemId"
}

function Find-ProblemByTitle {
    param([string]$Title)

    foreach ($problem in (Get-Problems)) {
        $problemTitle = Get-PropertyValue -Object $problem -Names @("title", "Title")
        if ($problemTitle -eq $Title) {
            $id = Get-PropertyValue -Object $problem -Names @("id", "Id")
            return Get-ProblemDetail -ProblemId $id
        }
    }

    return $null
}

function Ensure-Problem {
    param(
        [string]$Title,
        [string]$Description,
        [string]$InputDescription,
        [string]$OutputDescription,
        [int]$JudgeMode,
        [string]$FunctionSpecJson = $null,
        [string]$StarterCodeJson = $null
    )

    $existing = Find-ProblemByTitle -Title $Title
    if ($null -ne $existing) {
        $script:Reused++
        Write-Ok "Problem exists: $Title"
        return $existing
    }

    Write-Info "Create problem: $Title"
    $response = Invoke-ApiJson -Method "POST" -Path "/api/problems" -Body @{
        title = $Title
        description = $Description
        inputDescription = $InputDescription
        outputDescription = $OutputDescription
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $true
        judgeMode = $JudgeMode
        functionSpecJson = $FunctionSpecJson
        starterCodeJson = $StarterCodeJson
    }

    $problem = Require-Ok -Response $response -Action "Create problem $Title"
    $script:Created++
    Write-Ok "Created problem: $Title"
    $id = Get-PropertyValue -Object $problem -Names @("id", "Id")
    return Get-ProblemDetail -ProblemId $id
}

function Test-StandardAbProblemUsable {
    param([object]$Problem)

    $testCases = Get-PropertyValue -Object $Problem -Names @("testCases", "TestCases")
    foreach ($testCase in @($testCases)) {
        $caseInput = [string](Get-PropertyValue -Object $testCase -Names @("input", "Input"))
        $caseExpectedOutput = [string](Get-PropertyValue -Object $testCase -Names @("expectedOutput", "ExpectedOutput"))
        if ([string]::IsNullOrWhiteSpace($caseInput) -and ($caseExpectedOutput -eq "3" -or $caseExpectedOutput -eq "300")) {
            return $false
        }
    }

    return $true
}

function Ensure-StandardAbProblem {
    $title = "[Demo] A + B 标准题"
    $matchingProblems = @()
    foreach ($problem in (Get-Problems)) {
        $problemTitle = Get-PropertyValue -Object $problem -Names @("title", "Title")
        if ($problemTitle -eq $title) {
            $id = Get-PropertyValue -Object $problem -Names @("id", "Id")
            $matchingProblems += (Get-ProblemDetail -ProblemId $id)
        }
    }

    foreach ($problem in $matchingProblems) {
        if (Test-StandardAbProblemUsable -Problem $problem) {
            $script:Reused++
            Write-Ok "Problem exists: $title"
            return $problem
        }
    }

    if ($matchingProblems.Count -gt 0) {
        Write-Warn "Existing [Demo] A + B 标准题 has malformed empty-input demo cases from an older run. Creating a clean same-title demo problem without deleting data."
    }

    Write-Info "Create problem: $title"
    $response = Invoke-ApiJson -Method "POST" -Path "/api/problems" -Body @{
        title = $title
        description = "标准输入两个整数，输出它们的和。"
        inputDescription = "输入两个整数。"
        outputDescription = "输出两个整数之和。"
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $true
        judgeMode = $JudgeModeStandardInputOutput
        functionSpecJson = $null
        starterCodeJson = $null
    }

    $problem = Require-Ok -Response $response -Action "Create problem $title"
    $script:Created++
    Write-Ok "Created problem: $title"
    $id = Get-PropertyValue -Object $problem -Names @("id", "Id")
    return Get-ProblemDetail -ProblemId $id
}

function Test-CaseExists {
    param(
        [object]$Problem,
        [string]$InputText = "",
        [string]$ExpectedOutput = "",
        [string]$ArgumentsJson = $null,
        [string]$ExpectedJson = $null
    )

    $testCases = Get-PropertyValue -Object $Problem -Names @("testCases", "TestCases")
    foreach ($testCase in @($testCases)) {
        $caseInput = [string](Get-PropertyValue -Object $testCase -Names @("input", "Input"))
        $caseExpectedOutput = [string](Get-PropertyValue -Object $testCase -Names @("expectedOutput", "ExpectedOutput"))
        $caseArgumentsJson = [string](Get-PropertyValue -Object $testCase -Names @("argumentsJson", "ArgumentsJson"))
        $caseExpectedJson = [string](Get-PropertyValue -Object $testCase -Names @("expectedJson", "ExpectedJson"))

        if ($caseInput -eq $InputText -and
            $caseExpectedOutput -eq $ExpectedOutput -and
            (Normalize-JsonText -Json $caseArgumentsJson) -eq (Normalize-JsonText -Json $ArgumentsJson) -and
            (Normalize-JsonText -Json $caseExpectedJson) -eq (Normalize-JsonText -Json $ExpectedJson)) {
            return $true
        }
    }

    return $false
}

function Ensure-TestCase {
    param(
        [object]$Problem,
        [string]$InputText = "",
        [string]$ExpectedOutput = "",
        [string]$ArgumentsJson = $null,
        [string]$ExpectedJson = $null,
        [int]$Visibility = $VisibilityHidden,
        [int]$Score = 100
    )

    $problemId = Get-PropertyValue -Object $Problem -Names @("id", "Id")
    if (Test-CaseExists -Problem $Problem -InputText $InputText -ExpectedOutput $ExpectedOutput -ArgumentsJson $ArgumentsJson -ExpectedJson $ExpectedJson) {
        $script:Reused++
        Write-Ok "Test case exists: problem=$problemId visibility=$Visibility"
        return Get-ProblemDetail -ProblemId $problemId
    }

    $response = Invoke-ApiJson -Method "POST" -Path "/api/problems/$problemId/test-cases" -Body @{
        input = $InputText
        expectedOutput = $ExpectedOutput
        argumentsJson = $ArgumentsJson
        expectedJson = $ExpectedJson
        visibility = $Visibility
        score = $Score
    }
    Require-Ok -Response $response -Action "Add test case for problem $problemId" | Out-Null
    $script:Created++
    Write-Ok "Added test case: problem=$problemId visibility=$Visibility"
    return Get-ProblemDetail -ProblemId $problemId
}

function Get-Challenges {
    $response = Invoke-ApiJson -Method "GET" -Path "/api/challenges"
    $body = Require-Ok -Response $response -Action "Get challenges"
    return @($body)
}

function Get-ChallengeDetail {
    param([string]$ChallengeId)

    $response = Invoke-ApiJson -Method "GET" -Path "/api/challenges/$ChallengeId"
    return Require-Ok -Response $response -Action "Get challenge $ChallengeId"
}

function Find-ChallengeByTitle {
    param([string]$Title)

    foreach ($challenge in (Get-Challenges)) {
        $challengeTitle = Get-PropertyValue -Object $challenge -Names @("title", "Title")
        if ($challengeTitle -eq $Title) {
            $id = Get-PropertyValue -Object $challenge -Names @("id", "Id")
            return Get-ChallengeDetail -ChallengeId $id
        }
    }

    return $null
}

function Ensure-Challenge {
    param([string]$Title)

    $existing = Find-ChallengeByTitle -Title $Title
    if ($null -ne $existing) {
        $script:Reused++
        Write-Ok "Challenge exists: $Title"
        return $existing
    }

    Write-Info "Create challenge: $Title"
    $response = Invoke-ApiJson -Method "POST" -Path "/api/challenges" -Body @{
        title = $Title
        description = "演示用棋盘挑战，包含算法任务和文件上传任务。"
        startAt = (Get-Date).AddDays(-1).ToUniversalTime().ToString("o")
        endAt = (Get-Date).AddDays(30).ToUniversalTime().ToString("o")
        isPublished = $true
    }

    $challenge = Require-Ok -Response $response -Action "Create challenge $Title"
    $script:Created++
    Write-Ok "Created challenge: $Title"
    $id = Get-PropertyValue -Object $challenge -Names @("id", "Id")
    return Get-ChallengeDetail -ChallengeId $id
}

function Ensure-ChallengeTask {
    param(
        [object]$Challenge,
        [string]$Title,
        [string]$Description,
        [int]$TaskType,
        [int]$Difficulty,
        [int]$BoardX,
        [int]$BoardY,
        [string]$AlgorithmProblemId = $null,
        [int]$Score = 20
    )

    $challengeId = Get-PropertyValue -Object $Challenge -Names @("id", "Id")
    $tasks = Get-PropertyValue -Object $Challenge -Names @("tasks", "Tasks")
    foreach ($task in @($tasks)) {
        $taskTitle = Get-PropertyValue -Object $task -Names @("title", "Title")
        if ($taskTitle -eq $Title) {
            $script:Reused++
            Write-Ok "Challenge task exists: $Title"
            return Get-ChallengeDetail -ChallengeId $challengeId
        }
    }

    $algorithmProblemValue = $null
    if (-not [string]::IsNullOrWhiteSpace($AlgorithmProblemId)) {
        $algorithmProblemValue = $AlgorithmProblemId
    }

    $response = Invoke-ApiJson -Method "POST" -Path "/api/challenges/$challengeId/tasks" -Body @{
        title = $Title
        description = $Description
        taskType = $TaskType
        difficulty = $Difficulty
        boardX = $BoardX
        boardY = $BoardY
        algorithmProblemId = $algorithmProblemValue
        score = $Score
        isPublished = $true
    }
    Require-Ok -Response $response -Action "Create challenge task $Title" | Out-Null
    $script:Created++
    Write-Ok "Created challenge task: $Title"
    return Get-ChallengeDetail -ChallengeId $challengeId
}

function Submit-Code {
    param(
        [string]$Token,
        [string]$ProblemId,
        [int]$Language,
        [string]$SourceCode
    )

    $response = Invoke-ApiJson -Method "POST" -Path "/api/submissions" -Body @{
        problemId = $ProblemId
        language = $Language
        sourceCode = $SourceCode
    } -Token $Token

    return Require-Ok -Response $response -Action "Create submission"
}

function Wait-Submission {
    param(
        [string]$Token,
        [string]$SubmissionId,
        [int]$TimeoutSeconds = 60
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastSubmission = $null
    while ((Get-Date) -lt $deadline) {
        $response = Invoke-ApiJson -Method "GET" -Path "/api/submissions/$SubmissionId" -Token $Token
        $lastSubmission = Require-Ok -Response $response -Action "Get submission $SubmissionId"
        $status = Convert-StatusName -Status (Get-PropertyValue -Object $lastSubmission -Names @("status", "Status"))
        if ($TerminalStatuses -contains $status) {
            return $lastSubmission
        }

        Start-Sleep -Seconds 1
    }

    $lastStatus = Convert-StatusName -Status (Get-PropertyValue -Object $lastSubmission -Names @("status", "Status"))
    throw "Submission timeout. submission=$SubmissionId lastStatus=$lastStatus"
}

function Add-DemoSubmission {
    param(
        [string]$Token,
        [string]$Name,
        [string]$ProblemId,
        [int]$Language,
        [string]$SourceCode
    )

    Write-Info "Submit demo code: $Name"
    $submission = Submit-Code -Token $Token -ProblemId $ProblemId -Language $Language -SourceCode $SourceCode
    $submissionId = Get-PropertyValue -Object $submission -Names @("id", "Id")
    $finished = Wait-Submission -Token $Token -SubmissionId $submissionId
    $status = Convert-StatusName -Status (Get-PropertyValue -Object $finished -Names @("status", "Status"))
    Write-Ok "$Name status=$status submission=$submissionId"
}

function Write-FileUploadManualSteps {
    param(
        [object]$Challenge,
        [object]$Task
    )

    $challengeId = Get-PropertyValue -Object $Challenge -Names @("id", "Id")
    $taskId = Get-PropertyValue -Object $Task -Names @("id", "Id")
    Write-Warn "File upload demo is left as a manual step in this PowerShell 5.1 script."
    Write-Host "Manual file demo:"
    Write-Host "1. Login as demo_answerer / $DemoPassword."
    Write-Host "2. Open Challenge [Demo] 棋盘挑战 and join it."
    Write-Host "3. Upload a small ZIP to task id $taskId in challenge id $challengeId."
    Write-Host "4. Login as Root and review the file submission with score 8 and comment: 演示评分：结构完整，内容清晰。"
}

Write-Info "[1/7] Login as Root..."
$rootLogin = Login -Account $RootAccount -Password $RootPassword
$script:RootToken = Get-PropertyValue -Object $rootLogin -Names @("accessToken", "AccessToken")
Write-Ok "Root login succeeded"

$demoAnswerer = $null
$demoSetter = $null
$demoViewer = $null
if (-not $SkipUsers) {
    Write-Info "[2/7] Ensure demo users..."
    $demoAnswerer = Ensure-DemoUser -UserName "demo_answerer" -Email "demo_answerer@example.local"
    $demoSetter = Ensure-DemoUser -UserName "demo_setter" -Email "demo_setter@example.local" -AsProblemSetter
    $demoViewer = Ensure-DemoUser -UserName "demo_viewer" -Email "demo_viewer@example.local"
}
else {
    Write-Warn "SkipUsers was set. Demo users and user-dependent submissions are skipped."
}

Write-Info "[3/7] Ensure demo problems..."
$abProblem = Ensure-StandardAbProblem

$twoSumSpec = @'
{"functionName":"twoSum","returnType":"int[]","parameters":[{"name":"nums","type":"int[]"},{"name":"target","type":"int"}],"supportedLanguages":["cpp17","csharp","c11"]}
'@
$twoSumStarter = @{
    cpp17 = "class Solution {`npublic:`n    vector<int> twoSum(vector<int>& nums, int target) {`n        `n    }`n};"
    csharp = "public class Solution`n{`n    public int[] TwoSum(int[] nums, int target)`n    {`n        `n    }`n}"
    c11 = "int* twoSum(int* nums, int numsSize, int target, int* returnSize) {`n    `n}"
} | ConvertTo-Json -Compress
$twoSumProblem = Ensure-Problem `
    -Title "[Demo] Two Sum 函数式题" `
    -Description "函数式 Two Sum 演示题，只需要实现目标函数。" `
    -InputDescription "函数式题目无需标准输入。" `
    -OutputDescription "函数式题目无需标准输出。" `
    -JudgeMode $JudgeModeFunction `
    -FunctionSpecJson $twoSumSpec `
    -StarterCodeJson $twoSumStarter

$reverseListSpec = @'
{"functionName":"reverseList","returnType":"ListNode<int>","parameters":[{"name":"head","type":"ListNode<int>"}],"supportedLanguages":["cpp17","csharp"]}
'@
$reverseListStarter = @{
    cpp17 = "struct ListNode {`n    int val;`n    ListNode* next;`n`n    ListNode() : val(0), next(nullptr) {}`n    ListNode(int x) : val(x), next(nullptr) {}`n    ListNode(int x, ListNode* next) : val(x), next(next) {}`n};`n`nclass Solution {`npublic:`n    ListNode* reverseList(ListNode* head) {`n        `n    }`n};"
    csharp = "public class ListNode`n{`n    public int val;`n    public ListNode? next;`n`n    public ListNode(int val = 0, ListNode? next = null)`n    {`n        this.val = val;`n        this.next = next;`n    }`n}`n`npublic class Solution`n{`n    public ListNode? ReverseList(ListNode? head)`n    {`n        `n    }`n}"
} | ConvertTo-Json -Compress
$reverseListProblem = Ensure-Problem `
    -Title "[Demo] Reverse List 链表题" `
    -Description "链表使用 JSON 数组表示，例如 [1,2,3]。" `
    -InputDescription "函数式题目无需标准输入。" `
    -OutputDescription "链表返回值同样使用数组表示。" `
    -JudgeMode $JudgeModeFunction `
    -FunctionSpecJson $reverseListSpec `
    -StarterCodeJson $reverseListStarter

$invertTreeSpec = @'
{"functionName":"invertTree","returnType":"TreeNode<int>","parameters":[{"name":"root","type":"TreeNode<int>"}],"supportedLanguages":["cpp17","csharp"]}
'@
$invertTreeStarter = @{
    cpp17 = "struct TreeNode {`n    int val;`n    TreeNode* left;`n    TreeNode* right;`n`n    TreeNode() : val(0), left(nullptr), right(nullptr) {}`n    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}`n    TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}`n};`n`nclass Solution {`npublic:`n    TreeNode* invertTree(TreeNode* root) {`n        `n    }`n};"
    csharp = "public class TreeNode`n{`n    public int val;`n    public TreeNode? left;`n    public TreeNode? right;`n`n    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)`n    {`n        this.val = val;`n        this.left = left;`n        this.right = right;`n    }`n}`n`npublic class Solution`n{`n    public TreeNode? InvertTree(TreeNode? root)`n    {`n        `n    }`n}"
} | ConvertTo-Json -Compress
$invertTreeProblem = Ensure-Problem `
    -Title "[Demo] Invert Tree 二叉树题" `
    -Description "二叉树使用 LeetCode 风格层序数组表示，例如 [1,2,3,null,4]。" `
    -InputDescription "函数式题目无需标准输入。" `
    -OutputDescription "二叉树返回值同样使用层序数组表示。" `
    -JudgeMode $JudgeModeFunction `
    -FunctionSpecJson $invertTreeSpec `
    -StarterCodeJson $invertTreeStarter

Write-Info "[4/7] Ensure demo test cases..."
$abProblem = Ensure-TestCase -Problem $abProblem -InputText "1 2" -ExpectedOutput "3" -Visibility $VisibilitySample -Score 50
$abProblem = Ensure-TestCase -Problem $abProblem -InputText "100 200" -ExpectedOutput "300" -Visibility $VisibilityHidden -Score 50

$twoSumProblem = Ensure-TestCase -Problem $twoSumProblem -ArgumentsJson '{"nums":[2,7,11,15],"target":9}' -ExpectedJson '[0,1]' -Visibility $VisibilitySample -Score 50
$twoSumProblem = Ensure-TestCase -Problem $twoSumProblem -ArgumentsJson '{"nums":[3,2,4],"target":6}' -ExpectedJson '[1,2]' -Visibility $VisibilityHidden -Score 50

$reverseListProblem = Ensure-TestCase -Problem $reverseListProblem -ArgumentsJson '{"head":[1,2,3]}' -ExpectedJson '[3,2,1]' -Visibility $VisibilitySample -Score 50
$reverseListProblem = Ensure-TestCase -Problem $reverseListProblem -ArgumentsJson '{"head":[]}' -ExpectedJson '[]' -Visibility $VisibilityHidden -Score 50

$invertTreeProblem = Ensure-TestCase -Problem $invertTreeProblem -ArgumentsJson '{"root":[4,2,7,1,3,6,9]}' -ExpectedJson '[4,7,2,9,6,3,1]' -Visibility $VisibilitySample -Score 50
$invertTreeProblem = Ensure-TestCase -Problem $invertTreeProblem -ArgumentsJson '{"root":[1,2,3,null,4]}' -ExpectedJson '[1,3,2,null,null,4]' -Visibility $VisibilityHidden -Score 50

Write-Info "[5/7] Ensure demo challenge..."
$challenge = Ensure-Challenge -Title "[Demo] 棋盘挑战"
$twoSumProblemId = Get-PropertyValue -Object $twoSumProblem -Names @("id", "Id")
$challenge = Ensure-ChallengeTask -Challenge $challenge -Title "[Demo] Two Sum 算法任务" -Description "完成 Two Sum 函数式算法题。" -TaskType $TaskTypeAlgorithm -Difficulty $DifficultyKnight -BoardX 0 -BoardY 0 -AlgorithmProblemId $twoSumProblemId -Score 20
$challenge = Ensure-ChallengeTask -Challenge $challenge -Title "[Demo] ZIP 文件题" -Description "上传一个 ZIP 文件作为演示答案。" -TaskType $TaskTypeFileUpload -Difficulty $DifficultyBishop -BoardX 1 -BoardY 0 -Score 30
$challenge = Ensure-ChallengeTask -Challenge $challenge -Title "[Demo] 实验报告上传题" -Description "上传实验报告 ZIP 作为演示答案。" -TaskType $TaskTypeFileUpload -Difficulty $DifficultyRook -BoardX 2 -BoardY 1 -Score 50

if (-not $SkipSubmissions -and $null -ne $demoAnswerer) {
    Write-Info "[6/7] Create demo submissions..."
    $answererLogin = Login -Account "demo_answerer" -Password $DemoPassword
    $answererToken = Get-PropertyValue -Object $answererLogin -Names @("accessToken", "AccessToken")
    $abProblemId = Get-PropertyValue -Object $abProblem -Names @("id", "Id")
    $twoSumProblemId = Get-PropertyValue -Object $twoSumProblem -Names @("id", "Id")

    $abC11Accepted = @'
#include <stdio.h>

int main(void) {
    int a, b;
    scanf("%d%d", &a, &b);
    printf("%d", a + b);
    return 0;
}
'@
    $twoSumCppAccepted = @'
class Solution {
public:
    vector<int> twoSum(vector<int>& nums, int target) {
        for (int i = 0; i < (int)nums.size(); i++) {
            for (int j = i + 1; j < (int)nums.size(); j++) {
                if (nums[i] + nums[j] == target) {
                    return {i, j};
                }
            }
        }
        return {};
    }
};
'@
    $twoSumCppWrong = @'
class Solution {
public:
    vector<int> twoSum(vector<int>& nums, int target) {
        return {0, 0};
    }
};
'@
    $cppCompileError = @'
#include <bits/stdc++.h>
int main() {
    return broken_symbol;
}
'@

    Add-DemoSubmission -Token $answererToken -Name "A+B C11 Accepted" -ProblemId $abProblemId -Language $LanguageC11 -SourceCode $abC11Accepted
    Add-DemoSubmission -Token $answererToken -Name "Two Sum C++17 Accepted" -ProblemId $twoSumProblemId -Language $LanguageCpp17 -SourceCode $twoSumCppAccepted
    Add-DemoSubmission -Token $answererToken -Name "Two Sum C++17 WrongAnswer" -ProblemId $twoSumProblemId -Language $LanguageCpp17 -SourceCode $twoSumCppWrong
    Add-DemoSubmission -Token $answererToken -Name "A+B C++17 CompileError" -ProblemId $abProblemId -Language $LanguageCpp17 -SourceCode $cppCompileError
}
elseif ($SkipSubmissions) {
    Write-Warn "SkipSubmissions was set. Demo submissions are skipped."
}
else {
    Write-Warn "Demo answerer is unavailable. Demo submissions are skipped."
}

Write-Info "[7/7] File upload demo guidance..."
if ($SkipFileUploadDemo) {
    Write-Warn "SkipFileUploadDemo was set. File upload scoring demo is skipped."
}
else {
    $tasks = Get-PropertyValue -Object $challenge -Names @("tasks", "Tasks")
    $zipTask = $null
    foreach ($task in @($tasks)) {
        if ((Get-PropertyValue -Object $task -Names @("title", "Title")) -eq "[Demo] ZIP 文件题") {
            $zipTask = $task
            break
        }
    }

    Write-FileUploadManualSteps -Challenge $challenge -Task $zipTask
}

Write-Host ""
Write-Host "Demo data seed result:" -ForegroundColor Cyan
Write-Host "Created: $script:Created"
Write-Host "Reused:  $script:Reused"
Write-Host "Warnings: $script:Warnings"
Write-Host "Completed. No database was cleared and no tokens were written to disk." -ForegroundColor Green
