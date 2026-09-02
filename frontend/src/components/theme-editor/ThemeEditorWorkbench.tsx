import { createContext, type ChangeEvent, type DragEvent, type ReactNode, useCallback, useContext, useEffect, useMemo, useReducer, useRef, useState } from "react";
import {
  applyThemePreset,
  createThemePreset,
  createDefaultSiteAppearance,
  deleteThemeAsset,
  deleteThemePreset,
  duplicateThemePreset,
  exportThemePreset,
  importThemePreset,
  listThemeAssets,
  listThemePresets,
  normalizeSiteAppearance,
  preflightThemePresetImport,
  renameThemeAsset,
  renameThemePreset,
  resolveSiteAssetUrl,
  sitePageOptions,
  updateSiteAppearance,
  updateThemePreset,
  uploadThemeAsset,
  type SiteAppearance,
  type SiteAppearanceTheme,
  type SitePanelSkin,
  type SitePageKey,
  type SiteThemeBackground,
  type SiteThemeDecorationSlot,
  type SiteThemeIconSlot,
  type ThemeAssetLibraryItem,
  type ThemeAssetReference,
  type ThemePackPreflight,
  type ThemePreset,
  type ThemePresetList
} from "../../api/siteSettingsApi";
import { getApiErrorMessage } from "../../api/httpClient";
import { uploadImage } from "../../api/uploadsApi";
import { useTheme } from "../../theme/ThemeContext";
import { type ThemeDecorationSlot, type ThemeIconSlot } from "../../theme/themeSlots";
import { normalizeUploadedImagePath } from "../../utils/uploadedImageUrl";
import { ThemeEditorPreview } from "./ThemeEditorPreview";
import { ThemeEditorDialog } from "./ThemeEditorDialog";
import {
  ThemeEditorHistoryLimit,
  appearanceEquals,
  createThemeEditorHistory,
  getPreviewPageKey,
  getSurfaceGroupLabel,
  getThemeSurface,
  getThemeSurfaceBreadcrumb,
  reduceThemeEditorHistory,
  resetThemeSurface,
  themeEditableSurfaces,
  themeEditorPreviewPages,
  themeEditorPreviewZooms,
  themeEditorViewports,
  type ThemeEditorCompareMode,
  type ThemeEditorMode,
  type ThemeEditorPreviewPage,
  type ThemeEditorPreviewZoom,
  type ThemeEditorSurfaceId,
  type ThemeEditorViewport
} from "./themeEditorModel";

const ThemeEditorGestureContext = createContext({ begin: () => {}, end: () => {} });

type PendingDraftAction =
  | { kind: "load"; preset: ThemePreset }
  | { kind: "apply"; preset: ThemePreset }
  | { kind: "navigate"; url: string }
  | { kind: "reset" };

