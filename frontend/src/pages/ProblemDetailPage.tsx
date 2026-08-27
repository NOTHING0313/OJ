import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link, useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getProblem, type ProblemDetailDto } from "../api/problemsApi";
import { createSubmission, type JudgeLanguage } from "../api/submissionsApi";
import { canManageContent, useAuth } from "../auth/AuthContext";
import { CodeEditor } from "../components/CodeEditor";
import { MarkdownRenderer } from "../components/MarkdownRenderer";

export function ProblemDetailPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { currentUser, isAuthenticated } = useAuth();
  const challengeId = searchParams.get("challengeId");
  const taskId = searchParams.get("taskId");
  const [problem, setProblem] = useState<ProblemDetailDto | null>(null);
  const [language, setLanguage] = useState<JudgeLanguage>(1);
  const [sourceCode, setSourceCode] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const functionSpec = useMemo(() => parseFunctionSpec(problem?.functionSpecJson), [problem?.functionSpecJson]);
  const sampleTestCases = useMemo(() => problem?.testCases.filter((testCase) => testCase.visibility === 1) || [], [problem?.testCases]);
  const sampleScoreTotal = useMemo(() => sampleTestCases.reduce((total, testCase) => total + testCase.score, 0), [sampleTestCases]);
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
        if (detail.judgeMode === 2) {
          const cachedLanguage = languageCacheKey ? Number(localStorage.getItem(languageCacheKey)) : 1;
          const parsedSpec = parseFunctionSpec(detail.functionSpecJson);
          const c11UnsupportedProblem = hasFunctionSpecListNode(parsedSpec) || hasFunctionSpecTreeNode(parsedSpec);
          setLanguage((cachedLanguage === 2 && !c11UnsupportedProblem) || cachedLanguage === 3 ? cachedLanguage : 1);
        }
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "加载题目失败"));
  }, [id, languageCacheKey]);

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

  return (
    <section className="page-section two-column problem-detail-layout ui-v2-page problem-detail-v2-page">
      <article className="problem-content problem-content-v2">
        <div className="page-header compact ui-v2-page-header">
          <div>
            <p className="eyebrow">PROBLEM</p>
            <h1>{problem.title}</h1>
            <p>
              {problem.timeLimitMs} ms / {problem.memoryLimitMb} MB / {sampleScoreTotal} 分
            </p>
          </div>
          <div className="button-row">
            {challengeId && (
              <Link className="button" to={`/challenges/${challengeId}`}>
                返回挑战棋盘
              </Link>
            )}
            {taskId && <span className="context-chip">Challenge Task</span>}
            {isAuthenticated && (
              <Link className="button" to={`/submissions/my?problemId=${problem.id}`}>
                我的提交
              </Link>
            )}
            {canManageContent(currentUser?.role) && (
              <Link className="button" to={`/admin/problems/${problem.id}/test-cases`}>
                测试用例
              </Link>
            )}
          </div>
        </div>

        <section className="content-block">
          <h2>描述</h2>
          <MarkdownRenderer value={problem.description} />
        </section>
        {problem.judgeMode === 1 ? (
          <>
            <section className="content-block">
              <h2>输入说明</h2>
              <MarkdownRenderer value={problem.inputDescription} />
            </section>
            <section className="content-block">
              <h2>输出说明</h2>
              <MarkdownRenderer value={problem.outputDescription} />
            </section>
          </>
        ) : (
          <section className="content-block">
            <h2>函数说明</h2>
            <p>只需要完成 Solution 类中的函数，不需要编写 Main/main，不需要处理输入输出。函数式题目当前支持 C++17、C# 和 C11。</p>
            {hasListNode && <p className="quiet-note">链表测试数据使用数组表示，例如 [1,2,3] 表示 1 -&gt; 2 -&gt; 3；[] 表示空链表。C11 暂不支持链表函数式判题。</p>}
            {hasTreeNode && <p className="quiet-note">二叉树测试数据使用层序数组表示，例如 [1,2,3,null,4]；[] 表示空树；输出比较会忽略尾部多余 null。C11 暂不支持二叉树函数式判题。</p>}
            {language === 2 && <p className="quiet-note">C 语言返回数组时，请使用 malloc 分配返回数组，并正确设置 *returnSize。</p>}
            {functionSpec ? (
              <div className="table-wrap">
                <table className="function-spec-table">
                  <tbody>
                    <tr>
                      <th>函数名</th>
                      <td>{functionSpec.functionName}</td>
                    </tr>
                    <tr>
                      <th>返回类型</th>
                      <td>{functionSpec.returnType}</td>
                    </tr>
                    <tr>
                      <th>参数</th>
                      <td>{functionSpec.parameters.map((parameter) => `${parameter.type} ${parameter.name}`).join(", ") || "无"}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="quiet-note">函数配置暂不可用，请联系出题人检查配置。</div>
            )}
          </section>
        )}

        <section className="content-block public-samples">
          <h2>公开样例</h2>
          {sampleTestCases.length === 0 ? (
            <div className="empty-state">暂无公开样例</div>
          ) : (
            <div className="sample-list">
              {sampleTestCases.map((testCase, index) => (
                <div className="sample-card" key={testCase.id}>
                  <h3>样例 {index + 1}</h3>
                  {problem.judgeMode === 1 ? (
                    <div className="sample-grid">
                      <div>
                        <span>输入</span>
                        <pre>{testCase.input || "-"}</pre>
                      </div>
                      <div>
                        <span>输出</span>
                        <pre>{testCase.expectedOutput || "-"}</pre>
                      </div>
                    </div>
                  ) : (
                    <div className="sample-grid">
                      <div>
                        <span>ArgumentsJson</span>
                        <pre>{formatJsonString(testCase.argumentsJson)}</pre>
                      </div>
                      <div>
                        <span>ExpectedJson</span>
                        <pre>{formatJsonString(testCase.expectedJson)}</pre>
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </section>
      </article>

      <aside className="submit-panel submit-panel-v2">
        <h2>提交代码</h2>
        <form onSubmit={handleSubmit} className="form-stack">
          <label>
            语言
            <select value={language} onChange={(event) => handleLanguageChange(Number(event.target.value) as JudgeLanguage)}>
              <option value={1}>C++17</option>
              <option value={2} disabled={problem.judgeMode === 2 && hasC11UnsupportedComplexType}>C11</option>
              <option value={3}>C#</option>
            </select>
          </label>

          <div className="code-field">
            <span>代码</span>
            <CodeEditor value={sourceCode} language={language} onChange={handleSourceCodeChange} height="560px" />
          </div>

          {error && <div className="alert error">{error}</div>}
          <div className="button-row">
            <button className="button" type="button" onClick={clearSourceCache}>
              清空本地缓存
            </button>
            <button className="button primary" type="submit" disabled={isSubmitting}>
              {isSubmitting ? "提交中..." : "提交"}
            </button>
          </div>
        </form>
      </aside>
    </section>
  );
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
  | { functionName: string; returnType: string; parameters: Array<{ name: string; type: string }> }
  | null {
  if (!functionSpecJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(functionSpecJson) as {
      functionName?: string;
      returnType?: string;
      parameters?: Array<{ name: string; type: string }>;
    };

    if (!parsed.functionName || !parsed.returnType || !Array.isArray(parsed.parameters)) {
      return null;
    }

    return {
      functionName: parsed.functionName,
      returnType: parsed.returnType,
      parameters: parsed.parameters
    };
  } catch {
    return null;
  }
}

function formatJsonString(value?: string | null) {
  if (!value) {
    return "-";
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}
