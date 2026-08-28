import { ChangeEvent, FormEvent, useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { createProblem, deleteJudgeAsset, getJudgeAssets, getProblem, updateProblem, uploadJudgeAsset, type JudgeLanguage, type JudgeMode, type ProblemDetailDto, type ProblemJudgeAssetDto } from "../api/problemsApi";
import { MarkdownEditor } from "../components/MarkdownEditor";

interface FunctionParameterEditor {
  name: string;
  type: string;
}

interface FunctionCustomTypeEditor {
  name: string;
  fields: FunctionParameterEditor[];
}

const baseFunctionTypes = ["int", "long", "double", "bool", "string", "int[]", "long[]", "double[]", "bool[]", "string[]", "int[][]", "ListNode<int>", "TreeNode<int>"];
const customFieldPrimitiveTypes = ["int", "long", "double", "bool", "string"];
const allLanguageMask = 0b111;
const languageOptions = [
  { mask: 0b001, label: "C++" },
  { mask: 0b010, label: "C" },
  { mask: 0b100, label: "C#" }
] as const;

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
  const [isLanguageRestricted, setIsLanguageRestricted] = useState(false);
  const [allowedLanguagesMask, setAllowedLanguagesMask] = useState(allLanguageMask);
  const [functionName, setFunctionName] = useState("");
  const [returnType, setReturnType] = useState("int");
  const [parameters, setParameters] = useState<FunctionParameterEditor[]>([]);
  const [customTypes, setCustomTypes] = useState<FunctionCustomTypeEditor[]>([]);
  const [cpp17StarterCode, setCpp17StarterCode] = useState(defaultCpp17StarterCode("solve", "int", [], []));
  const [c11StarterCode, setC11StarterCode] = useState(defaultC11StarterCode("solve", "int", [], []));
  const [csharpStarterCode, setCSharpStarterCode] = useState(defaultCSharpStarterCode("solve", "int", [], []));
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(isEditMode);
  const [isSaving, setIsSaving] = useState(false);
  const [judgeAssets, setJudgeAssets] = useState<ProblemJudgeAssetDto[]>([]);
  const [uploadingLanguage, setUploadingLanguage] = useState<JudgeLanguage | null>(null);
  const [deletingAssetId, setDeletingAssetId] = useState<string | null>(null);
  const [judgeAssetError, setJudgeAssetError] = useState<string | null>(null);

  const functionTypes = useMemo(() => buildFunctionTypes(customTypes), [customTypes]);
  const customFieldTypes = useMemo(() => buildCustomFieldTypes(customTypes), [customTypes]);

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
          setIsLanguageRestricted(detail.allowedLanguagesMask !== 0);
          setAllowedLanguagesMask(detail.allowedLanguagesMask || allLanguageMask);
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

    getJudgeAssets(id)
      .then((assets) => {
        if (!ignore) {
          setJudgeAssets(assets);
          setJudgeAssetError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setJudgeAssetError(err instanceof Error ? err.message : "隐藏编译文件加载失败");
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
    if (functionConfig.isValid === false) {
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
      allowedLanguagesMask: selectedAllowedLanguagesMask,
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
    <section className="page-section narrow problem-editor-page ui-v2-page editor-v2-page problem-editor-v2-page">
      <div className="page-header ui-v2-page-header">
        <div>
          <p className="eyebrow">PROBLEM EDITOR</p>
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
          <>
            <MarkdownEditor label="输入说明" value={inputDescription} onChange={setInputDescription} required />
            <MarkdownEditor label="输出说明" value={outputDescription} onChange={setOutputDescription} required />
            <section className="content-block">
              <h2>隐藏编译文件</h2>
              <p className="muted-text">这些文件仅在服务器编译提交时使用，不会展示给答题人。</p>
              {renderJudgeAssetUpload(1, "C++17", ".cpp,.cc,.cxx,.h,.hpp")}
              {renderJudgeAssetUpload(2, "C11", ".c,.h")}
              {renderJudgeAssetUpload(3, "C#", ".cs")}
            </section>
          </>
        ) : (
          <section className="content-block">
            <h2>函数配置</h2>
            <p className="muted-text">函数式题目支持 C++17、C# 和 C11。自定义结构类型使用 JSON 对象表示，可作为参数、返回值或一维数组元素。</p>
            <p className="quiet-note">自定义结构字段当前支持 int / long / double / bool / string 或其他自定义结构；字段内部暂不支持数组。C11 自定义结构字段不支持 string。</p>
            {hasListNodeType(returnType, parameters) && (
              <p className="quiet-note">链表类型在测试用例中使用数组表示，例如 [1,2,3]；空数组 [] 表示空链表。C11 暂不支持 ListNode&lt;int&gt;。</p>
            )}
            {hasTreeNodeType(returnType, parameters) && (
              <p className="quiet-note">二叉树类型在测试用例中使用层序数组表示，例如 [1,2,3,null,4]；空数组 [] 表示空树。C11 暂不支持 TreeNode&lt;int&gt;。</p>
            )}

            <section className="content-block">
              <div className="section-heading-row">
                <div>
                  <h3>自定义结构类型</h3>
                  <p className="muted-text">例如先定义 Point，再定义 Triangle；Triangle 字段可以引用 Point。</p>
                </div>
                <button className="button" type="button" onClick={addCustomType}>
                  添加结构类型
                </button>
              </div>

              {customTypes.map((customType, typeIndex) => (
                <div className="content-block" key={typeIndex}>
                  <div className="form-row">
                    <label>
                      类型名
                      <input
                        value={customType.name}
                        onChange={(event) => updateCustomType(typeIndex, { ...customType, name: event.target.value })}
                        placeholder="Triangle"
                      />
                    </label>
                    <div className="button-row">
                      <button className="button" type="button" onClick={() => addCustomField(typeIndex)}>
                        添加字段
                      </button>
                      <button className="button danger" type="button" onClick={() => removeCustomType(typeIndex)}>
                        删除类型
                      </button>
                    </div>
                  </div>

                  <div className="table-wrap">
                    <table>
                      <thead>
                        <tr>
                          <th>字段名</th>
                          <th>字段类型</th>
                          <th>操作</th>
                        </tr>
                      </thead>
                      <tbody>
                        {customType.fields.map((field, fieldIndex) => (
                          <tr key={fieldIndex}>
                            <td>
                              <input value={field.name} onChange={(event) => updateCustomField(typeIndex, fieldIndex, { ...field, name: event.target.value })} />
                            </td>
                            <td>
                              <select value={field.type} onChange={(event) => updateCustomField(typeIndex, fieldIndex, { ...field, type: event.target.value })}>
                                {ensureSelectedType(customFieldTypes, field.type).map((type) => (
                                  <option key={type} value={type}>
                                    {type}
                                  </option>
                                ))}
                              </select>
                            </td>
                            <td>
                              <button className="button" type="button" onClick={() => removeCustomField(typeIndex, fieldIndex)}>
                                删除
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    {customType.fields.length === 0 && <div className="empty-state">至少添加一个字段</div>}
                  </div>
                </div>
              ))}

              {customTypes.length === 0 && <div className="empty-state">未定义自定义结构类型；原有基础类型行为保持不变。</div>}
            </section>

            <div className="form-row">
              <label>
                函数名
                <input value={functionName} onChange={(event) => setFunctionName(event.target.value)} placeholder="solve" required />
              </label>
              <label>
                返回类型
                <select value={returnType} onChange={(event) => setReturnType(event.target.value)}>
                  {ensureSelectedType(functionTypes, returnType).map((type) => (
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
                          {ensureSelectedType(functionTypes, parameter.type).map((type) => (
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

            {renderJudgeAssetUpload(1, "C++17", ".cpp,.cc,.cxx,.h,.hpp")}
            <label>
              C++17 初始代码模板
              <textarea className="code-area" value={cpp17StarterCode} onChange={(event) => setCpp17StarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setCpp17StarterCode(defaultCpp17StarterCode(functionName || "solve", returnType, parameters, customTypes))}>
              根据函数配置生成模板
            </button>

            {renderJudgeAssetUpload(2, "C11", ".c,.h")}
            <label>
              C11 初始代码模板
              <textarea className="code-area" value={c11StarterCode} onChange={(event) => setC11StarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setC11StarterCode(defaultC11StarterCode(functionName || "solve", returnType, parameters, customTypes))}>
              根据函数配置生成 C11 模板
            </button>

            {renderJudgeAssetUpload(3, "C#", ".cs")}
            <label>
              C# 初始代码模板
              <textarea className="code-area" value={csharpStarterCode} onChange={(event) => setCSharpStarterCode(event.target.value)} spellCheck={false} />
            </label>
            <button className="button" type="button" onClick={() => setCSharpStarterCode(defaultCSharpStarterCode(functionName || "solve", returnType, parameters, customTypes))}>
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

  function renderJudgeAssetUpload(language: JudgeLanguage, label: string, accept: string) {
    const assets = judgeAssets.filter((asset) => asset.language === language);
    return (
      <div className="judge-asset-panel">
        <div className="judge-asset-heading">
          <strong>{label} 隐藏编译文件</strong>
          <input
            className="judge-asset-file-input"
            type="file"
            accept={accept}
            disabled={!id || uploadingLanguage !== null}
            onChange={(event) => void handleJudgeAssetUpload(language, event)}
          />
        </div>
        {!id && <p className="judge-asset-hint">请先保存题目后上传隐藏编译文件。</p>}
        {judgeAssetError && <p className="judge-asset-hint error-text">{judgeAssetError}</p>}
        {assets.length > 0 && (
          <ul className="judge-asset-list">
            {assets.map((asset) => (
              <li key={asset.id}>
                <span title={asset.sha256}>{asset.originalFileName}</span>
                <small>{formatFileSize(asset.fileSizeBytes)}</small>
                <button className="button danger" type="button" disabled={deletingAssetId === asset.id} onClick={() => void handleDeleteJudgeAsset(asset)}>
                  {deletingAssetId === asset.id ? "删除中..." : "删除"}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }

  async function handleJudgeAssetUpload(language: JudgeLanguage, event: ChangeEvent<HTMLInputElement>) {
    const input = event.currentTarget;
    const file = input.files?.[0];
    if (!id || !file) {
      return;
    }

    setUploadingLanguage(language);
    setError(null);
    setNotice(null);
    try {
      await uploadJudgeAsset(id, language, file);
      setJudgeAssets(await getJudgeAssets(id));
      setJudgeAssetError(null);
      setNotice("隐藏编译文件已上传。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "隐藏编译文件上传失败");
    } finally {
      input.value = "";
      setUploadingLanguage(null);
    }
  }

  async function handleDeleteJudgeAsset(asset: ProblemJudgeAssetDto) {
    if (!id || !window.confirm(`确认删除隐藏编译文件 ${asset.originalFileName}？`)) {
      return;
    }

    setDeletingAssetId(asset.id);
    setError(null);
    setNotice(null);
    try {
      await deleteJudgeAsset(id, asset.id);
      setJudgeAssets(await getJudgeAssets(id));
      setJudgeAssetError(null);
      setNotice("隐藏编译文件已删除。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "隐藏编译文件删除失败");
    } finally {
      setDeletingAssetId(null);
    }
  }

  function updateParameter(index: number, parameter: FunctionParameterEditor) {
    setParameters((current) => current.map((item, itemIndex) => (itemIndex === index ? parameter : item)));
  }

  function removeParameter(index: number) {
    setParameters((current) => current.filter((_, itemIndex) => itemIndex !== index));
  }

  function addCustomType() {
    setCustomTypes((current) => [...current, { name: "", fields: [{ name: "", type: "double" }] }]);
  }

  function updateCustomType(index: number, customType: FunctionCustomTypeEditor) {
    setCustomTypes((current) => current.map((item, itemIndex) => (itemIndex === index ? customType : item)));
  }

  function removeCustomType(index: number) {
    setCustomTypes((current) => current.filter((_, itemIndex) => itemIndex !== index));
  }

  function addCustomField(typeIndex: number) {
    setCustomTypes((current) => current.map((type, index) => index === typeIndex ? { ...type, fields: [...type.fields, { name: "", type: "double" }] } : type));
  }

  function updateCustomField(typeIndex: number, fieldIndex: number, field: FunctionParameterEditor) {
    setCustomTypes((current) => current.map((type, index) => index === typeIndex
      ? { ...type, fields: type.fields.map((item, itemIndex) => itemIndex === fieldIndex ? field : item) }
      : type));
  }

  function removeCustomField(typeIndex: number, fieldIndex: number) {
    setCustomTypes((current) => current.map((type, index) => index === typeIndex
      ? { ...type, fields: type.fields.filter((_, itemIndex) => itemIndex !== fieldIndex) }
      : type));
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

    const normalizedParameters = parameters.map((parameter) => ({ name: parameter.name.trim(), type: parameter.type }));
    if (normalizedParameters.some((parameter) => !parameter.name)) {
      return { isValid: false, error: "请填写完整的参数名" };
    }

    const parameterNames = new Set(normalizedParameters.map((parameter) => parameter.name));
    if (parameterNames.size !== normalizedParameters.length) {
      return { isValid: false, error: "参数名不能重复" };
    }

    const normalizedCustomTypes = normalizeCustomTypes(customTypes);
    const customValidation = validateCustomTypes(normalizedCustomTypes);
    if (customValidation) {
      return { isValid: false, error: customValidation };
    }

    const allowedTypes = new Set(buildFunctionTypes(normalizedCustomTypes));
    if (!allowedTypes.has(returnType)) {
      return { isValid: false, error: `不支持的返回类型：${returnType}` };
    }

    const invalidParameterType = normalizedParameters.find((parameter) => !allowedTypes.has(parameter.type));
    if (invalidParameterType) {
      return { isValid: false, error: `不支持的参数类型：${invalidParameterType.type}` };
    }

    if (!cpp17StarterCode.trim()) {
      return { isValid: false, error: "请填写 C++17 初始代码模板" };
    }

    if (!csharpStarterCode.trim()) {
      return { isValid: false, error: "请填写 C# 初始代码模板" };
    }

    const supportedLanguages = hasC11UnsupportedType(returnType, normalizedParameters, normalizedCustomTypes)
      ? ["cpp17", "csharp"]
      : ["cpp17", "csharp", "c11"];

    return {
      isValid: true,
      functionSpecJson: JSON.stringify({
        types: normalizedCustomTypes,
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
    let parsedCustomTypes: FunctionCustomTypeEditor[] = [];

    try {
      const spec = JSON.parse(detail.functionSpecJson || "{}") as {
        functionName?: string;
        returnType?: string;
        parameters?: FunctionParameterEditor[];
        types?: FunctionCustomTypeEditor[];
      };
      parsedFunctionName = spec.functionName || "solve";
      parsedReturnType = spec.returnType || "int";
      parsedParameters = Array.isArray(spec.parameters) ? spec.parameters.map((parameter) => ({ name: parameter.name, type: parameter.type })) : [];
      parsedCustomTypes = Array.isArray(spec.types)
        ? spec.types.map((type) => ({ name: type.name, fields: Array.isArray(type.fields) ? type.fields.map((field) => ({ name: field.name, type: field.type })) : [] }))
        : [];
      setFunctionName(parsedFunctionName);
      setReturnType(parsedReturnType);
      setParameters(parsedParameters);
      setCustomTypes(parsedCustomTypes);
    } catch {
      setFunctionName(parsedFunctionName);
      setReturnType(parsedReturnType);
      setParameters(parsedParameters);
      setCustomTypes(parsedCustomTypes);
    }

    try {
      const starter = JSON.parse(detail.starterCodeJson || "{}") as { cpp17?: string; c11?: string; csharp?: string };
      setCpp17StarterCode(starter.cpp17 || defaultCpp17StarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
      setC11StarterCode(starter.c11 || defaultC11StarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
      setCSharpStarterCode(starter.csharp || defaultCSharpStarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
    } catch {
      setCpp17StarterCode(defaultCpp17StarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
      setC11StarterCode(defaultC11StarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
      setCSharpStarterCode(defaultCSharpStarterCode(parsedFunctionName, parsedReturnType, parsedParameters, parsedCustomTypes));
    }
  }
}

function formatFileSize(bytes: number) {
  return bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toFixed(1)} KB`;
}

function buildFunctionTypes(customTypes: FunctionCustomTypeEditor[]) {
  const customNames = customTypes.map((type) => type.name.trim()).filter(Boolean);
  return [...baseFunctionTypes, ...customNames, ...customNames.map((name) => `${name}[]`)].filter((type, index, values) => values.indexOf(type) === index);
}

function buildCustomFieldTypes(customTypes: FunctionCustomTypeEditor[]) {
  const customNames = customTypes.map((type) => type.name.trim()).filter(Boolean);
  return [...customFieldPrimitiveTypes, ...customNames].filter((type, index, values) => values.indexOf(type) === index);
}

function ensureSelectedType(types: string[], selected: string) {
  return selected && !types.includes(selected) ? [...types, selected] : types;
}

function normalizeCustomTypes(customTypes: FunctionCustomTypeEditor[]) {
  return customTypes.map((type) => ({
    name: type.name.trim(),
    fields: type.fields.map((field) => ({ name: field.name.trim(), type: field.type }))
  }));
}

function validateCustomTypes(customTypes: FunctionCustomTypeEditor[]) {
  const identifier = /^[A-Za-z_][A-Za-z0-9_]*$/;
  const names = customTypes.map((type) => type.name);

  if (names.some((name) => !name || !identifier.test(name))) {
    return "自定义结构类型名必须是合法标识符";
  }

  if (new Set(names).size !== names.length) {
    return "自定义结构类型名不能重复";
  }

  const allowedFieldTypes = new Set([...customFieldPrimitiveTypes, ...names]);
  for (const type of customTypes) {
    if (type.fields.length === 0) {
      return `结构类型 ${type.name} 至少需要一个字段`;
    }

    const fieldNames = type.fields.map((field) => field.name);
    if (fieldNames.some((name) => !name || !identifier.test(name))) {
      return `结构类型 ${type.name} 存在非法字段名`;
    }

    if (new Set(fieldNames).size !== fieldNames.length) {
      return `结构类型 ${type.name} 的字段名不能重复`;
    }

    const invalidField = type.fields.find((field) => !allowedFieldTypes.has(field.type));
    if (invalidField) {
      return `结构类型 ${type.name}.${invalidField.name} 的字段类型不受支持：${invalidField.type}`;
    }
  }

  const map = new Map(customTypes.map((type) => [type.name, type]));
  const states = new Map<string, number>();

  function visit(name: string): boolean {
    const state = states.get(name);
    if (state === 1) {
      return false;
    }
    if (state === 2) {
      return true;
    }

    states.set(name, 1);
    const type = map.get(name);
    for (const field of type?.fields || []) {
      if (map.has(field.type) && !visit(field.type)) {
        return false;
      }
    }
    states.set(name, 2);
    return true;
  }

  return names.every(visit) ? null : "自定义结构类型不能形成循环依赖";
}

function defaultCpp17StarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[], customTypes: FunctionCustomTypeEditor[]) {
  const cppReturnType = toCppType(returnType, customTypes);
  const cppParameters = parameters.map((parameter) => `${toCppParameterType(parameter.type, customTypes)} ${parameter.name || "arg"}`).join(", ");
  const customPrefix = buildCppCustomTypeDefinitions(customTypes);
  const listNodePrefix = hasListNodeType(returnType, parameters)
    ? `struct ListNode {\n    int val;\n    ListNode* next;\n\n    ListNode() : val(0), next(nullptr) {}\n    ListNode(int x) : val(x), next(nullptr) {}\n    ListNode(int x, ListNode* next) : val(x), next(next) {}\n};\n\n`
    : "";
  const treeNodePrefix = hasTreeNodeType(returnType, parameters)
    ? `struct TreeNode {\n    int val;\n    TreeNode* left;\n    TreeNode* right;\n\n    TreeNode() : val(0), left(nullptr), right(nullptr) {}\n    TreeNode(int x) : val(x), left(nullptr), right(nullptr) {}\n    TreeNode(int x, TreeNode* left, TreeNode* right) : val(x), left(left), right(right) {}\n};\n\n`
    : "";

  return `${customPrefix}${listNodePrefix}${treeNodePrefix}class Solution {\npublic:\n    ${cppReturnType} ${functionName}(${cppParameters}) {\n        \n    }\n};`;
}

function defaultCSharpStarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[], customTypes: FunctionCustomTypeEditor[]) {
  const csharpReturnType = toCSharpType(returnType, customTypes);
  const csharpFunctionName = toCSharpMethodName(functionName);
  const csharpParameters = parameters.map((parameter) => `${toCSharpType(parameter.type, customTypes)} ${parameter.name || "arg"}`).join(", ");
  const customPrefix = buildCSharpCustomTypeDefinitions(customTypes);
  const listNodePrefix = hasListNodeType(returnType, parameters)
    ? `public class ListNode\n{\n    public int val;\n    public ListNode? next;\n\n    public ListNode(int val = 0, ListNode? next = null)\n    {\n        this.val = val;\n        this.next = next;\n    }\n}\n\n`
    : "";
  const treeNodePrefix = hasTreeNodeType(returnType, parameters)
    ? `public class TreeNode\n{\n    public int val;\n    public TreeNode? left;\n    public TreeNode? right;\n\n    public TreeNode(int val = 0, TreeNode? left = null, TreeNode? right = null)\n    {\n        this.val = val;\n        this.left = left;\n        this.right = right;\n    }\n}\n\n`
    : "";

  return `${customPrefix}${listNodePrefix}${treeNodePrefix}public class Solution\n{\n    public ${csharpReturnType} ${csharpFunctionName}(${csharpParameters})\n    {\n        \n    }\n}`;
}

function defaultC11StarterCode(functionName: string, returnType: string, parameters: FunctionParameterEditor[], customTypes: FunctionCustomTypeEditor[]) {
  if (hasC11UnsupportedType(returnType, parameters, customTypes)) {
    return "/* 当前函数签名或自定义结构字段包含 C11 Function Judge 尚不支持的类型。 */";
  }

  const customPrefix = buildC11CustomTypeDefinitions(customTypes);
  const cReturnType = toC11ReturnType(returnType, customTypes);
  const cParameters = parameters.flatMap((parameter) => toC11ParameterParts(parameter.type, parameter.name || "arg", customTypes));
  if (isC11ArrayType(returnType, customTypes)) {
    cParameters.push("int* returnSize");
  }

  return `${customPrefix}${cReturnType} ${functionName}(${cParameters.join(", ")}) {\n    \n}`;
}

function buildCppCustomTypeDefinitions(customTypes: FunctionCustomTypeEditor[]) {
  return sortCustomTypesForDeclaration(customTypes)
    .map((type) => `struct ${type.name} {\n${type.fields.map((field) => `    ${toCppType(field.type, customTypes)} ${field.name};`).join("\n")}\n};\n\n`)
    .join("");
}

function buildCSharpCustomTypeDefinitions(customTypes: FunctionCustomTypeEditor[]) {
  return sortCustomTypesForDeclaration(customTypes)
    .map((type) => `public class ${type.name}\n{\n${type.fields.map((field) => `    public ${toCSharpType(field.type, customTypes)} ${field.name};`).join("\n")}\n}\n\n`)
    .join("");
}

function buildC11CustomTypeDefinitions(customTypes: FunctionCustomTypeEditor[]) {
  return sortCustomTypesForDeclaration(customTypes)
    .map((type) => `typedef struct ${type.name} {\n${type.fields.map((field) => `    ${toC11ScalarType(field.type, customTypes)} ${field.name};`).join("\n")}\n} ${type.name};\n\n`)
    .join("");
}

function sortCustomTypesForDeclaration(customTypes: FunctionCustomTypeEditor[]) {
  const map = new Map(customTypes.map((type) => [type.name, type]));
  const visited = new Set<string>();
  const result: FunctionCustomTypeEditor[] = [];

  function visit(type: FunctionCustomTypeEditor) {
    if (visited.has(type.name)) {
      return;
    }

    visited.add(type.name);
    type.fields.forEach((field) => {
      const dependency = map.get(field.type);
      if (dependency) {
        visit(dependency);
      }
    });
    result.push(type);
  }

  customTypes.forEach(visit);
  return result;
}

function toCppType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  if (type === "ListNode<int>") {
    return "ListNode*";
  }

  if (type === "TreeNode<int>") {
    return "TreeNode*";
  }

  const customNames = new Set(customTypes.map((item) => item.name));
  if (customNames.has(type)) {
    return type;
  }

  if (type.endsWith("[]") && customNames.has(type.slice(0, -2))) {
    return `vector<${type.slice(0, -2)}>`;
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

function toCppParameterType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  const cppType = toCppType(type, customTypes);
  return type.endsWith("[]") ? `${cppType}&` : cppType;
}

function toCSharpType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  if (type === "ListNode<int>") {
    return "ListNode?";
  }

  if (type === "TreeNode<int>") {
    return "TreeNode?";
  }

  const customNames = new Set(customTypes.map((item) => item.name));
  if (customNames.has(type) || type.endsWith("[]") && customNames.has(type.slice(0, -2))) {
    return type;
  }

  return type;
}

function hasListNodeType(returnType: string, parameters: FunctionParameterEditor[]) {
  return returnType === "ListNode<int>" || parameters.some((parameter) => parameter.type === "ListNode<int>");
}

function hasTreeNodeType(returnType: string, parameters: FunctionParameterEditor[]) {
  return returnType === "TreeNode<int>" || parameters.some((parameter) => parameter.type === "TreeNode<int>");
}

function hasC11UnsupportedType(returnType: string, parameters: FunctionParameterEditor[], customTypes: FunctionCustomTypeEditor[]) {
  return !isC11SupportedType(returnType, customTypes)
    || parameters.some((parameter) => !isC11SupportedType(parameter.type, customTypes))
    || customTypes.some((type) => type.fields.some((field) => !isC11SupportedCustomFieldType(field.type, customTypes)));
}

function isC11SupportedType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  if (["int", "long", "double", "bool", "int[]", "long[]", "double[]"].includes(type)) {
    return true;
  }

  const customNames = new Set(customTypes.map((item) => item.name));
  return customNames.has(type) || type.endsWith("[]") && customNames.has(type.slice(0, -2));
}

function isC11SupportedCustomFieldType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  return ["int", "long", "double", "bool"].includes(type) || customTypes.some((item) => item.name === type);
}

function toCSharpMethodName(functionName: string) {
  return functionName ? `${functionName[0].toUpperCase()}${functionName.slice(1)}` : functionName;
}

function isC11ArrayType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  return ["int[]", "long[]", "double[]"].includes(type)
    || type.endsWith("[]") && customTypes.some((item) => item.name === type.slice(0, -2));
}

function toC11ReturnType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  if (isC11ArrayType(type, customTypes)) {
    return `${type.slice(0, -2)}*`;
  }

  return toC11ScalarType(type, customTypes);
}

function toC11ScalarType(type: string, customTypes: FunctionCustomTypeEditor[]) {
  if (["int", "long", "double", "bool"].includes(type) || customTypes.some((item) => item.name === type)) {
    return type;
  }

  return type;
}

function toC11ParameterParts(type: string, name: string, customTypes: FunctionCustomTypeEditor[]) {
  if (isC11ArrayType(type, customTypes)) {
    return [`${type.slice(0, -2)}* ${name}`, `int ${name}Size`];
  }

  return [`${toC11ScalarType(type, customTypes)} ${name}`];
}