export function ThemeEditorWorkbench() {
  const { siteAppearance, reloadSiteAppearance } = useTheme();
  const [history, dispatch] = useReducer(reduceThemeEditorHistory, siteAppearance, createThemeEditorHistory);
  const [draftCheckpoint, setDraftCheckpoint] = useState(siteAppearance);
  const [selectedSurface, setSelectedSurface] = useState<ThemeEditorSurfaceId>("global.background");
  const [previewPage, setPreviewPage] = useState<ThemeEditorPreviewPage>("problem");
  const [pageBackgroundKey, setPageBackgroundKey] = useState<SitePageKey>("problems");
  const [viewport, setViewport] = useState<ThemeEditorViewport>("desktop");
  const [previewZoom, setPreviewZoom] = useState<ThemeEditorPreviewZoom>("fit");
  const [editorMode, setEditorMode] = useState<ThemeEditorMode>("select");
  const [compareMode, setCompareMode] = useState<ThemeEditorCompareMode>("draft");
  const [surfaceSearch, setSurfaceSearch] = useState("");
  const [navigatorCollapsed, setNavigatorCollapsed] = useState(false);
  const [inspectorCollapsed, setInspectorCollapsed] = useState(false);
  const [focusMode, setFocusMode] = useState(false);
  const [pulseSurface, setPulseSurface] = useState<ThemeEditorSurfaceId | null>(null);
  const pulseTimerRef = useRef<number | null>(null);
  const [themeAssets, setThemeAssets] = useState<ThemeAssetLibraryItem[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showResetAll, setShowResetAll] = useState(false);
  const [showAssetLibrary, setShowAssetLibrary] = useState(false);
  const [themeLibrary, setThemeLibrary] = useState<ThemePresetList>({ items: [], lastAppliedPresetId: null });
  const [presetSearch, setPresetSearch] = useState("");
  const [presetSort, setPresetSort] = useState<"updated" | "name">("updated");
  const [selectedPresetId, setSelectedPresetId] = useState<string | null | undefined>(undefined);
  const [libraryBusy, setLibraryBusy] = useState(false);
  const [pendingDraftAction, setPendingDraftAction] = useState<PendingDraftAction | null>(null);
  const [savePresetDialog, setSavePresetDialog] = useState<{ continuation: PendingDraftAction | null } | null>(null);
  const [applyPresetDialog, setApplyPresetDialog] = useState<ThemePreset | null>(null);
  const [importPresetDialog, setImportPresetDialog] = useState<{ file: File; preview: ThemePackPreflight } | null>(null);
  const [updatePresetDialog, setUpdatePresetDialog] = useState<ThemePreset | null>(null);
  const [renamePresetDialog, setRenamePresetDialog] = useState<ThemePreset | null>(null);
  const [deletePresetDialog, setDeletePresetDialog] = useState<ThemePreset | null>(null);
  const [renameAssetDialog, setRenameAssetDialog] = useState<ThemeAssetLibraryItem | null>(null);
  const [deleteAssetDialog, setDeleteAssetDialog] = useState<ThemeAssetLibraryItem | null>(null);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const dirty = !appearanceEquals(draftCheckpoint, history.present);
  const previewAppearance = compareMode === "draft" ? history.present : compareMode === "saved" ? draftCheckpoint : createDefaultSiteAppearance();
  const selected = getThemeSurface(selectedSurface);
  const filteredSurfaces = useMemo(() => {
    const query = surfaceSearch.trim().toLocaleLowerCase();
    return query.length === 0 ? themeEditableSurfaces : themeEditableSurfaces.filter((surface) =>
      [surface.label, surface.group, surface.description, ...surface.keywords].some((value) => value.toLocaleLowerCase().includes(query)));
  }, [surfaceSearch]);
  const gestureControls = useMemo(() => ({
    begin: () => dispatch({ type: "begin-gesture" }),
    end: () => dispatch({ type: "end-gesture" })
  }), []);
  const requestNavigation = useCallback((url: string) => setPendingDraftAction({ kind: "navigate", url }), []);
  const allowNavigation = useUnsavedAppearanceGuard(dirty, requestNavigation);

  useEffect(() => { dispatch({ type: "initialize", value: siteAppearance }); setDraftCheckpoint(siteAppearance); }, [siteAppearance]);
  useEffect(() => { void refreshAssets(); void refreshLibrary(); }, []);
  useEffect(() => () => { if (pulseTimerRef.current != null) window.clearTimeout(pulseTimerRef.current); }, []);

  function changeDraft(mutator: (draft: SiteAppearance) => void) {
    const next = normalizeSiteAppearance(history.present);
    mutator(next);
    dispatch({ type: "change", value: next });
    setCompareMode("draft");
    setPulseSurface(selectedSurface);
    if (pulseTimerRef.current != null) window.clearTimeout(pulseTimerRef.current);
    pulseTimerRef.current = window.setTimeout(() => setPulseSurface(null), 450);
    setNotice(null);
    setError(null);
  }

  async function refreshAssets() {
    try {
      setThemeAssets(await listThemeAssets());
    } catch (reason) {
      setError(getApiErrorMessage(reason, "主题资源列表加载失败。"));
    }
  }

  async function refreshLibrary() {
    try {
      setThemeLibrary(await listThemePresets());
    } catch (reason) {
      setError(getApiErrorMessage(reason, "主题库加载失败。"));
    }
  }

  async function saveDraftAsPreset(requestedName: string, description: string | null) {
    const name = requestedName.trim();
    if (!name) { setError("主题名称不能为空。"); return false; }
    setLibraryBusy(true);
    setError(null);
    try {
      const created = await createThemePreset(name, description, history.present);
      setSelectedPresetId(created.id);
      setDraftCheckpoint(created.appearance);
      await refreshLibrary();
      setNotice("当前草稿已保存到主题库；网站正在使用的主题没有改变。");
      return true;
    } catch (reason) {
      setError(getApiErrorMessage(reason, "主题保存失败。"));
      return false;
    } finally {
      setLibraryBusy(false);
    }
  }

  function requestLibraryAction(kind: "load" | "apply", preset: ThemePreset) {
    if (dirty) setPendingDraftAction({ kind, preset });
    else void executeLibraryAction(kind, preset);
  }

  async function executeLibraryAction(kind: "load" | "apply", preset: ThemePreset) {
    setPendingDraftAction(null);
    setSelectedPresetId(preset.id);
    if (kind === "load") {
      dispatch({ type: "change", value: preset.appearance });
      setDraftCheckpoint(preset.appearance);
      setCompareMode("draft");
      setNotice(`${getPresetDisplayName(preset)} 已载入编辑草稿；网站正在使用的主题没有改变。`);
      setError(null);
      return;
    }
    setApplyPresetDialog(preset);
  }

  async function performApply(preset: ThemePreset) {
    setApplyPresetDialog(null);
    setLibraryBusy(true);
    setError(null);
    try {
      const applied = await applyThemePreset(preset.id);
      dispatch({ type: "save-success", value: applied });
      setDraftCheckpoint(applied);
      await reloadSiteAppearance();
      await Promise.all([refreshLibrary(), refreshAssets()]);
      setNotice(appearanceEquals(applied, preset.appearance)
        ? `${getPresetDisplayName(preset)} 已应用到全站。`
        : `${getPresetDisplayName(preset)} 已应用；缺失素材已安全禁用并回退到默认视觉。`);
    } catch (reason) {
      setError(getApiErrorMessage(reason, "主题应用失败。"));
    } finally {
      setLibraryBusy(false);
    }
  }

  function saveThenContinuePendingAction() {
    const pending = pendingDraftAction;
    if (!pending) return;
    setPendingDraftAction(null);
    setSavePresetDialog({ continuation: pending });
  }

  async function confirmSavePreset(name: string, description: string | null) {
    const continuation = savePresetDialog?.continuation ?? null;
    if (!await saveDraftAsPreset(name, description)) return;
    setSavePresetDialog(null);
    if (continuation) await executePendingDraftAction(continuation);
  }

  function discardThenContinuePendingAction() {
    const pending = pendingDraftAction;
    if (!pending) return;
    dispatch({ type: "initialize", value: draftCheckpoint });
    void executePendingDraftAction(pending);
  }

  async function executePendingDraftAction(action: PendingDraftAction) {
    setPendingDraftAction(null);
    if (action.kind === "load" || action.kind === "apply") {
      await executeLibraryAction(action.kind, action.preset);
      return;
    }
    if (action.kind === "reset") {
      handleResetAll();
      return;
    }
    allowNavigation();
    window.location.assign(action.url);
  }

  async function handleUpdatePreset(preset: ThemePreset) {
    if (!preset.id) return;
    setUpdatePresetDialog(null);
    setLibraryBusy(true);
    try {
      await updateThemePreset(preset.id, preset.name, preset.description, history.present);
      setDraftCheckpoint(history.present);
      await refreshLibrary();
      setNotice(`${preset.name} 已更新；网站正在使用的主题没有改变。`);
    } catch (reason) { setError(getApiErrorMessage(reason, "主题更新失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleDuplicatePreset(preset: ThemePreset) {
    if (!preset.id) return;
    setLibraryBusy(true);
    try { const created = await duplicateThemePreset(preset.id); setSelectedPresetId(created.id); await refreshLibrary(); setNotice(`已创建 ${created.name}。`); }
    catch (reason) { setError(getApiErrorMessage(reason, "主题复制失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleRenamePreset(preset: ThemePreset, name: string) {
    if (!preset.id) return;
    setDialogError(null);
    setLibraryBusy(true);
    try { await renameThemePreset(preset.id, name); await refreshLibrary(); setRenamePresetDialog(null); setNotice("主题已重命名。"); }
    catch (reason) { setDialogError(getApiErrorMessage(reason, "主题重命名失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleDeletePreset(preset: ThemePreset) {
    if (!preset.id) return;
    setDialogError(null);
    setLibraryBusy(true);
    try { await deleteThemePreset(preset.id); if (selectedPresetId === preset.id) { setSelectedPresetId(undefined); setDraftCheckpoint(history.saved); } await Promise.all([refreshLibrary(), refreshAssets()]); setDeletePresetDialog(null); setNotice("主题已删除；素材文件保持不变。当前编辑内容仍保留为草稿。"); }
    catch (reason) { setDialogError(getApiErrorMessage(reason, "主题删除失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function prepareImportPreset(file: File) {
    setLibraryBusy(true);
    setError(null);
    setDialogError(null);
    try { setImportPresetDialog({ file, preview: await preflightThemePresetImport(file) }); }
    catch (reason) { setError(getApiErrorMessage(reason, "主题包未通过安全检查，未导入任何内容。")); }
    finally { setLibraryBusy(false); }
  }

  async function performImportPreset(file: File) {
    setLibraryBusy(true);
    setDialogError(null);
    try { const imported = await importThemePreset(file); setSelectedPresetId(imported.id); await Promise.all([refreshLibrary(), refreshAssets()]); setImportPresetDialog(null); setNotice(`${imported.name} 已安全导入 · 格式版本 ${imported.schemaVersion} · ${imported.assetCount} 个素材 · 尚未应用到全站。`); }
    catch (reason) { setDialogError(getApiErrorMessage(reason, "主题包导入失败，未应用任何主题。")); }
    finally { setLibraryBusy(false); }
  }

  const visiblePresets = useMemo(() => {
    const query = presetSearch.trim().toLocaleLowerCase();
    const items = themeLibrary.items.filter((preset) => preset.name.toLocaleLowerCase().includes(query));
    return items.sort((first, second) => presetSort === "name"
      ? first.name.localeCompare(second.name)
      : (second.updatedAt ?? "").localeCompare(first.updatedAt ?? "") || first.name.localeCompare(second.name));
  }, [themeLibrary.items, presetSearch, presetSort]);

  async function handleAssetUpload(file: File) {
    setIsUploading(true);
    setError(null);
    try {
      if (selectedSurface === "page.background") {
        const uploaded = await uploadImage(file);
        changeDraft((draft) => {
          const key = pageBackgroundKey;
          draft.pages[key] = { ...draft.pages[key], enabled: true, imageUrl: normalizeUploadedImagePath(uploaded.url) };
        });
      } else {
        const uploaded = await uploadThemeAsset(file);
        assignAsset({ assetId: uploaded.assetId, url: uploaded.url });
        await refreshAssets();
      }
      setNotice("素材已上传并加入当前草稿；在“保存并应用全站”前不会改变网站主题。");
    } catch (reason) {
      setError(getApiErrorMessage(reason, "资源上传失败，当前草稿已保留。"));
    } finally {
      setIsUploading(false);
    }
  }

  function assignAsset(asset: ThemeAssetReference | null) {
    changeDraft((draft) => {
      if (selectedSurface === "global.background") {
        draft.background = { ...draft.background, asset, enabled: Boolean(asset), positionX: draft.background.positionX ?? 50, positionY: draft.background.positionY ?? 50, sizeMode: draft.background.sizeMode ?? "cover", repeat: draft.background.repeat ?? "no-repeat", attachment: draft.background.attachment ?? "scroll", overlayColor: draft.background.overlayColor ?? "#000000", overlayOpacity: draft.background.overlayOpacity ?? 0.45, blur: draft.background.blur ?? 0, brightness: draft.background.brightness ?? 100 };
      } else if (selectedSurface === "panel.primary") {
        draft.panelSkin = { ...draft.panelSkin, backgroundTexture: asset, enabled: Boolean(asset) || draft.panelSkin.enabled };
      } else if (selectedSurface === "panel.header") {
        draft.panelSkin = { ...draft.panelSkin, headerTexture: asset, enabled: Boolean(asset) || draft.panelSkin.enabled };
      } else if (selectedSurface === "panel.border") {
        draft.panelSkin = { ...draft.panelSkin, borderTexture: asset, enabled: Boolean(asset) || draft.panelSkin.enabled };
      } else if (selectedSurface.startsWith("icon.")) {
        const slot = selectedSurface.slice("icon.".length) as ThemeIconSlot;
        draft.icons[slot] = asset ? { ...(draft.icons[slot] ?? createIconAssignment(asset)), asset, enabled: true } : null;
      } else if (selectedSurface.startsWith("decoration.")) {
        const slot = selectedSurface.slice("decoration.".length) as ThemeDecorationSlot;
        draft.decorations[slot] = asset ? { ...(draft.decorations[slot] ?? createDecorationAssignment(asset)), asset, enabled: true } : null;
      }
    });
  }

  async function handleDeleteAsset(assetId: string) {
    setLibraryBusy(true);
    setDialogError(null);
    try {
      await deleteThemeAsset(assetId);
      await refreshAssets();
      setDeleteAssetDialog(null);
      setNotice("未被正式配置或当前草稿引用的资源已删除。");
    } catch (reason) {
      setDialogError(getApiErrorMessage(reason, "资源删除失败。"));
    } finally {
      setLibraryBusy(false);
    }
  }

  async function handleRenameAsset(asset: ThemeAssetLibraryItem, displayName: string) {
    setLibraryBusy(true);
    setDialogError(null);
    try {
      await renameThemeAsset(asset.assetId, displayName);
      await refreshAssets();
      setRenameAssetDialog(null);
      setNotice("素材显示名称已更新；文件地址和现有引用保持不变。");
    } catch (reason) {
      setDialogError(getApiErrorMessage(reason, "素材重命名失败。"));
    } finally {
      setLibraryBusy(false);
    }
  }

  async function handleSave() {
    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      const saved = normalizeSiteAppearance(await updateSiteAppearance(history.present));
      dispatch({ type: "save-success", value: saved });
      setDraftCheckpoint(saved);
      await reloadSiteAppearance();
      await refreshAssets();
      setCompareMode("draft");
      setNotice("主题草稿已保存并应用到全站。");
    } catch (reason) {
      setError(getApiErrorMessage(reason, "保存失败，当前草稿仍然保留，可以修复后重试或放弃修改。"));
    } finally {
      setIsSaving(false);
    }
  }

  function handleDiscard() {
    dispatch({ type: "initialize", value: draftCheckpoint });
    setCompareMode("draft");
    setError(null);
    setNotice("草稿已恢复到当前已保存版本，服务器配置未发生额外变化。");
  }

  function handleResetSection() {
    dispatch({ type: "change", value: resetThemeSurface(history.present, selectedSurface, pageBackgroundKey) });
    setCompareMode("draft");
    setNotice(`${selected.label} 已恢复默认值，其他编辑区域保持不变。`);
  }

  function handleResetAll() {
    dispatch({ type: "change", value: createDefaultSiteAppearance() });
    setCompareMode("draft");
    setShowResetAll(false);
    setNotice("整个主题已恢复为系统默认草稿；素材文件没有被删除。");
  }

  function requestResetAll() {
    if (dirty) setPendingDraftAction({ kind: "reset" });
    else setShowResetAll(true);
  }

  function selectSurface(surface: ThemeEditorSurfaceId) {
    setSelectedSurface(surface);
    setEditorMode("select");
  }

  function changePreviewPage(page: ThemeEditorPreviewPage) {
    setPreviewPage(page);
    setPageBackgroundKey(getPreviewPageKey(page));
  }

  return (
    <ThemeEditorGestureContext.Provider value={gestureControls}>
    <section className={`theme-editor-page${focusMode ? " focus-mode" : ""}`}>
      <header className="theme-editor-page-header">
        <div><span>站点外观 / 可视化工作台</span><h1>可视化主题编辑器</h1><p>选择编辑区域 → 实时预览 → 撤销或对比 → 保存主题 → 明确应用</p></div>
        <div className={`theme-editor-save-state${dirty ? " dirty" : ""}`}><span />{dirty ? "有未保存修改" : "当前草稿已保存"}</div>
      </header>

      {(notice || error) && <div className={error ? "alert error" : "quiet-note success"} role="status">{error ?? notice}</div>}

      <div className="theme-editor-toolbar" aria-label="主题编辑工具栏">
        <ToolbarGroup label="编辑历史">
          <button className="button" type="button" disabled={history.past.length === 0 || isSaving} onClick={() => dispatch({ type: "undo" })}>撤销</button>
          <button className="button" type="button" disabled={history.future.length === 0 || isSaving} onClick={() => dispatch({ type: "redo" })}>重做</button>
          <small>{history.past.length}/{ThemeEditorHistoryLimit}</small>
        </ToolbarGroup>
        <ToolbarGroup label="预览操作">
          <SegmentedButton active={editorMode === "preview"} onClick={() => setEditorMode("preview")}>只看效果</SegmentedButton>
          <SegmentedButton active={editorMode === "select"} onClick={() => setEditorMode("select")}>点击选区</SegmentedButton>
        </ToolbarGroup>
        <ToolbarGroup label="对比版本">
          {(["draft", "saved", "default"] as ThemeEditorCompareMode[]).map((mode) => <SegmentedButton key={mode} active={compareMode === mode} onClick={() => setCompareMode(mode)}>{mode === "draft" ? "当前草稿" : mode === "saved" ? "已保存版本" : "系统默认"}</SegmentedButton>)}
        </ToolbarGroup>
        <div className="theme-editor-toolbar-actions">
          <button className="button" type="button" disabled={!dirty || isSaving} onClick={handleDiscard}>放弃草稿修改</button>
          <button className="button" type="button" disabled={isSaving} onClick={requestResetAll}>恢复整个默认主题</button>
          <button className="button primary" type="button" disabled={!dirty || isSaving || isUploading} onClick={() => void handleSave()}>{isSaving ? "正在保存..." : "保存并应用全站"}</button>
        </div>
      </div>

      <section className="theme-library-panel" aria-label="主题库">
        <header><div><span>主题库</span><h2>已保存主题</h2><p>“载入预览”只改变草稿；只有“应用全站”才会改变网站主题。</p></div><div><input value={presetSearch} onChange={(event) => setPresetSearch(event.target.value)} placeholder="搜索主题名称..." aria-label="搜索主题" /><select value={presetSort} onChange={(event) => setPresetSort(event.target.value as "updated" | "name")} aria-label="主题排序"><option value="updated">最近更新</option><option value="name">按名称</option></select><label className="button">{libraryBusy ? "正在检查..." : "导入主题包"}<input type="file" accept=".zip,application/zip" disabled={libraryBusy} onChange={(event) => { const file = event.target.files?.[0]; if (file) void prepareImportPreset(file); event.currentTarget.value = ""; }} /></label><button className="button primary" type="button" disabled={libraryBusy} onClick={() => setSavePresetDialog({ continuation: null })}>另存为主题</button></div></header>
        <div className="theme-library-grid">
          {visiblePresets.map((preset) => <article key={preset.id ?? "default"} className={selectedPresetId !== undefined && selectedPresetId === preset.id ? "selected" : ""}>
            <div className="theme-library-swatch" style={{ background: `linear-gradient(135deg, ${preset.appearance.theme.panelColor}, ${preset.appearance.theme.accentColor})` }} aria-hidden="true" />
            <div className="theme-library-meta"><div><strong>{getPresetDisplayName(preset)}</strong><span>{preset.isBuiltIn ? "系统" : "自定义"}</span></div><p>{preset.isBuiltIn ? "不可修改的系统默认主题" : preset.description || "可保存、复用和导出的主题"}</p><small>{preset.assetCount} 个素材 · {preset.updatedAt ? new Date(preset.updatedAt).toLocaleString() : "系统内置"}{themeLibrary.lastAppliedPresetId === preset.id && preset.id ? " · 最近从此主题应用" : ""}</small></div>
            <div className="theme-library-actions"><button type="button" onClick={() => requestLibraryAction("load", preset)}>载入预览</button><button type="button" className="primary" onClick={() => requestLibraryAction("apply", preset)}>应用全站</button>{!preset.isBuiltIn && <details onClick={(event) => { if ((event.target as HTMLElement).closest("button")) event.currentTarget.removeAttribute("open"); }}><summary>更多</summary><div><button type="button" onClick={() => setUpdatePresetDialog(preset)}>用草稿更新</button><button type="button" onClick={() => void handleDuplicatePreset(preset)}>复制主题</button><button type="button" onClick={() => { setDialogError(null); setRenamePresetDialog(preset); }}>重命名</button><button type="button" onClick={() => preset.id && void exportThemePreset(preset.id, preset.name)}>导出主题包</button><button type="button" className="danger" onClick={() => { setDialogError(null); setDeletePresetDialog(preset); }}>删除主题</button></div></details>}</div>
          </article>)}
          {visiblePresets.length === 0 && <div className="theme-library-empty">没有匹配的主题。</div>}
        </div>
      </section>

      <div className="theme-editor-context-bar">
        <label>预览页面<select value={previewPage} onChange={(event) => changePreviewPage(event.target.value as ThemeEditorPreviewPage)}>{themeEditorPreviewPages.map((option) => <option key={option.key} value={option.key}>{option.label}</option>)}</select></label>
        <div className="theme-editor-viewport-switch" aria-label="预览尺寸">{themeEditorViewports.map((option) => <SegmentedButton key={option.key} active={viewport === option.key} onClick={() => setViewport(option.key)}>{option.label}<small>{option.width}</small></SegmentedButton>)}</div>
        <div className="theme-editor-zoom-switch" aria-label="预览缩放">{themeEditorPreviewZooms.map((option) => <SegmentedButton key={option.key} active={previewZoom === option.key} onClick={() => setPreviewZoom(option.key)}>{option.label}</SegmentedButton>)}</div>
        <div className="theme-editor-quick-actions" aria-label="快速选择编辑区域">
          <button type="button" onClick={() => selectSurface("global.background")}>背景</button>
          <button type="button" onClick={() => selectSurface("panel.primary")}>面板</button>
          <button type="button" onClick={() => selectSurface("global.colors")}>颜色</button>
          <button type="button" onClick={() => selectSurface("icon.problem")}>图标</button>
          <button type="button" onClick={() => selectSurface("decoration.pageHeader")}>装饰</button>
        </div>
        <div className="theme-editor-layout-actions" aria-label="预览布局">
          {!focusMode && <button type="button" className="button" aria-pressed={navigatorCollapsed} onClick={() => setNavigatorCollapsed((value) => !value)}>{navigatorCollapsed ? "展开导航" : "收起导航"}</button>}
          {!focusMode && <button type="button" className="button" aria-pressed={inspectorCollapsed} onClick={() => setInspectorCollapsed((value) => !value)}>{inspectorCollapsed ? "展开属性" : "收起属性"}</button>}
          <button type="button" className="button primary" aria-pressed={focusMode} onClick={() => setFocusMode((value) => !value)}>{focusMode ? "退出专注预览" : "专注预览"}</button>
        </div>
      </div>

      <div className={`theme-editor-workbench${navigatorCollapsed || focusMode ? " navigator-collapsed" : ""}${inspectorCollapsed || focusMode ? " inspector-collapsed" : ""}`}>
        {!navigatorCollapsed && !focusMode && <aside className="theme-editor-navigator" aria-label="编辑区域导航">
          <div className="theme-editor-pane-heading"><div><span>编辑区域</span><strong>快速定位</strong></div><b>{filteredSurfaces.length}</b></div>
          <label className="theme-editor-search"><span>搜索</span><input value={surfaceSearch} onChange={(event) => setSurfaceSearch(event.target.value)} placeholder="背景、面板、题目、标题、角落..." /></label>
          {(["Global", "Panels", "Icons", "Decorations"] as const).map((group) => {
            const surfaces = filteredSurfaces.filter((surface) => surface.group === group);
            return surfaces.length > 0 && <section key={group}><h2>{getSurfaceGroupLabel(group)}</h2>{surfaces.map((surface) => <button key={surface.id} type="button" className={selectedSurface === surface.id ? "active" : ""} onClick={() => selectSurface(surface.id)}><strong>{surface.label}</strong><small>{surface.description}</small></button>)}</section>;
          })}
        </aside>}

        <section className="theme-editor-stage" aria-label="主题预览画布">
          <div className="theme-editor-stage-heading"><div><span>{getCompareModeLabel(compareMode)}预览</span><strong>{themeEditorPreviewPages.find((item) => item.key === previewPage)?.label}</strong></div><div className="theme-editor-stage-selection"><strong>当前选择：{getThemeSurfaceBreadcrumb(selectedSurface)}</strong><small>{getAffectedSurfaceMessage(previewPage, selectedSurface)}</small></div><small>{editorMode === "select" ? "点击带轮廓的区域即可编辑；蓝色轮廓只在编辑器中显示" : "只看最终效果，不显示选择轮廓"}</small></div>
          <div className="theme-editor-canvas-scroll">
            <ThemeEditorPreview appearance={previewAppearance} page={previewPage} pageBackgroundKey={pageBackgroundKey} viewport={viewport} zoom={previewZoom} mode={editorMode} selectedSurface={selectedSurface} pulseSurface={pulseSurface} onSelect={selectSurface} onBackgroundPositionChange={(positionX, positionY) => changeDraft((draft) => { draft.background.positionX = Math.round(positionX); draft.background.positionY = Math.round(positionY); })} onGestureStart={gestureControls.begin} onGestureEnd={gestureControls.end} />
          </div>
          {viewport === "mobile" && <p className="theme-editor-mobile-note">移动端提供基础编辑；建议使用桌面端高效调整主题。</p>}
        </section>

        {!inspectorCollapsed && !focusMode && <aside className="theme-editor-inspector" aria-label="属性设置">
          <div className="theme-editor-pane-heading"><div><span>属性设置</span><strong>{selected.label}</strong></div></div>
          <div className="theme-editor-breadcrumb">{getThemeSurfaceBreadcrumb(selectedSurface)}</div>
          <p className="theme-editor-inspector-description">{selected.description}</p>
          <ThemePropertyInspector appearance={history.present} page={previewPage} pageBackgroundKey={pageBackgroundKey} onPageBackgroundKeyChange={setPageBackgroundKey} surface={selectedSurface} assets={themeAssets} disabled={isSaving} uploading={isUploading} onChange={changeDraft} onAssignAsset={assignAsset} onUpload={handleAssetUpload} onOpenLibrary={() => setShowAssetLibrary(true)} />
          <button className="button theme-editor-reset-section" type="button" disabled={isSaving} onClick={handleResetSection}>恢复当前区域默认值</button>
        </aside>}
      </div>

      {showAssetLibrary && <AssetLibraryDialog assets={themeAssets} draft={history.present} onClose={() => setShowAssetLibrary(false)} onSelect={(asset) => { assignAsset(asset); setShowAssetLibrary(false); }} onRename={(asset) => { setDialogError(null); setRenameAssetDialog(asset); }} onDelete={(asset) => { setDialogError(null); setDeleteAssetDialog(asset); }} onNavigate={(surface) => { selectSurface(surface); setShowAssetLibrary(false); }} />}
      {showResetAll && <ConfirmDialog title="恢复整个默认主题？" description="全部自定义配置将变为系统默认草稿。素材文件不会删除；只有“保存并应用全站”后才会改变网站主题。" confirmLabel="恢复默认草稿" onCancel={() => setShowResetAll(false)} onConfirm={handleResetAll} />}
      {pendingDraftAction && <DraftTransitionDialog target={getPendingActionLabel(pendingDraftAction)} onSave={saveThenContinuePendingAction} onDiscard={discardThenContinuePendingAction} onCancel={() => setPendingDraftAction(null)} />}
      {savePresetDialog && <PresetSaveDialog busy={libraryBusy} onCancel={() => setSavePresetDialog(null)} onSave={(name, description) => void confirmSavePreset(name, description)} />}
      {applyPresetDialog && <ConfirmDialog title="应用主题到全站？" description={`“${getPresetDisplayName(applyPresetDialog)}”将改变网站当前主题。缺失素材会安全回退到默认视觉。`} confirmLabel="确认应用全站" onCancel={() => setApplyPresetDialog(null)} onConfirm={() => void performApply(applyPresetDialog)} />}
      {updatePresetDialog && <ConfirmDialog title="用当前草稿更新主题？" description={`“${updatePresetDialog.name}”将保存当前草稿，但不会改变网站正在使用的主题。`} confirmLabel="确认更新" onCancel={() => setUpdatePresetDialog(null)} onConfirm={() => void handleUpdatePreset(updatePresetDialog)} />}
      {renamePresetDialog && <RenameDialog title="重命名主题" label="主题名称" currentName={renamePresetDialog.name} maxLength={64} busy={libraryBusy} error={dialogError} onCancel={() => setRenamePresetDialog(null)} onSave={(name) => void handleRenamePreset(renamePresetDialog, name)} />}
      {deletePresetDialog && <DeleteDialog title="删除主题？" objectName={deletePresetDialog.name} description="该主题会从主题库删除，引用的素材文件会保留，当前草稿也不会改变。" busy={libraryBusy} error={dialogError} onCancel={() => setDeletePresetDialog(null)} onDelete={() => void handleDeletePreset(deletePresetDialog)} />}
      {renameAssetDialog && <RenameDialog title="重命名素材" label="素材显示名称" currentName={getAssetDisplayName(renameAssetDialog)} maxLength={128} busy={libraryBusy} error={dialogError} onCancel={() => setRenameAssetDialog(null)} onSave={(name) => void handleRenameAsset(renameAssetDialog, name)} />}
      {deleteAssetDialog && <DeleteDialog title="删除素材？" objectName={getAssetDisplayName(deleteAssetDialog)} description="只会删除这个未被引用的素材文件；此操作无法撤销。" busy={libraryBusy} error={dialogError} onCancel={() => setDeleteAssetDialog(null)} onDelete={() => void handleDeleteAsset(deleteAssetDialog.assetId)} />}
      {importPresetDialog && <ImportReviewDialog value={importPresetDialog.preview} busy={libraryBusy} error={dialogError} onCancel={() => setImportPresetDialog(null)} onConfirm={() => void performImportPreset(importPresetDialog.file)} />}
    </section>
    </ThemeEditorGestureContext.Provider>
  );
}

function ThemePropertyInspector({ appearance, page, pageBackgroundKey, onPageBackgroundKeyChange, surface, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: {
  appearance: SiteAppearance;
  page: ThemeEditorPreviewPage;
  pageBackgroundKey: SitePageKey;
  onPageBackgroundKeyChange: (key: SitePageKey) => void;
  surface: ThemeEditorSurfaceId;
  assets: ThemeAssetLibraryItem[];
  disabled: boolean;
  uploading: boolean;
  onChange: (mutator: (draft: SiteAppearance) => void) => void;
  onAssignAsset: (asset: ThemeAssetReference | null) => void;
  onUpload: (file: File) => Promise<void>;
  onOpenLibrary: () => void;
}) {
  if (surface === "global.background") return <BackgroundInspector value={appearance.background} assets={assets} disabled={disabled} uploading={uploading} onChange={(patch) => onChange((draft) => { draft.background = { ...draft.background, ...patch }; })} onAssignAsset={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />;
  if (surface === "global.colors") return <TokenInspector value={appearance.theme} disabled={disabled} onChange={(patch) => onChange((draft) => { draft.theme = { ...draft.theme, ...patch }; })} />;
  if (surface === "page.background") {
    const key = pageBackgroundKey;
    const value = appearance.pages[key];
    return <InspectorSection title={`${themeEditorPreviewPages.find((item) => item.key === page)?.label ?? "当前页面"}背景`}><EnumControl label="适用页面" value={key} disabled={disabled} options={sitePageOptions.map((option) => option.key)} onChange={(next) => onPageBackgroundKeyChange(next as SitePageKey)} /><ToggleControl label="启用此背景" checked={value.enabled} disabled={disabled || !value.imageUrl} onChange={(enabled) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], enabled }; })} /><InspectorGroup title="背景素材" open><PageBackgroundAsset value={value.imageUrl} disabled={disabled} uploading={uploading} onUpload={onUpload} onClear={() => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], enabled: false, imageUrl: null }; })} /></InspectorGroup><InspectorGroup title="构图"><NumericControl label="水平位置" value={value.positionX} min={0} max={100} step={1} disabled={disabled} onChange={(positionX) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], positionX }; })} /><NumericControl label="垂直位置" value={value.positionY} min={0} max={100} step={1} disabled={disabled} onChange={(positionY) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], positionY }; })} /><NumericControl label="缩放" value={value.scale} min={0.5} max={2.5} step={0.05} disabled={disabled} onChange={(scale) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], scale }; })} /></InspectorGroup><InspectorGroup title="效果"><NumericControl label="遮罩强度" value={value.overlayOpacity ?? appearance.theme.backgroundOverlayOpacity} min={0} max={1} step={0.05} disabled={disabled} onChange={(overlayOpacity) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], overlayOpacity }; })} /></InspectorGroup></InspectorSection>;
  }
  if (surface === "panel.primary") return <PanelInspector value={appearance.panelSkin} assets={assets} disabled={disabled} uploading={uploading} onChange={(patch) => onChange((draft) => { draft.panelSkin = { ...draft.panelSkin, ...patch }; })} onAssignAsset={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />;
  if (surface === "panel.header" || surface === "panel.border") {
    const value = surface === "panel.header" ? appearance.panelSkin.headerTexture : appearance.panelSkin.borderTexture;
    return <InspectorSection title={surface === "panel.header" ? "面板标题" : "面板边框"}><InspectorGroup title="素材" open><AssetProperty value={value} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /></InspectorGroup></InspectorSection>;
  }
  if (surface.startsWith("icon.")) {
    const slot = surface.slice("icon.".length) as ThemeIconSlot;
    const value = appearance.icons[slot] ?? null;
    return <SlotInspector value={value} assets={assets} disabled={disabled} uploading={uploading} onChange={(next) => onChange((draft) => { draft.icons[slot] = next; })} onAssignAsset={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />;
  }
  const slot = surface.slice("decoration.".length) as ThemeDecorationSlot;
  const value = appearance.decorations[slot] ?? null;
  return <DecorationInspector slot={slot} value={value} assets={assets} disabled={disabled} uploading={uploading} onChange={(next) => onChange((draft) => { draft.decorations[slot] = next; })} onAssignAsset={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />;
}

function BackgroundInspector({ value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { value: SiteThemeBackground; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (patch: Partial<SiteThemeBackground>) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="全站背景"><ToggleControl label="启用全站背景" checked={value.enabled} disabled={disabled || !value.asset} onChange={(enabled) => onChange({ enabled })} /><InspectorGroup title="背景素材" open><AssetProperty value={value.asset} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /></InspectorGroup><InspectorGroup title="构图"><NumericControl label="水平焦点" value={value.positionX ?? 50} min={0} max={100} step={1} disabled={disabled} onChange={(positionX) => onChange({ positionX })} /><NumericControl label="垂直焦点" value={value.positionY ?? 50} min={0} max={100} step={1} disabled={disabled} onChange={(positionY) => onChange({ positionY })} /><EnumControl label="填充方式" value={value.sizeMode ?? "cover"} disabled={disabled} options={["cover", "contain", "auto"]} onChange={(sizeMode) => onChange({ sizeMode: sizeMode as SiteThemeBackground["sizeMode"] })} /><EnumControl label="重复方式" value={value.repeat ?? "no-repeat"} disabled={disabled} options={["no-repeat", "repeat", "repeat-x", "repeat-y"]} onChange={(repeat) => onChange({ repeat: repeat as SiteThemeBackground["repeat"] })} /><EnumControl label="滚动方式" value={value.attachment ?? "scroll"} disabled={disabled} options={["scroll", "fixed"]} onChange={(attachment) => onChange({ attachment: attachment as SiteThemeBackground["attachment"] })} /></InspectorGroup><InspectorGroup title="效果"><ColorControl label="遮罩颜色" value={value.overlayColor ?? "#000000"} disabled={disabled} onChange={(overlayColor) => onChange({ overlayColor })} /><NumericControl label="遮罩强度" value={value.overlayOpacity ?? 0.45} min={0} max={1} step={0.05} disabled={disabled} onChange={(overlayOpacity) => onChange({ overlayOpacity })} /><NumericControl label="模糊" value={value.blur ?? 0} min={0} max={20} step={1} disabled={disabled} onChange={(blur) => onChange({ blur })} /><NumericControl label="亮度" value={value.brightness ?? 100} min={50} max={150} step={5} disabled={disabled} onChange={(brightness) => onChange({ brightness })} /></InspectorGroup></InspectorSection>;
}

function PanelInspector({ value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { value: SitePanelSkin; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (patch: Partial<SitePanelSkin>) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="主要面板"><ToggleControl label="启用面板外观" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ enabled })} /><InspectorGroup title="纹理" open><AssetProperty value={value.backgroundTexture} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /><NumericControl label="面板底色透明度" value={value.backgroundOpacity ?? 1} min={0} max={1} step={0.05} disabled={disabled} onChange={(backgroundOpacity) => onChange({ backgroundOpacity })} /><NumericControl label="纹理透明度" value={value.textureOpacity ?? 0.15} min={0} max={1} step={0.05} disabled={disabled} onChange={(textureOpacity) => onChange({ textureOpacity })} /></InspectorGroup><InspectorGroup title="形状与效果"><NumericControl label="圆角" value={value.radius ?? 8} min={0} max={32} step={1} disabled={disabled} onChange={(radius) => onChange({ radius })} /><NumericControl label="阴影强度" value={value.shadowStrength ?? 0.25} min={0} max={1} step={0.05} disabled={disabled} onChange={(shadowStrength) => onChange({ shadowStrength })} /></InspectorGroup></InspectorSection>;
}

function TokenInspector({ value, disabled, onChange }: { value: SiteAppearanceTheme; disabled: boolean; onChange: (patch: Partial<SiteAppearanceTheme>) => void }) {
  const warnings = getContrastWarnings(value);
  return <InspectorSection title="全站颜色"><ToggleControl label="启用自定义外观" checked={value.backgroundEnabled} disabled={disabled} onChange={(backgroundEnabled) => onChange({ backgroundEnabled })} /><InspectorGroup title="常用配色" open><ColorControl label="面板颜色" value={value.panelColor} disabled={disabled} onChange={(panelColor) => onChange({ panelColor })} /><ColorControl label="主要文字" value={value.textPrimaryColor} disabled={disabled} onChange={(textPrimaryColor) => onChange({ textPrimaryColor })} /><ColorControl label="次要文字" value={value.textSecondaryColor} disabled={disabled} onChange={(textSecondaryColor) => onChange({ textSecondaryColor })} /><ColorControl label="弱化文字" value={value.textMutedColor} disabled={disabled} onChange={(textMutedColor) => onChange({ textMutedColor })} /><ColorControl label="强调色" value={value.accentColor} disabled={disabled} onChange={(accentColor) => onChange({ accentColor })} /></InspectorGroup><InspectorGroup title="导航与字体"><ColorControl label="导航文字" value={value.navTextColor} disabled={disabled} onChange={(navTextColor) => onChange({ navTextColor })} /><ColorControl label="导航选中" value={value.navActiveColor} disabled={disabled} onChange={(navActiveColor) => onChange({ navActiveColor })} /><NumericControl label="导航透明度" value={value.navOpacity} min={0.35} max={1} step={0.05} disabled={disabled} onChange={(navOpacity) => onChange({ navOpacity })} /><NumericControl label="导航模糊" value={value.navBlur} min={0} max={30} step={1} disabled={disabled} onChange={(navBlur) => onChange({ navBlur })} /><EnumControl label="字体风格" value={value.fontPreset} disabled={disabled} options={["system", "readable", "mono"]} onChange={(fontPreset) => onChange({ fontPreset: fontPreset as SiteAppearanceTheme["fontPreset"] })} /></InspectorGroup><InspectorGroup title="面板效果"><NumericControl label="页面遮罩" value={value.backgroundOverlayOpacity} min={0} max={1} step={0.05} disabled={disabled} onChange={(backgroundOverlayOpacity) => onChange({ backgroundOverlayOpacity })} /><NumericControl label="面板透明度" value={value.panelOpacity} min={0.35} max={0.95} step={0.05} disabled={disabled} onChange={(panelOpacity) => onChange({ panelOpacity })} /><NumericControl label="面板模糊" value={value.panelBlur} min={0} max={30} step={1} disabled={disabled} onChange={(panelBlur) => onChange({ panelBlur })} /><NumericControl label="边框透明度" value={value.panelBorderOpacity} min={0} max={0.5} step={0.01} disabled={disabled} onChange={(panelBorderOpacity) => onChange({ panelBorderOpacity })} /></InspectorGroup><div className="theme-editor-token-presets"><button type="button" disabled={disabled} onClick={() => onChange({ panelColor: "#0F141D", panelOpacity: 0.9, panelBlur: 8, panelBorderOpacity: 0.18, textPrimaryColor: "#F7F9FC", textSecondaryColor: "#C5CCDA", textMutedColor: "#929CAE", accentColor: "#7B86FF", navOpacity: 0.82, navBlur: 10, navTextColor: "#E1E6EF", navActiveColor: "#FFFFFF", fontPreset: "readable" })}>深色高对比</button><button type="button" disabled={disabled} onClick={() => onChange({ panelColor: "#111827", panelOpacity: 0.62, panelBlur: 16, panelBorderOpacity: 0.15, textPrimaryColor: "#F4F7FB", textSecondaryColor: "#BCC5D4", textMutedColor: "#8792A6", accentColor: "#7C87FF", navOpacity: 0.58, navBlur: 18, navTextColor: "#DDE3EE", navActiveColor: "#FFFFFF", fontPreset: "system" })}>轻透玻璃</button><button type="button" disabled={disabled} onClick={() => onChange(createDefaultSiteAppearance().theme)}>恢复默认配色</button></div>{warnings.length > 0 && <p className="theme-editor-contrast-warning" role="status">对比度提示：{warnings.join("；")}</p>}</InspectorSection>;
}

function SlotInspector({ value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { value: SiteThemeIconSlot | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (value: SiteThemeIconSlot | null) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="图标"><InspectorGroup title="素材" open><AssetProperty value={value?.asset ?? null} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /></InspectorGroup>{value && <><ToggleControl label="显示图标" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ ...value, enabled })} /><InspectorGroup title="位置与大小"><SlotNumericProperties value={value} disabled={disabled} onChange={(patch) => onChange({ ...value, ...patch })} /></InspectorGroup></>}</InspectorSection>;
}

function DecorationInspector({ slot, value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { slot: ThemeDecorationSlot; value: SiteThemeDecorationSlot | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (value: SiteThemeDecorationSlot | null) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="装饰"><InspectorGroup title="素材" open><AssetProperty value={value?.asset ?? null} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /></InspectorGroup>{value && <><ToggleControl label="显示装饰" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ ...value, enabled })} /><InspectorGroup title="位置与大小"><SlotNumericProperties value={value} disabled={disabled} onChange={(patch) => onChange({ ...value, ...patch })} />{slot === "panelCorner" ? <EnumControl label="所在角落" value={value.corner ?? "top-right"} disabled={disabled} options={["top-left", "top-right", "bottom-left", "bottom-right"]} onChange={(corner) => onChange({ ...value, corner: corner as SiteThemeDecorationSlot["corner"] })} /> : <EnumControl label="对齐方式" value={value.alignment ?? "end"} disabled={disabled} options={["start", "center", "end"]} onChange={(alignment) => onChange({ ...value, alignment: alignment as SiteThemeDecorationSlot["alignment"] })} />}</InspectorGroup></>}</InspectorSection>;
}

function SlotNumericProperties({ value, disabled, onChange }: { value: SiteThemeIconSlot; disabled: boolean; onChange: (patch: Partial<SiteThemeIconSlot>) => void }) {
  return <><NumericControl label="透明度" value={value.opacity ?? 1} min={0} max={1} step={0.05} disabled={disabled} onChange={(opacity) => onChange({ opacity })} /><NumericControl label="缩放" value={value.scale ?? 1} min={0.5} max={2} step={0.05} disabled={disabled} onChange={(scale) => onChange({ scale })} /><NumericControl label="水平偏移" value={value.offsetX ?? 0} min={-64} max={64} step={1} disabled={disabled} onChange={(offsetX) => onChange({ offsetX })} /><NumericControl label="垂直偏移" value={value.offsetY ?? 0} min={-64} max={64} step={1} disabled={disabled} onChange={(offsetY) => onChange({ offsetY })} /></>;
}

function AssetProperty({ value, assets, disabled, uploading, onChoose, onUpload, onOpenLibrary }: { value: ThemeAssetReference | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChoose: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  const metadata = assets.find((asset) => asset.assetId === value?.assetId);
  return <div className="theme-editor-asset-property"><div className="theme-editor-asset-preview">{value ? <AssetThumbnail asset={{ ...metadata, ...value } as ThemeAssetLibraryItem} /> : <span>未选择素材</span>}</div><div className="theme-editor-asset-meta"><strong title={value?.assetId}>{metadata ? getAssetDisplayName(metadata) : value ? getFallbackAssetDisplayName(value.assetId) : "使用默认外观"}</strong><small>{metadata ? `${getAssetTypeLabel(metadata.contentType)} · ${formatBytes(metadata.size)} · 引用 ${metadata.usedBy.length} 处` : "可从素材库选择、上传新素材或清除"}</small></div><select aria-label="选择已有素材" value={value?.assetId ?? ""} disabled={disabled} onChange={(event) => { const asset = assets.find((item) => item.assetId === event.target.value); onChoose(asset ? toReference(asset) : null); }}><option value="">选择已有素材</option>{assets.map((asset) => <option key={asset.assetId} value={asset.assetId}>{getAssetDisplayName(asset)} · {getAssetTypeLabel(asset.contentType)} · {formatBytes(asset.size)}</option>)}</select><AssetDropZone disabled={disabled} uploading={uploading} onUpload={onUpload} /><div className="theme-editor-asset-actions"><button className="button" type="button" disabled={disabled} onClick={onOpenLibrary}>打开素材库</button><button className="button" type="button" disabled={disabled || !value} onClick={() => onChoose(null)}>清除素材</button></div></div>;
}

function PageBackgroundAsset({ value, disabled, uploading, onUpload, onClear }: { value: string | null; disabled: boolean; uploading: boolean; onUpload: (file: File) => Promise<void>; onClear: () => void }) {
  return <div className="theme-editor-asset-property"><div className="theme-editor-asset-preview">{value ? <img src={resolveSiteAssetUrl(value)} alt="页面背景预览" /> : <span>未选择图片</span>}</div><div className="theme-editor-asset-meta"><strong>页面背景图片</strong><small>{value ? "已使用当前上传素材" : "尚未上传页面背景"}</small></div><AssetDropZone disabled={disabled} uploading={uploading} onUpload={onUpload} /><button className="button" type="button" disabled={disabled || !value} onClick={onClear}>清除素材</button></div>;
}

function AssetDropZone({ disabled, uploading, onUpload }: { disabled: boolean; uploading: boolean; onUpload: (file: File) => Promise<void> }) {
  function receive(files: FileList | null) { const file = files?.[0]; if (file) void onUpload(file); }
  return <label className={`theme-editor-drop-zone${disabled ? " disabled" : ""}`} onDragOver={(event: DragEvent<HTMLLabelElement>) => { if (!disabled) event.preventDefault(); }} onDrop={(event: DragEvent<HTMLLabelElement>) => { event.preventDefault(); if (!disabled) receive(event.dataTransfer.files); }}><strong>{uploading ? "正在安全上传..." : "拖入图片或点击选择"}</strong><small>支持 PNG / JPEG / WebP，服务器会进行安全检查</small><input type="file" accept="image/png,image/jpeg,image/webp" disabled={disabled || uploading} onChange={(event: ChangeEvent<HTMLInputElement>) => { receive(event.target.files); event.target.value = ""; }} /></label>;
}

function AssetLibraryDialog({ assets, draft, onClose, onSelect, onRename, onDelete, onNavigate }: { assets: ThemeAssetLibraryItem[]; draft: SiteAppearance; onClose: () => void; onSelect: (asset: ThemeAssetReference) => void; onRename: (asset: ThemeAssetLibraryItem) => void; onDelete: (asset: ThemeAssetLibraryItem) => void; onNavigate: (surface: ThemeEditorSurfaceId) => void }) {
  const [search, setSearch] = useState("");
  const [type, setType] = useState("all");
  const [unusedOnly, setUnusedOnly] = useState(false);
  const visibleAssets = assets.map((asset) => ({ asset, usages: [...new Set([...asset.usedBy, ...getDraftAssetUsages(draft, asset.assetId)])] }))
    .filter(({ asset, usages }) => {
      const query = search.trim().toLocaleLowerCase();
      const matchesSearch = !query || [getAssetDisplayName(asset), asset.assetId, asset.contentType, ...usages].some((value) => value.toLocaleLowerCase().includes(query));
      return matchesSearch && (type === "all" || asset.contentType === type) && (!unusedOnly || usages.length === 0);
    });
  return <ThemeEditorDialog titleId="asset-library-title" onCancel={onClose} className="asset-library"><header><div><span>共享素材选择器</span><h2 id="asset-library-title">主题素材库</h2></div><button type="button" onClick={onClose} aria-label="关闭素材库">×</button></header><div className="theme-editor-asset-filters"><label>搜索<input data-dialog-autofocus value={search} onChange={(event) => setSearch(event.target.value)} placeholder="显示名称、素材编号或引用位置" /></label><label>类型<select value={type} onChange={(event) => setType(event.target.value)}><option value="all">全部类型</option><option value="image/png">PNG</option><option value="image/jpeg">JPEG</option><option value="image/webp">WebP</option></select></label><label className="theme-editor-unused-filter"><input type="checkbox" checked={unusedOnly} onChange={(event) => setUnusedOnly(event.target.checked)} />只看未引用</label></div>{assets.length === 0 ? <div className="empty-state">素材库中还没有图片</div> : visibleAssets.length === 0 ? <div className="empty-state">没有匹配的素材</div> : <div className="theme-editor-asset-library">{visibleAssets.map(({ asset, usages }) => <article key={asset.assetId}><AssetThumbnail asset={asset} /><div><strong title={asset.assetId}>{getAssetDisplayName(asset)}</strong><span>{getAssetTypeLabel(asset.contentType)} · {formatBytes(asset.size)}</span><small>引用位置</small><div className="theme-editor-usage-list">{usages.length === 0 ? <em>未引用</em> : usages.map((usage) => { const surface = usageToSurface(usage); return surface ? <button key={usage} type="button" onClick={() => onNavigate(surface)}>{getUsageLabel(usage)}</button> : <span key={usage}>{getUsageLabel(usage)}</span>; })}</div></div><div><button className="button primary" type="button" onClick={() => onSelect(toReference(asset))}>选用</button><button className="button" type="button" onClick={() => onRename(asset)}>重命名</button><button className="button danger" type="button" disabled={usages.length > 0} onClick={() => onDelete(asset)}>删除</button></div></article>)}</div>}</ThemeEditorDialog>;
}

function AssetThumbnail({ asset }: { asset: ThemeAssetLibraryItem }) {
  const [resolution, setResolution] = useState<string | null>(null);
  return <figure><img src={resolveSiteAssetUrl(asset.url)} alt="主题素材缩略图" onLoad={(event) => setResolution(`${event.currentTarget.naturalWidth}×${event.currentTarget.naturalHeight}`)} /><figcaption>{resolution ?? "正在读取尺寸"}</figcaption></figure>;
}

function NumericControl({ label, value, min, max, step, disabled, onChange }: { label: string; value: number; min: number; max: number; step: number; disabled: boolean; onChange: (value: number) => void }) {
  const gesture = useContext(ThemeEditorGestureContext);
  const commit = (raw: string) => { const parsed = Number(raw); if (Number.isFinite(parsed)) onChange(clamp(parsed, min, max)); };
  return <label className="theme-editor-numeric"><span>{label}</span><input type="range" min={min} max={max} step={step} value={value} disabled={disabled} onPointerDown={gesture.begin} onPointerUp={gesture.end} onPointerCancel={gesture.end} onKeyDown={gesture.begin} onKeyUp={gesture.end} onBlur={gesture.end} onChange={(event) => commit(event.target.value)} /><input type="number" min={min} max={max} step={step} value={value} disabled={disabled} onFocus={gesture.begin} onBlur={gesture.end} onChange={(event) => commit(event.target.value)} /></label>;
}

function ColorControl({ label, value, disabled, onChange }: { label: string; value: string; disabled: boolean; onChange: (value: string) => void }) {
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  const valid = /^#[0-9A-Fa-f]{6}$/.test(text);
  return <label className={`theme-editor-color${valid ? "" : " invalid"}`}><span>{label}</span><input type="color" value={value} disabled={disabled} onChange={(event) => { setText(event.target.value.toUpperCase()); onChange(event.target.value.toUpperCase()); }} /><input value={text} disabled={disabled} maxLength={7} spellCheck={false} onChange={(event) => { const next = event.target.value; setText(next); if (/^#[0-9A-Fa-f]{6}$/.test(next)) onChange(next.toUpperCase()); }} />{!valid && <small>请输入 #RRGGBB</small>}</label>;
}

function EnumControl({ label, value, options, disabled, onChange }: { label: string; value: string; options: string[]; disabled: boolean; onChange: (value: string) => void }) {
  return <label className="theme-editor-enum"><span>{label}</span><select value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)}>{options.map((option) => <option key={option} value={option}>{getEnumOptionLabel(option)}</option>)}</select></label>;
}

function ToggleControl({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (checked: boolean) => void }) {
  return <label className="theme-editor-toggle"><span>{label}</span><input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /><i aria-hidden="true" /></label>;
}

function InspectorSection({ title, children }: { title: string; children: ReactNode }) {
  return <section className="theme-editor-inspector-section"><h2>{title}</h2>{children}</section>;
}

function InspectorGroup({ title, open = false, children }: { title: string; open?: boolean; children: ReactNode }) {
  return <details className="theme-editor-inspector-group" open={open}><summary>{title}</summary><div>{children}</div></details>;
}

function ToolbarGroup({ label, children }: { label: string; children: ReactNode }) {
  return <div className="theme-editor-toolbar-group"><span>{label}</span><div>{children}</div></div>;
}

function getAffectedSurfaceMessage(page: ThemeEditorPreviewPage, surface: ThemeEditorSurfaceId) {
  if (surface === "panel.primary" && page === "leaderboard") return "该样式会影响当前预览中的 2 个榜单面板";
  if (surface === "panel.header" && page === "leaderboard") return "该样式会影响当前预览中的 2 个面板标题";
  if (surface === "global.background" || surface === "global.colors") return "该样式影响整个预览页面";
  return "当前选区会同步使用草稿中的主题设置";
}

function SegmentedButton({ active, children, onClick }: { active: boolean; children: ReactNode; onClick: () => void }) {
  return <button type="button" className={active ? "active" : ""} aria-pressed={active} onClick={onClick}>{children}</button>;
}

function ConfirmDialog({ title, description, confirmLabel, onCancel, onConfirm }: { title: string; description: string; confirmLabel: string; onCancel: () => void; onConfirm: () => void }) {
  return <ThemeEditorDialog titleId="theme-editor-confirm-title" descriptionId="theme-editor-confirm-description" onCancel={onCancel} className="confirm"><h2 id="theme-editor-confirm-title">{title}</h2><p id="theme-editor-confirm-description">{description}</p><div><button className="button" type="button" onClick={onCancel}>取消</button><button className="button primary" type="button" data-dialog-autofocus onClick={onConfirm}>{confirmLabel}</button></div></ThemeEditorDialog>;
}

function DraftTransitionDialog({ target, onSave, onDiscard, onCancel }: { target: string; onSave: () => void; onDiscard: () => void; onCancel: () => void }) {
  return <ThemeEditorDialog titleId="theme-draft-transition-title" descriptionId="theme-draft-transition-description" onCancel={onCancel} className="confirm"><h2 id="theme-draft-transition-title">当前草稿尚未保存</h2><p id="theme-draft-transition-description">继续“{target}”前，可以先另存当前草稿、放弃修改后继续，或取消操作。</p><div><button className="button" type="button" data-dialog-autofocus onClick={onCancel}>取消</button><button className="button danger" type="button" onClick={onDiscard}>放弃并继续</button><button className="button primary" type="button" onClick={onSave}>先另存草稿</button></div></ThemeEditorDialog>;
}

function PresetSaveDialog({ busy, onCancel, onSave }: { busy: boolean; onCancel: () => void; onSave: (name: string, description: string | null) => void }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  return <ThemeEditorDialog titleId="theme-preset-save-title" descriptionId="theme-preset-save-description" onCancel={busy ? undefined : onCancel} className="confirm theme-preset-save-dialog"><form onSubmit={(event) => { event.preventDefault(); if (name.trim() && !busy) onSave(name, description.trim() || null); }}><h2 id="theme-preset-save-title">将草稿另存为主题</h2><p id="theme-preset-save-description">保存到主题库不会改变网站正在使用的主题。</p><label>主题名称<input data-dialog-autofocus maxLength={64} value={name} onChange={(event) => setName(event.target.value)} placeholder="例如：夏日活动主题" /></label><label>说明 <small>可选</small><textarea maxLength={256} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="简短说明这套主题的用途" /></label><div className="theme-editor-dialog-actions"><button className="button" type="button" disabled={busy} onClick={onCancel}>取消</button><button className="button primary" type="submit" disabled={busy || !name.trim()}>{busy ? "正在保存..." : "保存到主题库"}</button></div></form></ThemeEditorDialog>;
}

function RenameDialog({ title, label, currentName, maxLength, busy, error, onCancel, onSave }: { title: string; label: string; currentName: string; maxLength: number; busy: boolean; error: string | null; onCancel: () => void; onSave: (name: string) => void }) {
  const [name, setName] = useState(currentName);
  const trimmed = name.trim();
  return <ThemeEditorDialog titleId="theme-rename-title" descriptionId="theme-rename-description" onCancel={busy ? undefined : onCancel} className="confirm theme-preset-save-dialog"><form onSubmit={(event) => { event.preventDefault(); if (trimmed && !busy) onSave(trimmed); }}><h2 id="theme-rename-title">{title}</h2><p id="theme-rename-description">仅更新显示名称，不会改变文件地址、素材引用或已应用主题。</p><label>{label}<input data-dialog-autofocus maxLength={maxLength} value={name} onChange={(event) => setName(event.target.value)} /></label>{error && <p className="theme-editor-dialog-error" role="alert">{error}</p>}<div className="theme-editor-dialog-actions"><button className="button" type="button" disabled={busy} onClick={onCancel}>取消</button><button className="button primary" type="submit" disabled={busy || !trimmed || trimmed.length > maxLength}>保存</button></div></form></ThemeEditorDialog>;
}

function DeleteDialog({ title, objectName, description, busy, error, onCancel, onDelete }: { title: string; objectName: string; description: string; busy: boolean; error: string | null; onCancel: () => void; onDelete: () => void }) {
  return <ThemeEditorDialog titleId="theme-delete-title" descriptionId="theme-delete-description" onCancel={busy ? undefined : onCancel} className="confirm"><h2 id="theme-delete-title">{title}</h2><p id="theme-delete-description"><strong>“{objectName}”</strong><br />{description}</p>{error && <p className="theme-editor-dialog-error" role="alert">{error}</p>}<div><button className="button" type="button" data-dialog-autofocus disabled={busy} onClick={onCancel}>取消</button><button className="button danger" type="button" disabled={busy} onClick={onDelete}>{busy ? "正在删除..." : "确认删除"}</button></div></ThemeEditorDialog>;
}

function ImportReviewDialog({ value, busy, error, onCancel, onConfirm }: { value: ThemePackPreflight; busy: boolean; error: string | null; onCancel: () => void; onConfirm: () => void }) {
  return <ThemeEditorDialog titleId="theme-import-title" descriptionId="theme-import-description" onCancel={busy ? undefined : onCancel} className="confirm theme-import-review"><h2 id="theme-import-title">主题包已通过安全检查</h2><p id="theme-import-description">以下内容仅完成验证，尚未导入，也不会自动应用到全站。</p><dl><dt>主题名称</dt><dd>{value.name}</dd><dt>导入名称</dt><dd>{value.resolvedName}</dd><dt>说明</dt><dd>{value.description || "无"}</dd><dt>格式</dt><dd>{value.format} V{value.version}</dd><dt>素材</dt><dd>{value.assetCount} 个 · {formatBytes(value.totalAssetBytes)}</dd><dt>背景</dt><dd>{value.hasBackground ? "有" : "无"}</dd><dt>面板 / 图标 / 装饰</dt><dd>{value.panelAssetCount} / {value.iconOverrideCount} / {value.decorationCount}</dd><dt>名称冲突</dt><dd>{value.hasNameCollision ? "有，已生成安全后缀" : "无"}</dd></dl>{value.warnings.length > 0 && <ul>{value.warnings.map((warning) => <li key={warning}>{warning}</li>)}</ul>}{error && <p className="theme-editor-dialog-error" role="alert">{error}</p>}<div><button className="button" type="button" data-dialog-autofocus disabled={busy} onClick={onCancel}>取消</button><button className="button primary" type="button" disabled={busy} onClick={onConfirm}>{busy ? "正在导入..." : "确认导入主题库"}</button></div></ThemeEditorDialog>;
}

function useUnsavedAppearanceGuard(dirty: boolean, onNavigate: (url: string) => void) {
  const allowNextNavigation = useRef(false);
  useEffect(() => {
    function beforeUnload(event: BeforeUnloadEvent) { if (dirty && !allowNextNavigation.current) event.preventDefault(); }
    function captureNavigation(event: MouseEvent) {
      if (!dirty || allowNextNavigation.current || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as Element | null)?.closest("a[href]") as HTMLAnchorElement | null;
      if (!anchor || anchor.target === "_blank") return;
      const destination = new URL(anchor.href, window.location.href);
      if (destination.origin !== window.location.origin || destination.href === window.location.href) return;
      event.preventDefault();
      onNavigate(destination.href);
    }
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", captureNavigation, true);
    return () => { window.removeEventListener("beforeunload", beforeUnload); document.removeEventListener("click", captureNavigation, true); };
  }, [dirty, onNavigate]);
  return () => { allowNextNavigation.current = true; };
}

function getPendingActionLabel(action: PendingDraftAction) {
  if (action.kind === "load") return `载入“${getPresetDisplayName(action.preset)}”预览`;
  if (action.kind === "apply") return `应用“${getPresetDisplayName(action.preset)}”到全站`;
  if (action.kind === "reset") return "恢复整个默认主题";
  return "离开主题编辑器";
}

function getPresetDisplayName(preset: ThemePreset) {
  return preset.isBuiltIn ? "系统默认主题" : preset.name;
}

function getCompareModeLabel(mode: ThemeEditorCompareMode) {
  if (mode === "draft") return "当前草稿";
  if (mode === "saved") return "已保存版本";
  return "系统默认";
}

function createIconAssignment(asset: ThemeAssetReference): SiteThemeIconSlot {
  return { enabled: true, asset, opacity: 1, scale: 1, offsetX: 0, offsetY: 0 };
}

function createDecorationAssignment(asset: ThemeAssetReference): SiteThemeDecorationSlot {
  return { ...createIconAssignment(asset), alignment: "end", corner: "top-right" };
}

function toReference(asset: ThemeAssetLibraryItem): ThemeAssetReference {
  return { assetId: asset.assetId, url: asset.url };
}

function formatBytes(size: number) {
  return size >= 1024 * 1024 ? `${(size / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(size / 1024))} KB`;
}

function getAssetDisplayName(asset: ThemeAssetLibraryItem) {
  return asset.displayName?.trim() || getFallbackAssetDisplayName(asset.assetId);
}

function getFallbackAssetDisplayName(assetId: string) {
  return `素材 ${assetId.slice(0, 8).toUpperCase()}`;
}

function getAssetTypeLabel(contentType: string) {
  if (contentType === "image/png") return "PNG 图片";
  if (contentType === "image/jpeg") return "JPEG 图片";
  if (contentType === "image/webp") return "WebP 图片";
  return "图片";
}

function getUsageLabel(usage: string) {
  const surface = usageToSurface(usage);
  if (surface) return getThemeSurface(surface).label;
  if (usage === "Current Site") return "当前网站";
  if (usage.startsWith("Preset: ")) return `主题：${usage.slice("Preset: ".length)}`;
  return usage;
}

function getEnumOptionLabel(option: string) {
  const page = sitePageOptions.find((item) => item.key === option);
  if (page) return page.label;
  const labels: Record<string, string> = {
    cover: "覆盖填满",
    contain: "完整显示",
    auto: "原始尺寸",
    "no-repeat": "不重复",
    repeat: "横纵重复",
    "repeat-x": "横向重复",
    "repeat-y": "纵向重复",
    scroll: "随页面滚动",
    fixed: "固定在窗口",
    system: "系统字体",
    readable: "易读字体",
    mono: "等宽字体",
    start: "靠前",
    center: "居中",
    end: "靠后",
    "top-left": "左上",
    "top-right": "右上",
    "bottom-left": "左下",
    "bottom-right": "右下"
  };
  return labels[option] ?? option;
}

function getDraftAssetUsages(appearance: SiteAppearance, assetId: string) {
  const usages: string[] = [];
  if (appearance.background.asset?.assetId === assetId) usages.push("background");
  if (appearance.panelSkin.backgroundTexture?.assetId === assetId) usages.push("panelBackground");
  if (appearance.panelSkin.headerTexture?.assetId === assetId) usages.push("panelHeader");
  if (appearance.panelSkin.borderTexture?.assetId === assetId) usages.push("panelBorder");
  for (const [slot, assignment] of Object.entries(appearance.icons)) if (assignment?.asset?.assetId === assetId) usages.push(`icon:${slot}`);
  for (const [slot, assignment] of Object.entries(appearance.decorations)) if (assignment?.asset?.assetId === assetId) usages.push(`decoration:${slot}`);
  return usages;
}

function usageToSurface(usage: string): ThemeEditorSurfaceId | null {
  if (usage === "background") return "global.background";
  if (usage === "panelBackground") return "panel.primary";
  if (usage === "panelHeader") return "panel.header";
  if (usage === "panelBorder") return "panel.border";
  if (usage.startsWith("icon:")) return `icon.${usage.slice("icon:".length)}` as ThemeEditorSurfaceId;
  if (usage.startsWith("decoration:")) return `decoration.${usage.slice("decoration:".length)}` as ThemeEditorSurfaceId;
  return null;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function getContrastWarnings(theme: SiteAppearanceTheme) {
  const warnings: string[] = [];
  if (contrastRatio(theme.textPrimaryColor, theme.panelColor) < 4.5) warnings.push("主文字与面板建议至少 4.5:1");
  if (contrastRatio(theme.accentColor, theme.panelColor) < 3) warnings.push("强调色与面板建议至少 3:1");
  return warnings;
}

function contrastRatio(first: string, second: string) {
  const firstLuminance = relativeLuminance(first);
  const secondLuminance = relativeLuminance(second);
  return (Math.max(firstLuminance, secondLuminance) + 0.05) / (Math.min(firstLuminance, secondLuminance) + 0.05);
}

function relativeLuminance(hex: string) {
  const channels = [hex.slice(1, 3), hex.slice(3, 5), hex.slice(5, 7)].map((value) => {
    const channel = Number.parseInt(value, 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return (0.2126 * channels[0]) + (0.7152 * channels[1]) + (0.0722 * channels[2]);
}
