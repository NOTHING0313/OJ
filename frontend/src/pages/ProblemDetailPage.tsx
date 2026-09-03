import { FormEvent, useEffect, useMemo, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getProblem, type ProblemDetailDto } from "../api/problemsApi";
import { getCurrentSeasonProblemLeaderboard, type SeasonProblemLeaderboard } from "../api/leaderboardsApi";
import { createSubmission, type JudgeLanguage } from "../api/submissionsApi";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { CodeEditor } from "../components/CodeEditor";
import { ProblemDetailView } from "../components/problems/ProblemDetailView";

export function ProblemDetailPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { currentUser, isAuthenticated } = useAuth();
  const challengeId = searchParams.get("challengeId");
  const taskId = searchParams.get("taskId");
  const [problem, setProblem] = useState<ProblemDetailDto | null>(null);
  const [seasonLeaderboard, setSeasonLeaderboard] = useState<SeasonProblemLeaderboard | null>(null);
  const [language, setLanguage] = useState<JudgeLanguage>(1);
  const [sourceCode, setSourceCode] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
  const hasTreeNode = useMemo(() => hasFunctionSpecTreeNode(functionSpec), [functionSpec]);
  const hasC11UnsupportedComplexType = hasListNode || hasTreeNode;
  const languageCacheKey = useMemo(() => {
    if (!id) {
      return null;
    }

    return `oj:language:${id}:${challengeId || "standalone"}:${taskId || "none"}`;
  }, [id, challengeId, taskId]);

  const sourceCacheKey = useMemo(() => {
    if (!id) {
      return null;
    }

    return `oj:source:${id}:${language}:${challengeId || "standalone"}:${taskId || "none"}`;
  }, [id, language, challengeId, taskId]);

  useEffect(() => {
    if (!id) {
      return;
    }

    getProblem(id)
      .then((detail) => {
        setProblem(detail);
        const cachedLanguage = languageCacheKey ? Number(localStorage.getItem(languageCacheKey)) : 1;
        const parsedSpec = parseFunctionSpec(detail.functionSpecJson);
        const languages = getAvailableLanguages(detail.allowedLanguagesMask, parsedSpec);
        const cachedJudgeLanguage = cachedLanguage as JudgeLanguage;
        setLanguage(languages.includes(cachedJudgeLanguage) ? cachedJudgeLanguage : languages[0] ?? 1);
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "加载题目失败"));
  }, [id, languageCacheKey]);

  useEffect(() => {
    if (!id) return;
    getCurrentSeasonProblemLeaderboard(id)
      .then(setSeasonLeaderboard)
      .catch(() => setSeasonLeaderboard(null));
  }, [id]);

  useEffect(() => {
    if (!languageCacheKey || availableLanguages.length === 0) {
      return;
    }

    setLanguage((current) => availableLanguages.includes(current) ? current : availableLanguages[0]);
  }, [availableLanguages, languageCacheKey]);

  useEffect(() => {
    if (!sourceCacheKey) {
      return;
    }

    const defaultSource = problem?.judgeMode === 2
      ? getFunctionStarterCode(language, problem.starterCodeJson, functionSpec)
      : defaultCodeTemplate(language);
    setSourceCode(localStorage.getItem(sourceCacheKey) ?? defaultSource);
  }, [sourceCacheKey, language, problem?.judgeMode, problem?.starterCodeJson, functionSpec]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!id || !sourceCode.trim()) {
      setError("请填写代码后再提交");
      return;
    }

    if (!isAuthenticated) {
      setError("请先登录");
      navigate(`/login?returnTo=${encodeURIComponent(`${location.pathname}${location.search}`)}`);
      return;
    }

    if (!availableLanguages.includes(language)) {
      setError("该题目不允许使用当前语言提交。");
      return;
    }

    if (problem?.judgeMode === 2 && hasC11UnsupportedComplexType && language === 2) {
      setError("C11 暂不支持链表或二叉树函数式判题。");
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const submission = await createSubmission({
        problemId: id,
        ...(taskId ? { challengeTaskId: taskId } : {}),
        language,
        sourceCode
      });
      navigate(`/submissions/${submission.id}${challengeId ? `?challengeId=${challengeId}` : ""}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "提交失败");
    } finally {
      setIsSubmitting(false);
    }
  }

  function handleLanguageChange(nextLanguage: JudgeLanguage) {
    if (sourceCacheKey) {
      localStorage.setItem(sourceCacheKey, sourceCode);
    }

    if (languageCacheKey) {
      localStorage.setItem(languageCacheKey, String(nextLanguage));
    }

    setLanguage(nextLanguage);
  }

  function handleSourceCodeChange(value: string) {
    setSourceCode(value);

    if (sourceCacheKey) {
      localStorage.setItem(sourceCacheKey, value);
    }
  }

  function clearSourceCache() {
    if (sourceCacheKey) {
      localStorage.removeItem(sourceCacheKey);
    }

    setSourceCode(problem?.judgeMode === 2 ? getFunctionStarterCode(language, problem.starterCodeJson, functionSpec) : defaultCodeTemplate(language));
  }

  if (error && !problem) {
    return <div className="alert error">{error}</div>;
  }

  if (!problem) {
    return <div className="state-line">加载中...</div>;
  }

  const sharedProblem = {
    id: problem.id,
    title: problem.title,
    description: problem.description,
    inputDescription: problem.inputDescription,
    outputDescription: problem.outputDescription,
    timeLimitMs: problem.timeLimitMs,
    memoryLimitMb: problem.memoryLimitMb,
    totalScore: problem.totalScore,
    judgeMode: problem.judgeMode,
    languageTags: explicitLanguageTags.map(getProblemLanguageTag),
    functionSpec,
    hasListNode,
    hasTreeNode,
    samples: sampleTestCases.map((testCase) => ({
      id: testCase.id,
      input: problem.judgeMode === 1 ? testCase.input : formatFunctionSampleArguments(testCase.argumentsJson, functionSpec),
      output: problem.judgeMode === 1 ? testCase.expectedOutput : formatFunctionSampleExpected(testCase.expectedJson)
    }))
  };

  return <ProblemDetailView
    problem={sharedProblem}
    seasonScore={seasonLeaderboard?.season && seasonLeaderboard.problem ? {
      seasonName: seasonLeaderboard.season.name,
      baseScore: seasonLeaderboard.problem.baseScore,
      timeBonus: Math.max(...seasonLeaderboard.season.scoringRules.timeBonusPercentages),
      runtimeBonus: maxBonus(seasonLeaderboard.season.scoringRules.runtimeBonusTiers),
      memoryBonus: maxBonus(seasonLeaderboard.season.scoringRules.memoryBonusTiers)
    } : null}
    language={language}
    languages={availableLanguages.map((value) => ({ value, label: getJudgeLanguageName(value) }))}
    isAuthenticated={isAuthenticated}
    canManage={canManageContent(currentUser?.role)}
    challengeId={challengeId}
    error={error}
    isSubmitting={isSubmitting}
    editor={<CodeEditor value={sourceCode} language={language} onChange={handleSourceCodeChange} height="560px" />}
    onSubmit={handleSubmit}
    onLanguageChange={(value) => handleLanguageChange(value as JudgeLanguage)}
    onClearSource={clearSourceCache}
  />;

}

function defaultCodeTemplate(language: JudgeLanguage) {
  if (language === 1) {
    return "#include <bits/stdc++.h>\nusing namespace std;\n\nint main() {\n    return 0;\n}\n";
  }

  if (language === 2) {
    return "#include <stdio.h>\n\nint main(void) {\n    return 0;\n}\n";
  }

  return "using System;\n\npublic class Program\n{\n    public static void Main()\n    {\n    }\n}\n";
}

function getFunctionStarterCode(
  language: JudgeLanguage,
  starterCodeJson?: string | null,
  functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null
) {
  if (language === 2) {
    return getC11StarterCode(starterCodeJson, functionSpec);
  }

  if (language === 3) {
    return getCSharpStarterCode(starterCodeJson, functionSpec);
  }

  return getCpp17StarterCode(starterCodeJson, functionSpec);
}

function getCpp17StarterCode(
  starterCodeJson?: string | null,
  functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null
) {
  const fallback = defaultCpp17StarterCode(functionSpec);
  if (!starterCodeJson) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(starterCodeJson) as { cpp17?: string };
    return parsed.cpp17 || fallback;
  } catch {
    return fallback;
  }
}

function getCSharpStarterCode(
  starterCodeJson?: string | null,
  functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null
) {
  const fallback = defaultCSharpStarterCode(functionSpec);
  if (!starterCodeJson) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(starterCodeJson) as { csharp?: string };
    return shouldReplaceLegacyCSharpStarter(parsed.csharp, functionSpec) ? fallback : parsed.csharp || fallback;
  } catch {
    return fallback;
  }
}

function getC11StarterCode(
  starterCodeJson?: string | null,
  functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null
) {
  const fallback = defaultC11StarterCode(functionSpec);
  if (!starterCodeJson) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(starterCodeJson) as { c11?: string };
    return parsed.c11 || fallback;
  } catch {
    return fallback;
  }
}

function defaultCpp17StarterCode(functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null) {
  const functionName = functionSpec?.functionName || "solve";
  const returnType = toCppType(functionSpec?.returnType || "int");
  const parameters = functionSpec?.parameters
    .map((parameter) => `${toCppParameterType(parameter.type)} ${parameter.name || "arg"}`)
    .join(", ") || "";
  const listNodePrefix = hasFunctionSpecListNode(functionSpec)
    ? `struct ListNode {\n    int val;\n    ListNode* next;\n\n    ListNode() : val(0), next(nullptr) {}\n    ListNode(int x) : val(x), next(nullptr) {}\n    ListNode(int x, ListNode* next) : val(x), next(next) {}\n};\n\n`
    : "";
  const treeNodePrefix = hasFunctionSpecTreeNode(functionSpec)
    ? `struct TreeNode {\n    int val;\n    TreeNode* left;\n    TreeNode* right;\n\n    TreeNode() : val(0), left(nullptr), right(nullptr) {}\n    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}\n    TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}\n};\n\n`
    : "";

  return `${listNodePrefix}${treeNodePrefix}class Solution {\npublic:\n    ${returnType} ${functionName}(${parameters}) {\n        \n    }\n};`;
}

function defaultCSharpStarterCode(functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null) {
  const functionName = toCSharpMethodName(functionSpec?.functionName || "solve");
  const returnType = toCSharpType(functionSpec?.returnType || "int");
  const parameters = functionSpec?.parameters
    .map((parameter) => `${toCSharpType(parameter.type)} ${parameter.name || "arg"}`)
    .join(", ") || "";
  const listNodePrefix = hasFunctionSpecListNode(functionSpec)
    ? `public class ListNode\n{\n    public int val;\n    public ListNode? next;\n\n    public ListNode(int val = 0, ListNode? next = null)\n    {\n        this.val = val;\n        this.next = next;\n    }\n}\n\n`
    : "";
  const treeNodePrefix = hasFunctionSpecTreeNode(functionSpec)
    ? `public class TreeNode\n{\n    public int val;\n    public TreeNode? left;\n    public TreeNode? right;\n\n    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)\n    {\n        this.val = val;\n        this.left = left;\n        this.right = right;\n    }\n}\n\n`
    : "";

  return `${listNodePrefix}${treeNodePrefix}public class Solution\n{\n    public ${returnType} ${functionName}(${parameters})\n    {\n        \n    }\n}`;
}

function shouldReplaceLegacyCSharpStarter(
  starterCode?: string,
  functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null
) {
  if (!starterCode || !functionSpec?.functionName) {
    return false;
  }

  const originalName = functionSpec.functionName;
  const csharpName = toCSharpMethodName(originalName);
  return originalName !== csharpName
    && starterCode.includes("public class Solution")
    && starterCode.includes(`${originalName}(`)
    && !starterCode.includes(`${csharpName}(`);
}

function toCSharpMethodName(functionName: string) {
  return functionName ? `${functionName[0].toUpperCase()}${functionName.slice(1)}` : functionName;
}

function toCSharpType(type: string) {
  if (type === "ListNode<int>") {
    return "ListNode?";
  }

  if (type === "TreeNode<int>") {
    return "TreeNode?";
  }

  return type;
}

function defaultC11StarterCode(functionSpec?: { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> } | null) {
  const unsupportedComplexType = getC11UnsupportedComplexType(functionSpec);
  if (unsupportedComplexType) {
    return `/* C11 function mode does not support type: ${unsupportedComplexType} */`;
  }

  const unsupportedType = [functionSpec?.returnType, ...(functionSpec?.parameters.map((parameter) => parameter.type) || [])]
    .find((type) => type && !isC11SupportedType(type));

  if (unsupportedType) {
    return `/* C11 function mode does not support type: ${unsupportedType} */`;
  }

  const functionName = functionSpec?.functionName || "solve";
  const returnType = functionSpec?.returnType || "int";
  const cReturnType = toC11ReturnType(returnType);
  const parameters = functionSpec?.parameters.flatMap((parameter) => toC11ParameterParts(parameter.type, parameter.name || "arg")) || [];
  if (isC11ArrayType(returnType)) {
    parameters.push("int* returnSize");
  }

  return `${cReturnType} ${functionName}(${parameters.join(", ")}) {\n    \n}`;
}

function isC11SupportedType(type: string) {
  return ["int", "long", "double", "bool", "int[]", "long[]", "double[]"].includes(type);
}

function isC11ArrayType(type: string) {
  return type.endsWith("[]");
}

function toC11ReturnType(type: string) {
  if (type === "int[]") {
    return "int*";
  }

  if (type === "long[]") {
    return "long*";
  }

  if (type === "double[]") {
    return "double*";
  }

  return type;
}

function toC11ParameterParts(type: string, name: string) {
  if (type === "int[]") {
    return [`int* ${name}`, `int ${name}Size`];
  }

  if (type === "long[]") {
    return [`long* ${name}`, `int ${name}Size`];
  }

  if (type === "double[]") {
    return [`double* ${name}`, `int ${name}Size`];
  }

  return [`${type} ${name}`];
}

function toCppType(type: string) {
  if (type === "ListNode<int>") {
    return "ListNode*";
  }

  if (type === "TreeNode<int>") {
    return "TreeNode*";
  }

  return type
    .replace("long", "long long")
    .replace("int[][]", "vector<vector<int>>")
    .replace("int[]", "vector<int>")
    .replace("long long[]", "vector<long long>")
    .replace("double[]", "vector<double>")
    .replace("bool[]", "vector<bool>")
    .replace("string[]", "vector<string>");
}

function toCppParameterType(type: string) {
  const cppType = toCppType(type);
  return type.endsWith("[]") ? `${cppType}&` : cppType;
}

function hasFunctionSpecListNode(
  functionSpec?: { returnType: string; parameters: Array<{ type: string }> } | null
) {
  return functionSpec?.returnType === "ListNode<int>"
    || Boolean(functionSpec?.parameters.some((parameter) => parameter.type === "ListNode<int>"));
}

function hasFunctionSpecTreeNode(
  functionSpec?: { returnType: string; parameters: Array<{ type: string }> } | null
) {
  return functionSpec?.returnType === "TreeNode<int>"
    || Boolean(functionSpec?.parameters.some((parameter) => parameter.type === "TreeNode<int>"));
}

function getC11UnsupportedComplexType(
  functionSpec?: { returnType: string; parameters: Array<{ type: string }> } | null
) {
  if (!functionSpec) {
    return null;
  }

  return [functionSpec.returnType, ...functionSpec.parameters.map((parameter) => parameter.type)]
    .find((type) => type === "ListNode<int>" || type === "TreeNode<int>") || null;
}

function parseFunctionSpec(functionSpecJson?: string | null):
  | { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }>; supportedLanguages?: string[] }
  | null {
  if (!functionSpecJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(functionSpecJson) as {
      functionName?: string;
      returnType?: string;
      parameters?: Array<{ name: string; type: string }>;
      supportedLanguages?: string[];
    };

    if (!parsed.functionName || !parsed.returnType || !Array.isArray(parsed.parameters)) {
      return null;
    }

    return {
      functionName: parsed.functionName,
      returnType: parsed.returnType,
      parameters: parsed.parameters,
      supportedLanguages: Array.isArray(parsed.supportedLanguages) ? parsed.supportedLanguages : undefined
    };
  } catch {
    return null;
  }
}

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

function formatFunctionSampleArguments(
  value: string | null | undefined,
  functionSpec?: { parameters: Array<{ name: string; type: string }> } | null
) {
  if (!value) {
    return "-";
  }

  try {
    const parsed = JSON.parse(value) as unknown;
    if (!isPlainRecord(parsed)) {
      return formatSampleValue(parsed);
    }

    const parameterNames = functionSpec?.parameters.map((parameter) => parameter.name) ?? Object.keys(parsed);
    const orderedNames = [
      ...parameterNames.filter((name) => Object.prototype.hasOwnProperty.call(parsed, name)),
      ...Object.keys(parsed).filter((name) => !parameterNames.includes(name))
    ];

    return orderedNames
      .map((name) => `${name} = ${formatSampleValue(parsed[name])}`)
      .join("\n");
  } catch {
    return value;
  }
}

function formatFunctionSampleExpected(value?: string | null) {
  if (!value) {
    return "-";
  }

  try {
    return formatSampleValue(JSON.parse(value) as unknown);
  } catch {
    return value;
  }
}

function formatSampleValue(value: unknown, indent = 0): string {
  if (value === null) {
    return "null";
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return "[]";
    }

    if (value.every(isSamplePrimitive)) {
      return `[${value.map((item) => formatSampleValue(item)).join(", ")}]`;
    }

    const padding = "  ".repeat(indent);
    const childPadding = "  ".repeat(indent + 1);
    return `[\n${value.map((item) => `${childPadding}${indentMultiline(formatSampleValue(item, indent + 1), indent + 1)}`).join(",\n")}\n${padding}]`;
  }

  if (isPlainRecord(value)) {
    const entries = Object.entries(value);
    if (entries.length === 0) {
      return "{}";
    }

    if (entries.every(([, item]) => isSamplePrimitive(item))) {
      const compact = `{ ${entries.map(([key, item]) => `${formatSampleKey(key)}: ${formatSampleValue(item)}`).join(", ")} }`;
      if (compact.length <= 92) {
        return compact;
      }
    }

    const padding = "  ".repeat(indent);
    const childPadding = "  ".repeat(indent + 1);
    return `{\n${entries.map(([key, item]) => `${childPadding}${formatSampleKey(key)}: ${indentMultiline(formatSampleValue(item, indent + 1), indent + 1)}`).join(",\n")}\n${padding}}`;
  }

  if (typeof value === "string") {
    return JSON.stringify(value);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return JSON.stringify(value);
}

function indentMultiline(value: string, indent: number) {
  if (!value.includes("\n")) {
    return value;
  }

  const padding = "  ".repeat(indent);
  return value.split("\n").map((line, index) => index === 0 ? line : `${padding}${line}`).join("\n");
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isSamplePrimitive(value: unknown) {
  return value === null || ["string", "number", "boolean"].includes(typeof value);
}

function formatSampleKey(key: string) {
  return /^[A-Za-z_][A-Za-z0-9_]*$/.test(key) ? key : JSON.stringify(key);
}

function maxBonus(tiers: Array<{ bonusPercentage: number }>) {
  return Math.max(0, ...tiers.map((tier) => tier.bonusPercentage));
}
