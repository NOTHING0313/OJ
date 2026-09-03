param(
    [string]$ApiBaseUrl = "http://localhost:5101",
    [string]$Account = "UnrealStudio",
    [string]$Password = "UnrealStudio",
    [int]$TimeoutSeconds = 60,
    [int]$PollIntervalSeconds = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Keep these values in sync with OnlineJudge.Domain.Enums.JudgeLanguage.
$LanguageCpp17 = 1
$LanguageC11 = 2
$LanguageCSharp = 3

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

$script:Passed = 0
$script:Failed = 0
$script:AccessToken = $null

function Write-Step {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Add-Pass {
    param([string]$Name)
    $script:Passed++
    Write-Host "PASS $Name" -ForegroundColor Green
}

function Add-Fail {
    param(
        [string]$Name,
        [string]$Details = ""
    )

    $script:Failed++
    Write-Host "FAILED $Name" -ForegroundColor Red
    if (-not [string]::IsNullOrWhiteSpace($Details)) {
        Write-Host $Details
    }
}

function Stop-E2E {
    param(
        [string]$Name,
        [string]$Details = ""
    )

    Add-Fail -Name $Name -Details $Details
    Write-Summary
    exit 1
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
    return ($Value | ConvertTo-Json -Depth 50 -Compress)
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

function Get-ErrorMessage {
    param([object]$Value)

    if ($null -eq $Value) {
        return ""
    }

    if ($Value -is [string]) {
        return $Value
    }

    $title = Get-PropertyValue -Object $Value -Names @("title", "Title")
    $message = Get-PropertyValue -Object $Value -Names @("message", "Message", "error", "Error", "errorMessage", "ErrorMessage")
    if ($message) {
        return [string]$message
    }

    if ($title) {
        return [string]$title
    }

    return ConvertTo-CompactJson -Value $Value
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

function Invoke-ApiJson {
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [object]$Body = $null,
        [string]$Token = $null
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
        $jsonBody = $Body | ConvertTo-Json -Depth 50
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
        $parsed = ConvertFrom-JsonOrString -Content $raw
        return [pscustomobject]@{
            Ok = $true
            StatusCode = [int]$response.StatusCode
            Body = $parsed
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

function Login {
    Write-Step "[1/10] Login..."
    $response = Invoke-ApiJson -Method "POST" -Path "/api/auth/login" -Body @{
        account = $Account
        password = $Password
    }

    if (-not $response.Ok) {
        Stop-E2E -Name "Login failed" -Details "HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }

    $token = Get-PropertyValue -Object $response.Body -Names @("accessToken", "AccessToken")
    if ([string]::IsNullOrWhiteSpace($token)) {
        Stop-E2E -Name "Login failed" -Details "AccessToken was not returned."
    }

    $script:AccessToken = $token
}

function Get-Problems {
    $response = Invoke-ApiJson -Method "GET" -Path "/api/problems" -Token $script:AccessToken
    if (-not $response.Ok) {
        Stop-E2E -Name "Get problems failed" -Details "HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }

    return @($response.Body)
}

function Get-ProblemDetail {
    param([string]$ProblemId)

    $response = Invoke-ApiJson -Method "GET" -Path "/api/problems/$ProblemId" -Token $script:AccessToken
    if (-not $response.Ok) {
        Stop-E2E -Name "Get problem detail failed" -Details "problemId=$ProblemId HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }

    return $response.Body
}

function Ensure-Problem {
    param(
        [string]$Title,
        [hashtable]$CreateBody
    )

    Write-Host "Ensure problem: $Title"
    $problems = Get-Problems
    $matches = @($problems | Where-Object { (Get-PropertyValue -Object $_ -Names @("title", "Title")) -eq $Title })

    if ($matches.Count -gt 1) {
        Write-Host "WARNING multiple problems found with title '$Title'; using the first one." -ForegroundColor Yellow
    }

    if ($matches.Count -gt 0) {
        $id = Get-PropertyValue -Object $matches[0] -Names @("id", "Id")
        return Get-ProblemDetail -ProblemId $id
    }

    $response = Invoke-ApiJson -Method "POST" -Path "/api/problems" -Body $CreateBody -Token $script:AccessToken
    if (-not $response.Ok) {
        Stop-E2E -Name "Create problem failed: $Title" -Details "HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }

    $createdId = Get-PropertyValue -Object $response.Body -Names @("id", "Id")
    if ([string]::IsNullOrWhiteSpace($createdId)) {
        Stop-E2E -Name "Create problem failed: $Title" -Details "Problem id was not returned."
    }

    return Get-ProblemDetail -ProblemId $createdId
}

function Ensure-PublishedProblem {
    param(
        [object]$Problem,
        [hashtable]$UpdateBody
    )

    $problemId = Get-PropertyValue -Object $Problem -Names @("id", "Id")
    $isPublished = Get-PropertyValue -Object $Problem -Names @("isPublished", "IsPublished")
    if ($isPublished -eq $true) {
        return Get-ProblemDetail -ProblemId $problemId
    }

    $UpdateBody.isPublished = $true
    $response = Invoke-ApiJson -Method "PUT" -Path "/api/problems/$problemId" -Body $UpdateBody -Token $script:AccessToken
    if (-not $response.Ok) {
        Stop-E2E -Name "Publish problem failed" -Details "problemId=$problemId HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }

    return Get-ProblemDetail -ProblemId $problemId
}

function Get-TestCases {
    param([object]$Problem)

    $testCases = Get-PropertyValue -Object $Problem -Names @("testCases", "TestCases")
    if ($null -eq $testCases) {
        return @()
    }

    return @($testCases)
}

function Test-FunctionCaseExists {
    param(
        [object]$Problem,
        [string]$ArgumentsJson,
        [string]$ExpectedJson
    )

    $arguments = Normalize-JsonText -Json $ArgumentsJson
    $expected = Normalize-JsonText -Json $ExpectedJson

    foreach ($testCase in Get-TestCases -Problem $Problem) {
        $existingArguments = Normalize-JsonText -Json ([string](Get-PropertyValue -Object $testCase -Names @("argumentsJson", "ArgumentsJson")))
        $existingExpected = Normalize-JsonText -Json ([string](Get-PropertyValue -Object $testCase -Names @("expectedJson", "ExpectedJson")))
        if ($existingArguments -eq $arguments -and $existingExpected -eq $expected) {
            return $true
        }
    }

    return $false
}

function Test-StandardCaseExists {
    param(
        [object]$Problem,
        [string]$InputText,
        [string]$ExpectedOutput
    )

    foreach ($testCase in Get-TestCases -Problem $Problem) {
        $existingInput = [string](Get-PropertyValue -Object $testCase -Names @("input", "Input"))
        $existingExpected = [string](Get-PropertyValue -Object $testCase -Names @("expectedOutput", "ExpectedOutput"))
        if ($existingInput.Trim() -eq $InputText.Trim() -and $existingExpected.Trim() -eq $ExpectedOutput.Trim()) {
            return $true
        }
    }

    return $false
}

function Add-TestCase {
    param(
        [string]$ProblemId,
        [hashtable]$Body
    )

    $response = Invoke-ApiJson -Method "POST" -Path "/api/problems/$ProblemId/test-cases" -Body $Body -Token $script:AccessToken
    if (-not $response.Ok) {
        Stop-E2E -Name "Add test case failed" -Details "problemId=$ProblemId HTTP $($response.StatusCode) $($response.ErrorMessage)"
    }
}

function Ensure-FunctionTestCase {
    param(
        [object]$Problem,
        [string]$ArgumentsJson,
        [string]$ExpectedJson,
        [int]$Score
    )

    if (Test-FunctionCaseExists -Problem $Problem -ArgumentsJson $ArgumentsJson -ExpectedJson $ExpectedJson) {
        return
    }

    $problemId = Get-PropertyValue -Object $Problem -Names @("id", "Id")
    Add-TestCase -ProblemId $problemId -Body @{
        input = ""
        expectedOutput = ""
        argumentsJson = $ArgumentsJson
        expectedJson = $ExpectedJson
        visibility = 1
        score = $Score
    }
}

function Ensure-StandardTestCase {
    param(
        [object]$Problem,
        [string]$InputText,
        [string]$ExpectedOutput,
        [int]$Score
    )

    if (Test-StandardCaseExists -Problem $Problem -InputText $InputText -ExpectedOutput $ExpectedOutput) {
        return
    }

    $problemId = Get-PropertyValue -Object $Problem -Names @("id", "Id")
    Add-TestCase -ProblemId $problemId -Body @{
        input = $InputText
        expectedOutput = $ExpectedOutput
        argumentsJson = $null
        expectedJson = $null
        visibility = 1
        score = $Score
    }
}

function Convert-StatusName {
    param([object]$Status)

    if ($null -eq $Status) {
        return ""
    }

    $text = [string]$Status
    $number = 0
    if ([int]::TryParse($text, [ref]$number) -and $StatusNames.ContainsKey($number)) {
        return $StatusNames[$number]
    }

    return $text
}

function Format-CaseResults {
    param([object]$Submission)

    $caseResults = Get-PropertyValue -Object $Submission -Names @("caseResults", "CaseResults")
    if ($null -eq $caseResults) {
        return ""
    }

    $parts = @()
    foreach ($case in @($caseResults)) {
        $testCaseId = Get-PropertyValue -Object $case -Names @("testCaseId", "TestCaseId")
        $status = Convert-StatusName -Status (Get-PropertyValue -Object $case -Names @("status", "Status"))
        $error = Get-PropertyValue -Object $case -Names @("errorMessage", "ErrorMessage")
        $parts += "case=$testCaseId status=$status error=$error"
    }

    return ($parts -join "; ")
}

function Submit-Code {
    param(
        [string]$ProblemId,
        [int]$Language,
        [string]$SourceCode
    )

    return Invoke-ApiJson -Method "POST" -Path "/api/submissions" -Body @{
        problemId = $ProblemId
        language = $Language
        sourceCode = $SourceCode
    } -Token $script:AccessToken
}

function Wait-Submission {
    param([string]$SubmissionId)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastSubmission = $null
    while ((Get-Date) -lt $deadline) {
        $response = Invoke-ApiJson -Method "GET" -Path "/api/submissions/$SubmissionId" -Token $script:AccessToken
        if (-not $response.Ok) {
            Stop-E2E -Name "Get submission failed" -Details "submissionId=$SubmissionId HTTP $($response.StatusCode) $($response.ErrorMessage)"
        }

        $lastSubmission = $response.Body
        $status = Convert-StatusName -Status (Get-PropertyValue -Object $lastSubmission -Names @("status", "Status"))
        if ($TerminalStatuses -contains $status) {
            return $lastSubmission
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    $lastStatus = Convert-StatusName -Status (Get-PropertyValue -Object $lastSubmission -Names @("status", "Status"))
    Add-Fail -Name "Submission timeout" -Details "SubmissionId: $SubmissionId`nLastStatus: $lastStatus"
    return $null
}

function Assert-Accepted {
    param(
        [string]$Name,
        [object]$Submission
    )

    if ($null -eq $Submission) {
        return
    }

    $submissionId = Get-PropertyValue -Object $Submission -Names @("id", "Id")
    $status = Convert-StatusName -Status (Get-PropertyValue -Object $Submission -Names @("status", "Status"))
    $errorMessage = Get-PropertyValue -Object $Submission -Names @("errorMessage", "ErrorMessage")

    if ($status -eq "Accepted") {
        Add-Pass -Name "$Name Accepted submission=$submissionId"
        return
    }

    $caseSummary = Format-CaseResults -Submission $Submission
    Add-Fail -Name "$Name expected Accepted" -Details "SubmissionId: $submissionId`nStatus: $status`nErrorMessage: $errorMessage`nCaseResults: $caseSummary"
}

function Run-AcceptedScenario {
    param(
        [string]$Name,
        [string]$ProblemId,
        [int]$Language,
        [string]$SourceCode
    )

    Write-Host "Submit $Name..."
    $submit = Submit-Code -ProblemId $ProblemId -Language $Language -SourceCode $SourceCode
    if (-not $submit.Ok) {
        Add-Fail -Name "$Name submit failed" -Details "HTTP $($submit.StatusCode) $($submit.ErrorMessage)"
        return
    }

    $submissionId = Get-PropertyValue -Object $submit.Body -Names @("id", "Id")
    if ([string]::IsNullOrWhiteSpace($submissionId)) {
        Add-Fail -Name "$Name submit failed" -Details "Submission id was not returned."
        return
    }

    $submission = Wait-Submission -SubmissionId $submissionId
    Assert-Accepted -Name $Name -Submission $submission
}

function Assert-FriendlyUnsupported {
    param(
        [string]$Name,
        [string]$ProblemId,
        [string]$SourceCode,
        [string]$ExpectedMessage
    )

    Write-Host "Submit $Name unsupported..."
    $submit = Submit-Code -ProblemId $ProblemId -Language $LanguageC11 -SourceCode $SourceCode

    if (-not $submit.Ok) {
        if ($submit.StatusCode -eq 400 -and $submit.RawBody.Contains($ExpectedMessage)) {
            Add-Pass -Name "$Name friendly unsupported"
            return
        }

        Add-Fail -Name "$Name expected friendly unsupported type" -Details "HTTP $($submit.StatusCode)`nBody: $($submit.RawBody)"
        return
    }

    $submissionId = Get-PropertyValue -Object $submit.Body -Names @("id", "Id")
    if ([string]::IsNullOrWhiteSpace($submissionId)) {
        Add-Fail -Name "$Name submit failed" -Details "Submission id was not returned."
        return
    }

    $submission = Wait-Submission -SubmissionId $submissionId
    if ($null -eq $submission) {
        return
    }

    $status = Convert-StatusName -Status (Get-PropertyValue -Object $submission -Names @("status", "Status"))
    $errorMessage = [string](Get-PropertyValue -Object $submission -Names @("errorMessage", "ErrorMessage"))

    if ($status -eq "CompileError" -and $errorMessage.Contains($ExpectedMessage)) {
        Add-Pass -Name "$Name friendly unsupported submission=$submissionId"
        return
    }

    Add-Fail -Name "$Name expected friendly unsupported type" -Details "SubmissionId: $submissionId`nStatus: $status`nErrorMessage: $errorMessage"
}

function Write-Summary {
    Write-Host ""
    Write-Host "Function Mode E2E Result:"
    Write-Host "Passed: $script:Passed"
    Write-Host "Failed: $script:Failed"
}

function New-TwoSumProblemBody {
    $spec = ConvertTo-CompactJson -Value @{
        functionName = "twoSum"
        returnType = "int[]"
        parameters = @(
            @{ name = "nums"; type = "int[]" },
            @{ name = "target"; type = "int" }
        )
        supportedLanguages = @("cpp17", "csharp", "c11")
    }

    $starter = ConvertTo-CompactJson -Value @{
        cpp17 = $TwoSumCpp17Source
        csharp = $TwoSumCSharpSource
        c11 = $TwoSumC11Source
    }

    return @{
        title = "[E2E] Function Two Sum"
        description = "E2E function Two Sum test problem"
        inputDescription = "Function mode does not use standard input"
        outputDescription = "Function mode does not use standard output"
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $false
        judgeMode = 2
        functionSpecJson = $spec
        starterCodeJson = $starter
    }
}

function New-ReverseListProblemBody {
    $spec = ConvertTo-CompactJson -Value @{
        functionName = "reverseList"
        returnType = "ListNode<int>"
        parameters = @(
            @{ name = "head"; type = "ListNode<int>" }
        )
        supportedLanguages = @("cpp17", "csharp")
    }

    $starter = ConvertTo-CompactJson -Value @{
        cpp17 = $ReverseListCpp17Source
        csharp = $ReverseListCSharpSource
    }

    return @{
        title = "[E2E] Function Reverse List"
        description = "E2E function reverse linked list test problem"
        inputDescription = "Linked list values are represented as JSON arrays"
        outputDescription = "Linked list values are represented as JSON arrays"
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $false
        judgeMode = 2
        functionSpecJson = $spec
        starterCodeJson = $starter
    }
}

function New-InvertTreeProblemBody {
    $spec = ConvertTo-CompactJson -Value @{
        functionName = "invertTree"
        returnType = "TreeNode<int>"
        parameters = @(
            @{ name = "root"; type = "TreeNode<int>" }
        )
        supportedLanguages = @("cpp17", "csharp")
    }

    $starter = ConvertTo-CompactJson -Value @{
        cpp17 = $InvertTreeCpp17Source
        csharp = $InvertTreeCSharpSource
    }

    return @{
        title = "[E2E] Function Invert Tree"
        description = "E2E function invert binary tree test problem"
        inputDescription = "Binary tree values are represented as level-order JSON arrays"
        outputDescription = "Binary tree values are represented as level-order JSON arrays"
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $false
        judgeMode = 2
        functionSpecJson = $spec
        starterCodeJson = $starter
    }
}

function New-StandardAbProblemBody {
    return @{
        title = "[E2E] Standard A+B"
        description = "E2E standard input/output A+B test problem"
        inputDescription = "Read two integers"
        outputDescription = "Print the sum"
        timeLimitMs = 1000
        memoryLimitMb = 128
        isPublished = $false
        judgeMode = 1
        functionSpecJson = $null
        starterCodeJson = $null
    }
}

$TwoSumCpp17Source = @'
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

$TwoSumCSharpSource = @'
public class Solution
{
    public int[] TwoSum(int[] nums, int target)
    {
        for (int i = 0; i < nums.Length; i++)
        {
            for (int j = i + 1; j < nums.Length; j++)
            {
                if (nums[i] + nums[j] == target)
                {
                    return new[] { i, j };
                }
            }
        }

        return System.Array.Empty<int>();
    }
}
'@

$TwoSumC11Source = @'
#include <stdlib.h>

int* twoSum(int* nums, int numsSize, int target, int* returnSize) {
    int* result = (int*)malloc(sizeof(int) * 2);

    for (int i = 0; i < numsSize; i++) {
        for (int j = i + 1; j < numsSize; j++) {
            if (nums[i] + nums[j] == target) {
                result[0] = i;
                result[1] = j;
                *returnSize = 2;
                return result;
            }
        }
    }

    *returnSize = 0;
    free(result);
    return NULL;
}
'@

$ReverseListCpp17Source = @'
struct ListNode {
    int val;
    ListNode* next;

    ListNode() : val(0), next(nullptr) {}
    ListNode(int x) : val(x), next(nullptr) {}
    ListNode(int x, ListNode* next) : val(x), next(next) {}
};

class Solution {
public:
    ListNode* reverseList(ListNode* head) {
        ListNode* prev = nullptr;
        ListNode* current = head;

        while (current != nullptr) {
            ListNode* next = current->next;
            current->next = prev;
            prev = current;
            current = next;
        }

        return prev;
    }
};
'@

$ReverseListCSharpSource = @'
public class ListNode
{
    public int val;
    public ListNode? next;

    public ListNode(int val = 0, ListNode? next = null)
    {
        this.val = val;
        this.next = next;
    }
}

public class Solution
{
    public ListNode? ReverseList(ListNode? head)
    {
        ListNode? prev = null;
        ListNode? current = head;

        while (current != null)
        {
            ListNode? next = current.next;
            current.next = prev;
            prev = current;
            current = next;
        }

        return prev;
    }
}
'@

$ReverseListC11UnsupportedSource = @'
int reverseList(int head) {
    return head;
}
'@

$InvertTreeCpp17Source = @'
struct TreeNode {
    int val;
    TreeNode* left;
    TreeNode* right;

    TreeNode() : val(0), left(nullptr), right(nullptr) {}
    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}
    TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}
};

class Solution {
public:
    TreeNode* invertTree(TreeNode* root) {
        if (root == nullptr) {
            return nullptr;
        }

        TreeNode* temp = root->left;
        root->left = invertTree(root->right);
        root->right = invertTree(temp);

        return root;
    }
};
'@

$InvertTreeCSharpSource = @'
public class TreeNode
{
    public int val;
    public TreeNode? left;
    public TreeNode? right;

    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)
    {
        this.val = val;
        this.left = left;
        this.right = right;
    }
}

public class Solution
{
    public TreeNode? InvertTree(TreeNode? root)
    {
        if (root == null)
        {
            return null;
        }

        TreeNode? temp = root.left;
        root.left = InvertTree(root.right);
        root.right = InvertTree(temp);

        return root;
    }
}
'@

$InvertTreeC11UnsupportedSource = @'
int invertTree(int root) {
    return root;
}
'@

$StandardAbC11Source = @'
#include <stdio.h>

int main(void) {
    int a, b;
    scanf("%d%d", &a, &b);
    printf("%d", a + b);
    return 0;
}
'@

Login

Write-Step "[2/10] Ensure problem: [E2E] Function Two Sum"
$twoSumBody = New-TwoSumProblemBody
$twoSumProblem = Ensure-Problem -Title "[E2E] Function Two Sum" -CreateBody $twoSumBody

Write-Step "[3/10] Ensure problem: [E2E] Function Reverse List"
$reverseListBody = New-ReverseListProblemBody
$reverseListProblem = Ensure-Problem -Title "[E2E] Function Reverse List" -CreateBody $reverseListBody

Write-Step "[4/10] Ensure problem: [E2E] Function Invert Tree"
$invertTreeBody = New-InvertTreeProblemBody
$invertTreeProblem = Ensure-Problem -Title "[E2E] Function Invert Tree" -CreateBody $invertTreeBody

Write-Step "[5/10] Ensure problem: [E2E] Standard A+B"
$standardBody = New-StandardAbProblemBody
$standardProblem = Ensure-Problem -Title "[E2E] Standard A+B" -CreateBody $standardBody

Write-Step "[6/10] Ensure test cases..."
Ensure-FunctionTestCase -Problem $twoSumProblem -ArgumentsJson '{ "nums": [2,7,11,15], "target": 9 }' -ExpectedJson '[0,1]' -Score 100
$twoSumProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $twoSumProblem -Names @("id", "Id"))
Ensure-FunctionTestCase -Problem $reverseListProblem -ArgumentsJson '{ "head": [1,2,3,4,5] }' -ExpectedJson '[5,4,3,2,1]' -Score 50
$reverseListProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $reverseListProblem -Names @("id", "Id"))
Ensure-FunctionTestCase -Problem $reverseListProblem -ArgumentsJson '{ "head": [] }' -ExpectedJson '[]' -Score 50
$reverseListProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $reverseListProblem -Names @("id", "Id"))
Ensure-FunctionTestCase -Problem $invertTreeProblem -ArgumentsJson '{ "root": [4,2,7,1,3,6,9] }' -ExpectedJson '[4,7,2,9,6,3,1]' -Score 50
$invertTreeProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $invertTreeProblem -Names @("id", "Id"))
Ensure-FunctionTestCase -Problem $invertTreeProblem -ArgumentsJson '{ "root": [] }' -ExpectedJson '[]' -Score 25
$invertTreeProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $invertTreeProblem -Names @("id", "Id"))
Ensure-FunctionTestCase -Problem $invertTreeProblem -ArgumentsJson '{ "root": [1,2,3,null,4] }' -ExpectedJson '[1,3,2,null,null,4]' -Score 25
$invertTreeProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $invertTreeProblem -Names @("id", "Id"))
Ensure-StandardTestCase -Problem $standardProblem -InputText '1 2' -ExpectedOutput '3' -Score 100
$standardProblem = Get-ProblemDetail -ProblemId (Get-PropertyValue -Object $standardProblem -Names @("id", "Id"))

$twoSumProblem = Ensure-PublishedProblem -Problem $twoSumProblem -UpdateBody $twoSumBody
$reverseListProblem = Ensure-PublishedProblem -Problem $reverseListProblem -UpdateBody $reverseListBody
$invertTreeProblem = Ensure-PublishedProblem -Problem $invertTreeProblem -UpdateBody $invertTreeBody
$standardProblem = Ensure-PublishedProblem -Problem $standardProblem -UpdateBody $standardBody

$twoSumProblemId = Get-PropertyValue -Object $twoSumProblem -Names @("id", "Id")
$reverseListProblemId = Get-PropertyValue -Object $reverseListProblem -Names @("id", "Id")
$invertTreeProblemId = Get-PropertyValue -Object $invertTreeProblem -Names @("id", "Id")
$standardProblemId = Get-PropertyValue -Object $standardProblem -Names @("id", "Id")

Write-Step "[7/10] Submit and verify function array/list cases..."
Run-AcceptedScenario -Name "C++17 twoSum" -ProblemId $twoSumProblemId -Language $LanguageCpp17 -SourceCode $TwoSumCpp17Source
Run-AcceptedScenario -Name "C# twoSum" -ProblemId $twoSumProblemId -Language $LanguageCSharp -SourceCode $TwoSumCSharpSource
Run-AcceptedScenario -Name "C11 twoSum" -ProblemId $twoSumProblemId -Language $LanguageC11 -SourceCode $TwoSumC11Source
Run-AcceptedScenario -Name "C++17 reverseList" -ProblemId $reverseListProblemId -Language $LanguageCpp17 -SourceCode $ReverseListCpp17Source
Run-AcceptedScenario -Name "C# reverseList" -ProblemId $reverseListProblemId -Language $LanguageCSharp -SourceCode $ReverseListCSharpSource
Assert-FriendlyUnsupported -Name "C11 reverseList" -ProblemId $reverseListProblemId -SourceCode $ReverseListC11UnsupportedSource -ExpectedMessage "Selected language is not supported by this function problem."

Write-Step "[8/10] Submit and verify TreeNode cases..."
Run-AcceptedScenario -Name "C++17 invertTree" -ProblemId $invertTreeProblemId -Language $LanguageCpp17 -SourceCode $InvertTreeCpp17Source
Run-AcceptedScenario -Name "C# invertTree" -ProblemId $invertTreeProblemId -Language $LanguageCSharp -SourceCode $InvertTreeCSharpSource
Assert-FriendlyUnsupported -Name "C11 invertTree" -ProblemId $invertTreeProblemId -SourceCode $InvertTreeC11UnsupportedSource -ExpectedMessage "Selected language is not supported by this function problem."

Write-Step "[9/10] Submit and verify standard mode regression..."
Run-AcceptedScenario -Name "C11 standard A+B" -ProblemId $standardProblemId -Language $LanguageC11 -SourceCode $StandardAbC11Source

Write-Step "[10/10] Summary"
Write-Summary
if ($script:Failed -gt 0) {
    exit 1
}

exit 0
