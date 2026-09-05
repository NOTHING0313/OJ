import { ChangeEvent, FormEvent, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { createHelpDocument, getAdminHelpDocument, publishHelpDocument, updateHelpDocument, type HelpDocument, type HelpDocumentRequest } from "../api/helpDocumentsApi";
import { getApiErrorMessage } from "../api/httpClient";
import { HelpMarkdown } from "../components/help/HelpMarkdown";

const MaxImportBytes = 1024 * 1024;

export function HelpDocumentEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [title, setTitle] = useState("");
  const [slug, setSlug] = useState("");
  const [summary, setSummary] = useState("");
  const [sortOrder, setSortOrder] = useState(0);
  const [markdownContent, setMarkdownContent] = useState("");
  const [isPublished, setIsPublished] = useState(false);
  const [slugEdited, setSlugEdited] = useState(false);
  const [mobileMode, setMobileMode] = useState<"edit" | "preview">("edit");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    let ignore = false;
    getAdminHelpDocument(id).then((document) => {
      if (ignore) return;
      setTitle(document.title);
      setSlug(document.slug);
      setSummary(document.summary ?? "");
      setSortOrder(document.sortOrder);
      setMarkdownContent(document.markdownContent);
      setIsPublished(document.isPublished);
      setSlugEdited(true);
    }).catch((err) => { if (!ignore) setError(getApiErrorMessage(err, "加载文档失败，请稍后重试。")); });
    return () => { ignore = true; };
  }, [id]);

  function handleTitleChange(value: string) {
    setTitle(value);
    if (!slugEdited) setSlug(toSlug(value));
  }

  function payload(): HelpDocumentRequest {
    return { title, slug, summary: summary.trim() || null, markdownContent, sortOrder };
  }

  async function save(): Promise<HelpDocument> {
    return id ? updateHelpDocument(id, payload()) : createHelpDocument(payload());
  }

  async function handleSave(event: FormEvent) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      await save();
      navigate("/help/manage");
    } catch (err) {
      setError(getApiErrorMessage(err, "保存文档失败，请稍后重试。"));
    } finally {
      setIsSaving(false);
    }
  }

  async function handlePublish() {
    if (!title.trim() || !slug.trim() || !markdownContent.trim()) {
      setError("发布前请填写标题、Slug 和 Markdown 正文。");
      return;
    }
    setIsSaving(true);
    setError(null);
    try {
      const saved = await save();
      await publishHelpDocument(saved.id);
      navigate("/help/manage");
    } catch (err) {
      setError(getApiErrorMessage(err, "发布失败，请稍后重试。"));
    } finally {
      setIsSaving(false);
    }
  }

  async function handleImport(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const isMarkdown = file.name.toLowerCase().endsWith(".md") || file.type === "text/markdown" || file.type === "text/plain";
    if (!isMarkdown) { setError("只支持 .md 或纯文本文件。"); return; }
    if (file.size > MaxImportBytes) { setError("Markdown 文件不能超过 1 MB。"); return; }
    setMarkdownContent(await file.text());
    setError(null);
  }

  return (
    <section className="help-editor-page">
      <header className="page-header help-center-header">
        <div><h1>{id ? "编辑文档" : "新建文档"}</h1><p>左侧编辑 Markdown，右侧即时预览。</p></div>
        <Link className="button" to="/help/manage">返回文档管理</Link>
      </header>
      {error && <div className="alert error" role="alert">{error}</div>}
      <form onSubmit={handleSave}>
        <div className="help-editor-meta">
          <label>标题<input value={title} maxLength={120} onChange={(event) => handleTitleChange(event.target.value)} required /></label>
          <label>Slug<input value={slug} maxLength={120} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" onChange={(event) => { setSlug(event.target.value); setSlugEdited(true); }} required /></label>
          <label>摘要<input value={summary} maxLength={300} onChange={(event) => setSummary(event.target.value)} /></label>
          <label>排序<input type="number" min={-100000} max={100000} value={sortOrder} onChange={(event) => setSortOrder(Number(event.target.value))} /></label>
        </div>
        <div className="help-editor-toolbar">
          <div className="help-editor-tabs" role="tablist">
            <button type="button" className={mobileMode === "edit" ? "active" : ""} onClick={() => setMobileMode("edit")}>编辑</button>
            <button type="button" className={mobileMode === "preview" ? "active" : ""} onClick={() => setMobileMode("preview")}>预览</button>
          </div>
          <label className="button help-import-button">导入 .md<input type="file" accept=".md,text/markdown,text/plain" onChange={(event) => void handleImport(event)} /></label>
          <span className={`help-status ${isPublished ? "published" : "draft"}`}>{isPublished ? "已发布" : "草稿"}</span>
        </div>
        <div className={`help-editor-split mode-${mobileMode}`}>
          <label className="help-editor-pane edit-pane">Markdown 正文<textarea value={markdownContent} maxLength={200000} onChange={(event) => setMarkdownContent(event.target.value)} placeholder="# 从这里开始编写帮助文档" /></label>
          <section className="help-editor-pane preview-pane" aria-label="Markdown 预览"><HelpMarkdown>{markdownContent || "*暂无预览内容*"}</HelpMarkdown></section>
        </div>
        <div className="help-editor-actions">
          <button className="button" type="submit" disabled={isSaving}>{isSaving ? "保存中..." : "保存草稿"}</button>
          <button className="button primary" type="button" disabled={isSaving} onClick={() => void handlePublish()}>发布</button>
        </div>
      </form>
    </section>
  );
}

function toSlug(value: string) {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "").slice(0, 120);
}
