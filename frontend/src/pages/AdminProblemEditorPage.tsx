import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { createProblem, getProblem, updateProblem, type JudgeMode, type ProblemDetailDto } from "../api/problemsApi";
import { MarkdownEditor } from "../components/MarkdownEditor";

interface FunctionParameterEditor {
  name: string;
  type: string;
}

const functionTypes = ["int", "long", "double", "bool", "string", "int[]", "long[]", "double[]", "bool[]", "string[]", "int[][]", "ListNode<int>", "TreeNode<int>"];

export function AdminProblemEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEditMode = Boolean(id);
  const [problem, setProblem] = useState<ProblemDetailDto | null>(null);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [inputDescription, setInputDescription] = useState("");
  const [outputDescription, setOutputDescription] = useState("");
  const [timeLimitMs, setTimeLimitMs] = useState(1000);
  const [memoryLimitMb, setMemoryLimitMb] = useState(128);
  const [isPublished, setIsPublished] = useState(false);
  const [judgeMode, setJudgeMode] = useState<JudgeMode>(1);
  const [functionName, setFunctionName] = useState("");
  const [returnType, setReturnType] = useState("int");
  const [parameters, setParameters] = useState<FunctionParameterEditor[]>([]);
  const [cpp17StarterCode, setCpp17StarterCode] = useState(defaultCpp17StarterCode("solve", "int", []));
  const [c11StarterCode, setC11StarterCode] = useState(defaultC11StarterCode("solve", "int", []));
  const [csharpStarterCode, setCSharpStarterCode] = useState(defaultCSharpStarterCode("solve", "int", []));
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(isEditMode);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!id) {
      return;
    }

    let ignore = false;
    setIsLoading(true);

    getProblem(id)
      .then((detail) => {
        if (!ignore) {
          setProblem(detail);
          setTitle(detail.title);
          setDescription(detail.description);
          setInputDescription(detail.inputDescription);
          setOutputDescription(detail.outputDescription);
          setTimeLimitMs(detail.timeLimitMs);
          setMemoryLimitMb(detail.memoryLimitMb);
          setIsPublished(detail.isPublished);
          setJudgeMode(detail.judgeMode);
          applyFunctionConfig(detail);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "题目加载失败");
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [id]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setNotice(null);

    const functionConfig = buildFunctionConfig();
    if (!functionConfig.isValid) {
      setError(functionConfig.error);
      setIsSaving(false);
      return;
    }

    const payload = {
      title: title.trim(),
      description,
      inputDescription: judgeMode === 1 ? inputDescription : "",
      outputDescription: judgeMode === 1 ? outputDescription : "",
      timeLimitMs,
      memoryLimitMb,
      isPublished,
      judgeMode,
      functionSpecJson: functionConfig.functionSpecJson,
      starterCodeJson: functionConfig.starterCodeJson
    };

    try {
      if (id) {
        const updated = await updateProblem(id, payload);
        setProblem(updated);
        setNotice("题目已保存。");
      } else {
        const created = await createProblem(payload);
        navigate(`/admin/problems/${created.id}/edit`);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "保存题目失败");
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return <div className="state-line">正在加载题目...</div>;
  }

  if (error && isEditMode && !problem) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to="/admin/problems">
          返回题目管理
        </Link>
      </section>
    );
  }

  return (
    <section className="page-section narrow problem-editor-page">
      <div className="page-header">
        <div>
          <h1>{isEditMode ? "编辑题目" : "创建题目"}</h1>
          <p>维护题面、输入输出说明和基础限制。</p>
        </div>
        <div className="button-row">
          <Link className="button" to="/admin/problems">
            返回题目管理
          </Link>
          {problem && (
            <>
              <Link className="button" to={`/admin/problems/${problem.id}/test-cases`}>
                测试用例
              </Link>
              <Link className="button" to={`/problems/${problem.id}`}>
                查看题目
              </Link>
            </>
          )}
        </div>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <form className="form-stack" onSubmit={handleSubmit}>
        <label>
          标题
          <input value={title} onChange={(event) => setTitle(event.target.value)} required />
        </label>
        <MarkdownEditor label="题目描述" value={description} onChange={setDescription} required />
        <label>
          判题模式
          <select value={judgeMode} onChange={(event) => setJudgeMode(Number(event.target.value) as JudgeMode)}>
            <option value={1}>标准输入输出</option>
            <option value={2}>函数式答题</option>
          </select>
        </label>
        {judgeMode === 1 ? (
          <>
            <MarkdownEditor label="输入说明" value={inputDescription} onChange={setInputDescription} required />
            <MarkdownEditor label="输出说明" value={outputDescription} onChange={setOutputDescription} required />
          </>
        ) : (
          <section className="content-block">
            <h2>函数配置</h2>
            <p className="muted-text">函数式题目当前支持 C++17、C# 和 C11。答题人只需要实现目标函数，不需要编写 Main/main 或处理输入输出。</p>
            {hasListNodeType(returnType, parameters) && (
              <p className="quiet-note">链表类型在测试用例中使用数组表示，例如 [1,2,3] 表示 1 -&gt; 2 -&gt; 3；空数组 [] 表示空链表。C11 暂不支持 ListNode&lt;int&gt;。</p>
            )}
            {hasTreeNodeType(returnType, parameters) && (
              <p className="quiet-note">二叉树类型在测试用例中使用层序数组表示，例如 [1,2,3,null,4]；空数组 [] 表示空树；输出比较时会忽略尾部多余 null。C11 暂不支持 TreeNode&lt;int&gt;。</p>
            )}
            <div className="form-row">
              <label>
                函数名
                <input value={functionName} onChange={(event) => setFunctionName(event.target.value)} placeholder="twoSum" required />
              </label>
              <label>
                返回类型
                <select value={returnType} onChange={(event) => setReturnType(event.target.value)}>
                  {functionTypes.map((type) => (
                    <option key={type} value={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>参数名</th>
                    <th>参数类型</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {parameters.map((parameter, index) => (
                    <tr key={index}>
                      <td>
                        <input value={parameter.name} onChange={(event) => updateParameter(index, { ...parameter, name: event.target.value })} />
                      </td>
                      <td>
                        <select value={parameter.type} onChange={(event) => updateParameter(index, { ...parameter, type: event.target.value })}>
                          {functionTypes.map((type) => (
                            <option key={type} value={type}>
                              {type}
                            </option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <button className="button" type="button" onClick={() => removeParameter(index)}>
                          删除
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {parameters.length === 0 && <div className="empty-state">暂无参数</div>}
            </div>
            <button className="button" type="button" onClick={addParameter}>
              添加参数
            </button>
            <label>
              C++17 初始代码模板
              <textarea className="code-area" value={cpp17StarterCode} onChange={(event) => setCpp17StarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setCpp17StarterCode(defaultCpp17StarterCode(functionName || "solve", returnType, parameters))}>
              根据函数配置生成模板
            </button>
            <label>
              C11 初始代码模板
              <textarea className="code-area" value={c11StarterCode} onChange={(event) => setC11StarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setC11StarterCode(defaultC11StarterCode(functionName || "solve", returnType, parameters))}>
              根据函数配置生成 C11 模板
            </button>
            <label>
              C# 初始代码模板
              <textarea className="code-area" value={csharpStarterCode} onChange={(event) => setCSharpStarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setCSharpStarterCode(defaultCSharpStarterCode(functionName || "solve", returnType, parameters))}>
              根据函数配置生成 C# 模板
            </button>
          </section>
        )}
        <div className="form-row">
          <label>
            时间限制 ms
            <input type="number" min={1} value={timeLimitMs} onChange={(event) => setTimeLimitMs(Number(event.target.value))} />
          </label>
          <label>
            内存限制 MB
            <input type="number" min={16} value={memoryLimitMb} onChange={(event) => setMemoryLimitMb(Number(event.target.value))} />
          </label>
        </div>
        <label className="checkbox-line">
          <input type="checkbox" checked={isPublished} onChange={(event) => setIsPublished(event.target.checked)} />
          发布题目
        </label>
        <button className="button primary" type="submit" disabled={isSaving}>
          {isSaving ? "保存中..." : isEditMode ? "保存题目" : "创建题目"}
        </button>
      </form>
    </section>
  );

  function addParameter() {
    setParameters((current) => [...current, { name: "", type: "int" }]);
  }

  function updateParameter(index: number, parameter: FunctionParameterEditor) {
    setParameters((current) => current.map((item, itemIndex) => (itemIndex === index ? parameter : item)));
  }

  function removeParameter(index: number) {
    setParameters((current) => current.filter((_, itemIndex) => itemIndex !== index));
  }

  function buildFunctionConfig():
    | { isValid: true; functionSpecJson?: string | null; starterCodeJson?: string | null }
    | { isValid: false; error: string } {
    if (judgeMode === 1) {
      return { isValid: true, functionSpecJson: null, starterCodeJson: null };
    }

    const trimmedFunctionName = functionName.trim();
    if (!trimmedFunctionName) {
      return { isValid: false, error: "请填写函数名" };
    }

    const normalizedParameters = parameters.map((parameter) => ({
      name: parameter.name.trim(),
      type: parameter.type
    }));

    if (normalizedParameters.some((parameter) => !parameter.name)) {
      return { isValid: false, error: "请填写完整的参数名" };
    }

    const parameterNames = new Set(normalizedParameters.map((parameter) => parameter.name));
    if (parameterNames.size !== normalizedParameters.length) {
      return { isValid: false, error: "参数名不能重复" };
    }

    if (!cpp17StarterCode.trim()) {
      return { isValid: false, error: "请填写 C++17 初始代码模板" };
    }

    if (!c11StarterCode.trim()) {
      return { isValid: false, error: "请填写 C11 初始代码模板" };
    }

    if (!csharpStarterCode.trim()) {
      return { isValid: false, error: "请填写 C# 初始代码模板" };
    }

    const supportedLanguages = hasC11UnsupportedComplexType(returnType, normalizedParameters)
      ? ["cpp17", "csharp"]
      : ["cpp17", "csharp", "c11"];

    return {
      isValid: true,
      functionSpecJson: JSON.stringify({
        functionName: trimmedFunctionName,
        returnType,
        parameters: normalizedParameters,
        supportedLanguages
      }),
      starterCodeJson: JSON.stringify({
        cpp17: cpp17StarterCode,
        c11: c11StarterCode,
        csharp: csharpStarterCode
      })
    };
  }

  function applyFunctionConfig(detail: ProblemDetailDto) {
    if (detail.judgeMode !== 2) {
      return;
    }

    let parsedFunctionName = "solve";
    let parsedReturnType = "int";
    let parsedParameters: FunctionParameterEditor[] = [];

    try {
      const spec = JSON.parse(detail.functionSpecJson || "{}") as {
        functionName?: string;
        returnType?: string;
        parameters?: FunctionParameterEditor[];
      };
      parsedFunctionName = spec.functionName || "solve";
      parsedReturnType = spec.returnType || "int";
      parsedParameters = Array.isArray(spec.parameters) ? spec.parameters.map((parameter) => ({ name: parameter.name, type: parameter.type })) : [];
      setFunctionName(parsedFunctionName);
      setReturnType(parsedReturnType);
      setParameters(parsedParameters);
    } catch {
      setFunctionName(parsedFunctionName);
      setReturnType(parsedReturnType);
      setParameters(parsedParameters);
    }

    try {
      const starter = JSON.parse(detail.starterCodeJson || "{}") as { cpp17?: string; c11?: string; csharp?: string };
      setCpp17StarterCode(starter.cpp17 || defaultCpp17StarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
      setC11StarterCode(starter.c11 || defaultC11StarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
      setCSharpStarterCode(starter.csharp || defaultCSharpStarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
    } catch {
      setCpp17StarterCode(defaultCpp17StarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
      setC11StarterCode(defaultC11StarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
      setCSharpStarterCode(defaultCSharpStarterCode(parsedFunctionName, parsedReturnType, parsedParameters));
    }
  }
}

function defaultCpp17StarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[]) {
  const cppReturnType = toCppType(returnType);
  const cppParameters = parameters
    .map((parameter) => `${toCppParameterType(parameter.type)} ${parameter.name || "arg"}`)
    .join(", ");
  const listNodePrefix = hasListNodeType(returnType, parameters)
    ? `struct ListNode {\n    int val;\n    ListNode* next;\n\n    ListNode() : val(0), next(nullptr) {}\n    ListNode(int x) : val(x), next(nullptr) {}\n    ListNode(int x, ListNode* next) : val(x), next(next) {}\n};\n\n`
    : "";
  const treeNodePrefix = hasTreeNodeType(returnType, parameters)
    ? `struct TreeNode {\n    int val;\n    TreeNode* left;\n    TreeNode* right;\n\n    TreeNode() : val(0), left(nullptr), right(nullptr) {}\n    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}\n    TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}\n};\n\n`
    : "";

  return `${listNodePrefix}${treeNodePrefix}class Solution {\npublic:\n    ${cppReturnType} ${functionName}(${cppParameters}) {\n        \n    }\n};`;
}

function defaultCSharpStarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[]) {
  const csharpReturnType = toCSharpType(returnType);
  const csharpFunctionName = toCSharpMethodName(functionName);
  const csharpParameters = parameters
    .map((parameter) => `${toCSharpType(parameter.type)} ${parameter.name || "arg"}`)
    .join(", ");
  const listNodePrefix = hasListNodeType(returnType, parameters)
    ? `public class ListNode\n{\n    public int val;\n    public ListNode? next;\n\n    public ListNode(int val = 0, ListNode? next = null)\n    {\n        this.val = val;\n        this.next = next;\n    }\n}\n\n`
    : "";
  const treeNodePrefix = hasTreeNodeType(returnType, parameters)
    ? `public class TreeNode\n{\n    public int val;\n    public TreeNode? left;\n    public TreeNode? right;\n\n    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)\n    {\n        this.val = val;\n        this.left = left;\n        this.right = right;\n    }\n}\n\n`
    : "";

  return `${listNodePrefix}${treeNodePrefix}public class Solution\n{\n    public ${csharpReturnType} ${csharpFunctionName}(${csharpParameters})\n    {\n        \n    }\n}`;
}

function defaultC11StarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[]) {
  const unsupportedComplexType = getC11UnsupportedComplexType(returnType, parameters);
  if (unsupportedComplexType) {
    return `/* C11 function mode does not support type: ${unsupportedComplexType} */`;
  }

  const unsupportedType = [returnType, ...parameters.map((parameter) => parameter.type)]
    .find((type) => !isC11SupportedType(type));

  if (unsupportedType) {
    return `/* C11 function mode does not support type: ${unsupportedType} */`;
  }

  const cReturnType = toC11ReturnType(returnType);
  const cParameters = parameters.flatMap((parameter) => toC11ParameterParts(parameter.type, parameter.name || "arg"));
  if (isC11ArrayType(returnType)) {
    cParameters.push("int* returnSize");
  }

  return `${cReturnType} ${functionName}(${cParameters.join(", ")}) {\n    \n}`;
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

function toCSharpType(type: string) {
  if (type === "ListNode<int>") {
    return "ListNode?";
  }

  if (type === "TreeNode<int>") {
    return "TreeNode?";
  }

  return type;
}

function hasListNodeType(returnType: string, parameters: FunctionParameterEditor[]) {
  return returnType === "ListNode<int>" || parameters.some((parameter) => parameter.type === "ListNode<int>");
}

function hasTreeNodeType(returnType: string, parameters: FunctionParameterEditor[]) {
  return returnType === "TreeNode<int>" || parameters.some((parameter) => parameter.type === "TreeNode<int>");
}

function hasC11UnsupportedComplexType(returnType: string, parameters: FunctionParameterEditor[]) {
  return Boolean(getC11UnsupportedComplexType(returnType, parameters));
}

function getC11UnsupportedComplexType(returnType: string, parameters: FunctionParameterEditor[]) {
  return [returnType, ...parameters.map((parameter) => parameter.type)]
    .find((type) => type === "ListNode<int>" || type === "TreeNode<int>");
}

function toCSharpMethodName(functionName: string) {
  return functionName ? `${functionName[0].toUpperCase()}${functionName.slice(1)}` : functionName;
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
