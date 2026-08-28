import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  addTestCase,
  deleteTestCase,
  exportTestCases,
  getProblem,
  importTestCases,
  updateTestCase,
  type ImportTestCaseError,
  type ProblemDetailDto,
  type TestCaseDto,
  type TestCaseVisibility
} from "../api/problemsApi";
import { visibilityLabel } from "../utils/labels";

type FunctionCustomType = {
  name: string;
  fields: Array<{ name: string; type: string }>;
};

type FunctionSpec = {
  functionName: string;
  returnType: string;
  parameters: Array<{ name: string; type: string }>;
  types?: FunctionCustomType[];
};

export function AdminTestCaseEditorPage() {
  const { id } = useParams();
  const [problem, setProblem] = useState<ProblemDetailDto | null>(null);
  const [input, setInput] = useState("");
  const [expectedOutput, setExpectedOutput] = useState("");
  const [argumentsJson, setArgumentsJson] = useState("");
  const [expectedJson, setExpectedJson] = useState("");
  const [visibility, setVisibility] = useState<TestCaseVisibility>(2);
  const [score, setScore] = useState(100);
  const [importText, setImportText] = useState("");
  const [showImport, setShowImport] = useState(false);
  const [importErrors, setImportErrors] = useState<ImportTestCaseError[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [editingTestCase, setEditingTestCase] = useState<TestCaseDto | null>(null);
  const [editInput, setEditInput] = useState("");
  const [editExpectedOutput, setEditExpectedOutput] = useState("");
  const [editArgumentsJson, setEditArgumentsJson] = useState("");
  const [editExpectedJson, setEditExpectedJson] = useState("");
  const [editVisibility, setEditVisibility] = useState<TestCaseVisibility>(2);
  const [editScore, setEditScore] = useState(100);
  const functionSpec = useMemo(() => parseFunctionSpec(problem?.functionSpecJson), [problem?.functionSpecJson]);
  const importExample = useMemo(() => buildImportExample(problem, functionSpec), [problem, functionSpec]);

  async function loadProblem(problemId: string) {
    const item = await getProblem(problemId);
    setProblem(item);
  }

  useEffect(() => {
    if (!id) {
      return;
    }

    loadProblem(id).catch((err: unknown) => setError(err instanceof Error ? err.message : "题目加载失败"));
  }, [id]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!id) {
      return;
    }

    setError(null);
    setNotice(null);

    if (problem?.judgeMode === 2 && (!isValidJson(argumentsJson) || !isValidJson(expectedJson))) {
      setError("参数 JSON 和期望返回 JSON 必须是合法 JSON");
      return;
    }

    try {
      await addTestCase(id, {
        input: problem?.judgeMode === 1 ? input : "",
        expectedOutput: problem?.judgeMode === 1 ? expectedOutput : "",
        argumentsJson: problem?.judgeMode === 2 ? argumentsJson : null,
        expectedJson: problem?.judgeMode === 2 ? expectedJson : null,
        visibility,
        score
      });
      setInput("");
      setExpectedOutput("");
      setArgumentsJson("");
      setExpectedJson("");
      setVisibility(2);
      await loadProblem(id);
      setNotice("测试用例已添加。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "添加测试用例失败");
    }
  }

  async function handleImport() {
    if (!id) {
      return;
    }

    setError(null);
    setNotice(null);
    setImportErrors([]);

    let parsed: unknown;
    try {
      parsed = JSON.parse(importText);
    } catch {
      setError("批量导入内容必须是合法 JSON 数组。");
      return;
    }

    if (!Array.isArray(parsed)) {
      setError("批量导入内容必须是 JSON 数组。");
      return;
    }

    try {
      const result = await importTestCases(id, { items: parsed });
      await loadProblem(id);
      setImportText("");
      setShowImport(false);
      setNotice(`已导入 ${result.importedCount} 个测试点。`);
    } catch (err) {
      const parsedErrors = parseImportErrors(err);
      if (parsedErrors.length > 0) {
        setImportErrors(parsedErrors);
      }
      setError(err instanceof Error ? parseImportMessage(err.message) : "批量导入失败");
    }
  }

  async function handleExport() {
    if (!id) {
      return;
    }

    setError(null);
    setNotice(null);

    try {
      const file = await exportTestCases(id);
      const url = URL.createObjectURL(file.blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = file.fileName;
      anchor.click();
      URL.revokeObjectURL(url);
      setNotice("测试点 JSON 已导出。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "导出测试点失败");
    }
  }

  function beginEdit(testCase: TestCaseDto) {
    setEditingTestCase(testCase);
    setEditInput(testCase.input);
    setEditExpectedOutput(testCase.expectedOutput);
    setEditArgumentsJson(testCase.argumentsJson || "");
    setEditExpectedJson(testCase.expectedJson || "");
    setEditVisibility(testCase.visibility);
    setEditScore(testCase.score);
    setError(null);
    setNotice(null);
  }

  async function handleUpdate() {
    if (!id || !problem || !editingTestCase) {
      return;
    }

    if (problem.judgeMode === 2 && !isValidJson(editArgumentsJson)) {
      setError("Arguments JSON 格式无效");
      return;
    }

    if (problem.judgeMode === 2 && !isValidJson(editExpectedJson)) {
      setError("Expected JSON 格式无效");
      return;
    }

    setError(null);
    setNotice(null);
    try {
      await updateTestCase(id, editingTestCase.id, {
        input: problem.judgeMode === 1 ? editInput : "",
        expectedOutput: problem.judgeMode === 1 ? editExpectedOutput : "",
        argumentsJson: problem.judgeMode === 2 ? editArgumentsJson : null,
        expectedJson: problem.judgeMode === 2 ? editExpectedJson : null,
        visibility: editVisibility,
        score: editScore
      });
      setEditingTestCase(null);
      await loadProblem(id);
      setNotice("测试用例已更新。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "更新测试用例失败");
    }
  }

  async function handleDelete(testCaseId: string) {
    if (!id || !window.confirm("确定删除该测试点吗？删除后不会影响历史提交，但不会再参与未来判题。")) {
      return;
    }

    setError(null);
    setNotice(null);
    try {
      await deleteTestCase(id, testCaseId);
      if (editingTestCase?.id === testCaseId) {
        setEditingTestCase(null);
      }
      await loadProblem(id);
      setNotice("测试用例已删除。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "删除测试用例失败");
    }
  }

  function fillImportExample() {
    setImportText(importExample);
    setShowImport(true);
  }

  if (error && !problem) {
    return <div className="alert error">{error}</div>;
  }

  if (!problem) {
    return <div className="state-line">正在加载题目...</div>;
  }

  return (
    <section className="page-section two-column test-case-editor-page ui-v2-page testcase-editor-v2-page">
      <article>
        <div className="page-header compact ui-v2-page-header">
          <div>
            <p className="eyebrow">TEST CASES</p>
            <h1>测试用例配置</h1>
            <p>{problem.title}</p>
          </div>
          <div className="button-row">
            <button className="button" type="button" onClick={() => setShowImport((value) => !value)}>
              批量导入
            </button>
            <button className="button" type="button" onClick={handleExport}>
              导出 JSON
            </button>
            <Link className="button" to={`/admin/problems/${problem.id}/edit`}>
              编辑题目
            </Link>
            <Link className="button" to={`/problems/${problem.id}`}>
              查看题目
            </Link>
          </div>
        </div>

        {problem.judgeMode === 2 && (
          <section className="quiet-note function-helper">
            <strong>函数式测试点说明</strong>
            <p>{functionSpec ? formatSignature(functionSpec) : "函数配置暂不可用，请检查 FunctionSpecJson。"}</p>
            <p>ArgumentsJson 必须包含函数签名中的全部参数，不允许额外字段；ExpectedJson 表示期望返回值。</p>
            {hasType(functionSpec, "ListNode<int>") && <p>链表使用数组表示，例如 [1,2,3]；空链表使用 []。</p>}
            {hasType(functionSpec, "TreeNode<int>") && <p>二叉树使用层序数组表示，例如 [1,2,3,null,4]；空树使用 []。</p>}
            {(functionSpec?.types?.length || 0) > 0 && (
              <>
                <p>自定义结构使用 JSON 对象，字段必须与类型定义完全一致：</p>
                {functionSpec?.types?.map((type) => (
                  <p key={type.name}>
                    {type.name}: {"{ "}
                    {type.fields.map((field) => `${field.name}: ${field.type}`).join(", ")}
                    {" }"}
                  </p>
                ))}
              </>
            )}
          </section>
        )}

        {showImport && (
          <section className="content-block import-panel">
            <div className="section-heading-row">
              <h2>批量导入测试点</h2>
              <button className="button" type="button" onClick={fillImportExample}>
                填入示例
              </button>
            </div>
            <textarea className="import-textarea" value={importText} onChange={(event) => setImportText(event.target.value)} placeholder={importExample} />
            <div className="quiet-note">visibility 默认 Hidden。导入采用事务化策略，任一测试点校验失败时不会写入任何测试点。</div>
            {importErrors.length > 0 && (
              <div className="alert error">
                {importErrors.map((item) => (
                  <div key={`${item.index}-${item.field}-${item.message}`}>
                    第 {item.index} 条 / {item.field}: {item.message}
                  </div>
                ))}
              </div>
            )}
            <div className="button-row">
              <button className="button primary" type="button" onClick={handleImport}>
                确认导入
              </button>
              <button className="button" type="button" onClick={() => setShowImport(false)}>
                收起
              </button>
            </div>
          </section>
        )}

        {notice && <div className="quiet-note success">{notice}</div>}
        {error && <div className="alert error">{error}</div>}

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                {problem.judgeMode === 1 ? (
                  <>
                    <th>输入</th>
                    <th>期望输出</th>
                  </>
                ) : (
                  <>
                    <th>参数 JSON</th>
                    <th>期望返回 JSON</th>
                  </>
                )}
                <th>可见性</th>
                <th>分数</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {problem.testCases.map((testCase) => (
                <tr key={testCase.id}>
                  {editingTestCase?.id === testCase.id ? (
                    problem.judgeMode === 1 ? (
                      <>
                        <td><textarea value={editInput} onChange={(event) => setEditInput(event.target.value)} /></td>
                        <td><textarea value={editExpectedOutput} onChange={(event) => setEditExpectedOutput(event.target.value)} /></td>
                      </>
                    ) : (
                      <>
                        <td><textarea value={editArgumentsJson} onChange={(event) => setEditArgumentsJson(event.target.value)} /></td>
                        <td><textarea value={editExpectedJson} onChange={(event) => setEditExpectedJson(event.target.value)} /></td>
                      </>
                    )
                  ) : problem.judgeMode === 1 ? (
                    <>
                      <td className="pre-line">{testCase.input}</td>
                      <td className="pre-line">{testCase.expectedOutput}</td>
                    </>
                  ) : (
                    <>
                      <td className="pre-line">{formatJsonString(testCase.argumentsJson)}</td>
                      <td className="pre-line">{formatJsonString(testCase.expectedJson)}</td>
                    </>
                  )}
                  {editingTestCase?.id === testCase.id ? (
                    <>
                      <td>
                        <select value={editVisibility} onChange={(event) => setEditVisibility(Number(event.target.value) as TestCaseVisibility)}>
                          <option value={1}>示例</option>
                          <option value={2}>隐藏</option>
                        </select>
                      </td>
                      <td><input type="number" min={0} value={editScore} onChange={(event) => setEditScore(Number(event.target.value))} /></td>
                      <td>
                        <div className="button-row test-case-actions">
                          <button className="button primary" type="button" onClick={handleUpdate}>保存</button>
                          <button className="button" type="button" onClick={() => setEditingTestCase(null)}>取消</button>
                        </div>
                      </td>
                    </>
                  ) : (
                    <>
                      <td>
                        <span className={`visibility-badge visibility-${testCase.visibility}`}>{visibilityLabel(testCase.visibility)}</span>
                      </td>
                      <td>{testCase.score}</td>
                      <td>
                        <div className="button-row test-case-actions">
                          <button className="button" type="button" onClick={() => beginEdit(testCase)}>编辑</button>
                          <button className="button danger" type="button" onClick={() => handleDelete(testCase.id)}>删除</button>
                        </div>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
          {problem.testCases.length === 0 && <div className="empty-state">暂无测试用例</div>}
        </div>
      </article>

      <aside className="submit-panel">
        <h2>添加测试用例</h2>
        <form className="form-stack" onSubmit={handleSubmit}>
          {problem.judgeMode === 1 ? (
            <>
              <label>
                输入
                <textarea value={input} onChange={(event) => setInput(event.target.value)} />
              </label>
              <label>
                期望输出
                <textarea value={expectedOutput} onChange={(event) => setExpectedOutput(event.target.value)} />
              </label>
            </>
          ) : (
            <>
              <label>
                参数 JSON
                <textarea value={argumentsJson} onChange={(event) => setArgumentsJson(event.target.value)} placeholder={buildArgumentsTemplate(functionSpec)} />
              </label>
              <label>
                期望返回 JSON
                <textarea value={expectedJson} onChange={(event) => setExpectedJson(event.target.value)} placeholder={buildExpectedTemplate(functionSpec)} />
              </label>
            </>
          )}
          <label>
            可见性
            <select value={visibility} onChange={(event) => setVisibility(Number(event.target.value) as TestCaseVisibility)}>
              <option value={1}>示例</option>
              <option value={2}>隐藏</option>
            </select>
          </label>
          <label>
            分数
            <input type="number" min={0} value={score} onChange={(event) => setScore(Number(event.target.value))} />
          </label>
          <button className="button primary" type="submit">
            添加测试用例
          </button>
        </form>
      </aside>
    </section>
  );
}

function isValidJson(value: string) {
  if (!value.trim()) {
    return false;
  }

  try {
    JSON.parse(value);
    return true;
  } catch {
    return false;
  }
}

function parseFunctionSpec(functionSpecJson?: string | null): FunctionSpec | null {
  if (!functionSpecJson) {
    return null;
  }

  try {
    const parsed = JSON.parse(functionSpecJson) as FunctionSpec;
    return parsed?.functionName && parsed?.returnType && Array.isArray(parsed.parameters) ? parsed : null;
  } catch {
    return null;
  }
}

function formatSignature(spec: FunctionSpec) {
  const parameters = spec.parameters.map((parameter) => `${parameter.name}: ${parameter.type}`).join(", ");
  return `${spec.functionName}(${parameters}) -> ${spec.returnType}`;
}

function hasType(spec: FunctionSpec | null, type: string) {
  return spec?.returnType === type || Boolean(spec?.parameters.some((parameter) => parameter.type === type));
}

function buildImportExample(problem: ProblemDetailDto | null, spec: FunctionSpec | null) {
  if (!problem || problem.judgeMode === 1) {
    return JSON.stringify([
      { input: "1 2", expectedOutput: "3", score: 100, visibility: "Sample" },
      { input: "10 20", expectedOutput: "30", score: 100, visibility: "Hidden" }
    ], null, 2);
  }

  return JSON.stringify([
    {
      argumentsJson: JSON.parse(buildArgumentsTemplate(spec)),
      expectedJson: JSON.parse(buildExpectedTemplate(spec)),
      score: 100,
      visibility: "Sample"
    }
  ], null, 2);
}

function buildArgumentsTemplate(spec: FunctionSpec | null) {
  const template: Record<string, unknown> = {};
  spec?.parameters.forEach((parameter) => {
    template[parameter.name] = sampleValueForType(parameter.type, spec);
  });
  return JSON.stringify(template, null, 2);
}

function buildExpectedTemplate(spec: FunctionSpec | null) {
  return JSON.stringify(sampleValueForType(spec?.returnType || "int", spec), null, 2);
}

function sampleValueForType(type: string, spec: FunctionSpec | null, depth = 0): unknown {
  if (depth > 8) {
    return null;
  }

  const customTypes = spec?.types || [];
  const customType = customTypes.find((item) => item.name === type);
  if (customType) {
    return Object.fromEntries(customType.fields.map((field) => [field.name, sampleValueForType(field.type, spec, depth + 1)]));
  }

  if (type.endsWith("[]")) {
    const elementType = type.slice(0, -2);
    if (customTypes.some((item) => item.name === elementType)) {
      return [sampleValueForType(elementType, spec, depth + 1)];
    }
  }

  switch (type) {
    case "int":
    case "long":
      return 1;
    case "double":
      return 1.5;
    case "bool":
      return true;
    case "string":
      return "abc";
    case "ListNode<int>":
      return [1, 2, 3];
    case "TreeNode<int>":
      return [1, 2, 3, null, 4];
    case "int[][]":
      return [[1, 2], [3, 4]];
    default:
      return type.endsWith("[]") ? [1, 2, 3] : null;
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

function parseImportErrors(error: unknown): ImportTestCaseError[] {
  if (!(error instanceof Error)) {
    return [];
  }

  try {
    const parsed = JSON.parse(error.message) as { errors?: ImportTestCaseError[] };
    return Array.isArray(parsed.errors) ? parsed.errors : [];
  } catch {
    return [];
  }
}

function parseImportMessage(message: string) {
  try {
    const parsed = JSON.parse(message) as { message?: string };
    return parsed.message || message;
  } catch {
    return message;
  }
}
