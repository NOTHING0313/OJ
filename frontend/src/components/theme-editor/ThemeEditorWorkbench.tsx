import { createContext, type ChangeEvent, type DragEvent, type ReactNode, useContext, useEffect, useMemo, useReducer, useState } from "react";
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
  type ThemePreset,
  type ThemePresetList
} from "../../api/siteSettingsApi";
import { getApiErrorMessage } from "../../api/httpClient";
import { uploadImage } from "../../api/uploadsApi";
import { useTheme } from "../../theme/ThemeContext";
import { type ThemeDecorationSlot, type ThemeIconSlot } from "../../theme/themeSlots";
import { normalizeUploadedImagePath } from "../../utils/uploadedImageUrl";
import { ThemeEditorPreview } from "./ThemeEditorPreview";
import {
  ThemeEditorHistoryLimit,
  appearanceEquals,
  createThemeEditorHistory,
  getPreviewPageKey,
  getThemeSurface,
  getThemeSurfaceBreadcrumb,
  reduceThemeEditorHistory,
  resetThemeSurface,
  themeEditableSurfaces,
  themeEditorPreviewPages,
  themeEditorViewports,
  type ThemeEditorCompareMode,
  type ThemeEditorMode,
  type ThemeEditorPreviewPage,
  type ThemeEditorSurfaceId,
  type ThemeEditorViewport
} from "./themeEditorModel";

const ThemeEditorGestureContext = createContext({ begin: () => {}, end: () => {} });

export function ThemeEditorWorkbench() {
  const { siteAppearance, reloadSiteAppearance } = useTheme();
  const [history, dispatch] = useReducer(reduceThemeEditorHistory, siteAppearance, createThemeEditorHistory);
  const [selectedSurface, setSelectedSurface] = useState<ThemeEditorSurfaceId>("global.background");
  const [previewPage, setPreviewPage] = useState<ThemeEditorPreviewPage>("problem");
  const [pageBackgroundKey, setPageBackgroundKey] = useState<SitePageKey>("problems");
  const [viewport, setViewport] = useState<ThemeEditorViewport>("desktop");
  const [editorMode, setEditorMode] = useState<ThemeEditorMode>("select");
  const [compareMode, setCompareMode] = useState<ThemeEditorCompareMode>("draft");
  const [surfaceSearch, setSurfaceSearch] = useState("");
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
  const [pendingLibraryAction, setPendingLibraryAction] = useState<{ kind: "load" | "apply"; preset: ThemePreset } | null>(null);
  const [savePresetDialog, setSavePresetDialog] = useState<{ continuation: { kind: "load" | "apply"; preset: ThemePreset } | null } | null>(null);
  const [applyPresetDialog, setApplyPresetDialog] = useState<ThemePreset | null>(null);
  const [importPresetDialog, setImportPresetDialog] = useState<File | null>(null);
  const dirty = !appearanceEquals(history.saved, history.present);
  const previewAppearance = compareMode === "draft" ? history.present : compareMode === "saved" ? history.saved : createDefaultSiteAppearance();
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

  useEffect(() => dispatch({ type: "initialize", value: siteAppearance }), [siteAppearance]);
  useEffect(() => { void refreshAssets(); void refreshLibrary(); }, []);
  useUnsavedAppearanceGuard(dirty);

  function changeDraft(mutator: (draft: SiteAppearance) => void) {
    const next = normalizeSiteAppearance(history.present);
    mutator(next);
    dispatch({ type: "change", value: next });
    setCompareMode("draft");
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
      setError(getApiErrorMessage(reason, "Theme Library 加载失败。"));
    }
  }

  async function saveDraftAsPreset(requestedName: string, description: string | null) {
    const name = requestedName.trim();
    if (!name) { setError("Preset 名称不能为空。"); return false; }
    setLibraryBusy(true);
    setError(null);
    try {
      const created = await createThemePreset(name, description, history.present);
      setSelectedPresetId(created.id);
      await refreshLibrary();
      setNotice("当前 Draft 已保存为 Preset；正式 Appearance 未改变。");
      return true;
    } catch (reason) {
      setError(getApiErrorMessage(reason, "Preset 保存失败。"));
      return false;
    } finally {
      setLibraryBusy(false);
    }
  }

  function requestLibraryAction(kind: "load" | "apply", preset: ThemePreset) {
    if (dirty) setPendingLibraryAction({ kind, preset });
    else void executeLibraryAction(kind, preset);
  }

  async function executeLibraryAction(kind: "load" | "apply", preset: ThemePreset) {
    setPendingLibraryAction(null);
    setSelectedPresetId(preset.id);
    if (kind === "load") {
      dispatch({ type: "change", value: preset.appearance });
      setCompareMode("draft");
      setNotice(`${preset.name} 已载入 Draft；未发送 Appearance PUT。`);
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
      await reloadSiteAppearance();
      await Promise.all([refreshLibrary(), refreshAssets()]);
      setNotice(appearanceEquals(applied, preset.appearance)
        ? `${preset.name} 已通过正式 Appearance 更新路径应用。`
        : `${preset.name} 已应用；缺失资源已安全禁用并回退到默认视觉。`);
    } catch (reason) {
      setError(getApiErrorMessage(reason, "Theme 应用失败。"));
    } finally {
      setLibraryBusy(false);
    }
  }

  function saveThenContinuePendingAction() {
    const pending = pendingLibraryAction;
    if (!pending) return;
    setPendingLibraryAction(null);
    setSavePresetDialog({ continuation: pending });
  }

  async function confirmSavePreset(name: string, description: string | null) {
    const continuation = savePresetDialog?.continuation ?? null;
    if (!await saveDraftAsPreset(name, description)) return;
    setSavePresetDialog(null);
    if (continuation) await executeLibraryAction(continuation.kind, continuation.preset);
  }

  function discardThenContinuePendingAction() {
    const pending = pendingLibraryAction;
    if (!pending) return;
    dispatch({ type: "discard" });
    void executeLibraryAction(pending.kind, pending.preset);
  }

  async function handleUpdatePreset(preset: ThemePreset) {
    if (!preset.id || !window.confirm(`用当前 Draft 覆盖「${preset.name}」？正式 Appearance 不会改变。`)) return;
    setLibraryBusy(true);
    try {
      await updateThemePreset(preset.id, preset.name, preset.description, history.present);
      await refreshLibrary();
      setNotice(`${preset.name} 已更新；正式 Appearance 未改变。`);
    } catch (reason) { setError(getApiErrorMessage(reason, "Preset 更新失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleDuplicatePreset(preset: ThemePreset) {
    if (!preset.id) return;
    setLibraryBusy(true);
    try { const created = await duplicateThemePreset(preset.id); setSelectedPresetId(created.id); await refreshLibrary(); setNotice(`已创建 ${created.name}。`); }
    catch (reason) { setError(getApiErrorMessage(reason, "Preset 复制失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleRenamePreset(preset: ThemePreset) {
    if (!preset.id) return;
    const name = window.prompt("新的 Preset 名称", preset.name)?.trim();
    if (!name) return;
    setLibraryBusy(true);
    try { await renameThemePreset(preset.id, name); await refreshLibrary(); setNotice("Preset 已重命名。"); }
    catch (reason) { setError(getApiErrorMessage(reason, "Preset 重命名失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function handleDeletePreset(preset: ThemePreset) {
    if (!preset.id || !window.confirm(`删除「${preset.name}」？引用的资源文件不会被自动删除。`)) return;
    setLibraryBusy(true);
    try { await deleteThemePreset(preset.id); if (selectedPresetId === preset.id) setSelectedPresetId(undefined); await Promise.all([refreshLibrary(), refreshAssets()]); setNotice("Preset 已删除；资源文件保持不变。"); }
    catch (reason) { setError(getApiErrorMessage(reason, "Preset 删除失败。")); }
    finally { setLibraryBusy(false); }
  }

  async function performImportPreset(file: File) {
    setImportPresetDialog(null);
    setLibraryBusy(true);
    try { const imported = await importThemePreset(file); setSelectedPresetId(imported.id); await Promise.all([refreshLibrary(), refreshAssets()]); setNotice(`${imported.name} 已安全导入，尚未应用。`); }
    catch (reason) { setError(getApiErrorMessage(reason, "Theme Pack 导入失败。")); }
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
      setNotice("资源已上传并加入当前草稿；Save & Apply 前不会改变正式 Appearance。");
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
    setError(null);
    try {
      await deleteThemeAsset(assetId);
      await refreshAssets();
      setNotice("未被正式配置或当前草稿引用的资源已删除。");
    } catch (reason) {
      setError(getApiErrorMessage(reason, "资源删除失败。"));
    }
  }

  async function handleSave() {
    setIsSaving(true);
    setError(null);
    setNotice(null);
    try {
      const saved = normalizeSiteAppearance(await updateSiteAppearance(history.present));
      dispatch({ type: "save-success", value: saved });
      await reloadSiteAppearance();
      await refreshAssets();
      setCompareMode("draft");
      setNotice("Theme 已保存并应用。此次保存产生一次 SiteAppearance.Updated 审计事件。");
    } catch (reason) {
      setError(getApiErrorMessage(reason, "保存失败，当前 Draft 已保留，可修复后重试或 Discard。"));
    } finally {
      setIsSaving(false);
    }
  }

  function handleDiscard() {
    dispatch({ type: "discard" });
    setCompareMode("draft");
    setError(null);
    setNotice("Draft 已恢复到 Current Saved，服务器配置未发生额外变化。");
  }

  function handleResetSection() {
    dispatch({ type: "change", value: resetThemeSurface(history.present, selectedSurface, pageBackgroundKey) });
    setCompareMode("draft");
    setNotice(`${selected.label} 已恢复默认草稿，其他 Surface 保持不变。`);
  }

  function handleResetAll() {
    dispatch({ type: "change", value: createDefaultSiteAppearance() });
    setCompareMode("draft");
    setShowResetAll(false);
    setNotice("Entire Theme 已恢复为 Exact Default 草稿；资源文件未被自动删除。");
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
    <section className="theme-editor-page">
      <header className="theme-editor-page-header">
        <div><span>APPEARANCE / VISUAL WORKBENCH</span><h1>Visual Theme Editor</h1><p>Select → Preview → Edit → Undo → Compare → Save</p></div>
        <div className={`theme-editor-save-state${dirty ? " dirty" : ""}`}><span />{dirty ? "Unsaved Changes" : "Current Saved"}</div>
      </header>

      {(notice || error) && <div className={error ? "alert error" : "quiet-note success"} role="status">{error ?? notice}</div>}

      <div className="theme-editor-toolbar" aria-label="Editor Toolbar">
        <ToolbarGroup label="History">
          <button className="button" type="button" disabled={history.past.length === 0 || isSaving} onClick={() => dispatch({ type: "undo" })}>Undo</button>
          <button className="button" type="button" disabled={history.future.length === 0 || isSaving} onClick={() => dispatch({ type: "redo" })}>Redo</button>
          <small>{history.past.length}/{ThemeEditorHistoryLimit}</small>
        </ToolbarGroup>
        <ToolbarGroup label="Mode">
          <SegmentedButton active={editorMode === "preview"} onClick={() => setEditorMode("preview")}>Preview</SegmentedButton>
          <SegmentedButton active={editorMode === "select"} onClick={() => setEditorMode("select")}>Select</SegmentedButton>
        </ToolbarGroup>
        <ToolbarGroup label="Compare">
          {(["draft", "saved", "default"] as ThemeEditorCompareMode[]).map((mode) => <SegmentedButton key={mode} active={compareMode === mode} onClick={() => setCompareMode(mode)}>{mode === "draft" ? "Draft" : mode === "saved" ? "Current Saved" : "Default"}</SegmentedButton>)}
        </ToolbarGroup>
        <div className="theme-editor-toolbar-actions">
          <button className="button" type="button" disabled={!dirty || isSaving} onClick={handleDiscard}>Discard Changes</button>
          <button className="button" type="button" disabled={isSaving} onClick={() => setShowResetAll(true)}>Reset Entire Theme</button>
          <button className="button primary" type="button" disabled={!dirty || isSaving || isUploading} onClick={() => void handleSave()}>{isSaving ? "Saving..." : "Save & Apply"}</button>
        </div>
      </div>

      <section className="theme-library-panel" aria-label="Theme Library">
        <header><div><span>THEME LIBRARY</span><h2>Presets</h2><p>保存、预览、导入或应用可移植主题；运行时仍只读取正式 Appearance。</p></div><div><input value={presetSearch} onChange={(event) => setPresetSearch(event.target.value)} placeholder="Search presets..." aria-label="Search theme presets" /><select value={presetSort} onChange={(event) => setPresetSort(event.target.value as "updated" | "name")} aria-label="Sort theme presets"><option value="updated">Updated</option><option value="name">Name</option></select><label className="button">Import<input type="file" accept=".zip,application/zip" disabled={libraryBusy} onChange={(event) => { const file = event.target.files?.[0]; if (file) setImportPresetDialog(file); event.currentTarget.value = ""; }} /></label><button className="button primary" type="button" disabled={libraryBusy} onClick={() => setSavePresetDialog({ continuation: null })}>Save As</button></div></header>
        <div className="theme-library-grid">
          {visiblePresets.map((preset) => <article key={preset.id ?? "default"} className={selectedPresetId !== undefined && selectedPresetId === preset.id ? "selected" : ""}>
            <div className="theme-library-swatch" style={{ background: `linear-gradient(135deg, ${preset.appearance.theme.panelColor}, ${preset.appearance.theme.accentColor})` }} aria-hidden="true" />
            <div className="theme-library-meta"><div><strong>{preset.name}</strong><span>{preset.isBuiltIn ? "SYSTEM" : "CUSTOM"}</span></div><p>{preset.description || (preset.isBuiltIn ? "Exact built-in default" : "Portable SiteAppearance preset")}</p><small>{preset.assetCount} assets · {preset.updatedAt ? new Date(preset.updatedAt).toLocaleString() : "Built-in"}{themeLibrary.lastAppliedPresetId === preset.id && preset.id ? " · last applied" : ""}</small></div>
            <div className="theme-library-actions"><button type="button" onClick={() => requestLibraryAction("load", preset)}>Load</button><button type="button" className="primary" onClick={() => requestLibraryAction("apply", preset)}>Apply</button>{!preset.isBuiltIn && <details><summary>More</summary><div><button type="button" onClick={() => void handleUpdatePreset(preset)}>Save Draft</button><button type="button" onClick={() => void handleDuplicatePreset(preset)}>Duplicate</button><button type="button" onClick={() => void handleRenamePreset(preset)}>Rename</button><button type="button" onClick={() => preset.id && void exportThemePreset(preset.id, preset.name)}>Export</button><button type="button" className="danger" onClick={() => void handleDeletePreset(preset)}>Delete</button></div></details>}</div>
          </article>)}
          {visiblePresets.length === 0 && <div className="theme-library-empty">No matching presets.</div>}
        </div>
      </section>

      <div className="theme-editor-context-bar">
        <label>Preview Page<select value={previewPage} onChange={(event) => changePreviewPage(event.target.value as ThemeEditorPreviewPage)}>{themeEditorPreviewPages.map((option) => <option key={option.key} value={option.key}>{option.label}</option>)}</select></label>
        <div className="theme-editor-viewport-switch" aria-label="Preview viewport">{themeEditorViewports.map((option) => <SegmentedButton key={option.key} active={viewport === option.key} onClick={() => setViewport(option.key)}>{option.label}<small>{option.width}</small></SegmentedButton>)}</div>
        <div className="theme-editor-quick-actions" aria-label="Quick Actions">
          <button type="button" onClick={() => selectSurface("global.background")}>Background</button>
          <button type="button" onClick={() => selectSurface("panel.primary")}>Panels</button>
          <button type="button" onClick={() => selectSurface("global.colors")}>Colors</button>
          <button type="button" onClick={() => selectSurface("icon.problem")}>Icons</button>
          <button type="button" onClick={() => selectSurface("decoration.pageHeader")}>Decorations</button>
        </div>
      </div>

      <div className="theme-editor-workbench">
        <aside className="theme-editor-navigator" aria-label="Surface Navigator">
          <div className="theme-editor-pane-heading"><div><span>SURFACES</span><strong>Navigator</strong></div><b>{filteredSurfaces.length}</b></div>
          <label className="theme-editor-search"><span>Search</span><input value={surfaceSearch} onChange={(event) => setSurfaceSearch(event.target.value)} placeholder="problem, panel, background..." /></label>
          {(["Global", "Panels", "Icons", "Decorations"] as const).map((group) => {
            const surfaces = filteredSurfaces.filter((surface) => surface.group === group);
            return surfaces.length > 0 && <section key={group}><h2>{group}</h2>{surfaces.map((surface) => <button key={surface.id} type="button" className={selectedSurface === surface.id ? "active" : ""} onClick={() => selectSurface(surface.id)}><strong>{surface.label}</strong><small>{surface.description}</small></button>)}</section>;
          })}
        </aside>

        <section className="theme-editor-stage" aria-label="Preview Canvas">
          <div className="theme-editor-stage-heading"><div><span>{compareMode.toUpperCase()} PREVIEW</span><strong>{themeEditorPreviewPages.find((item) => item.key === previewPage)?.label}</strong></div><small>{editorMode === "select" ? "点击受控 Surface 选择；蓝色轮廓仅存在于 Editor" : "纯预览模式"}</small></div>
          <div className="theme-editor-canvas-scroll">
            <ThemeEditorPreview appearance={previewAppearance} page={previewPage} pageBackgroundKey={pageBackgroundKey} viewport={viewport} mode={editorMode} selectedSurface={selectedSurface} onSelect={selectSurface} onBackgroundPositionChange={(positionX, positionY) => changeDraft((draft) => { draft.background.positionX = Math.round(positionX); draft.background.positionY = Math.round(positionY); })} onGestureStart={gestureControls.begin} onGestureEnd={gestureControls.end} />
          </div>
          {viewport === "mobile" && <p className="theme-editor-mobile-note">移动端提供基础编辑；建议使用桌面端完成高效率 Theme 调整。</p>}
        </section>

        <aside className="theme-editor-inspector" aria-label="Property Inspector">
          <div className="theme-editor-pane-heading"><div><span>INSPECTOR</span><strong>{selected.label}</strong></div></div>
          <div className="theme-editor-breadcrumb">{getThemeSurfaceBreadcrumb(selectedSurface)}</div>
          <p className="theme-editor-inspector-description">{selected.description}</p>
          <ThemePropertyInspector appearance={history.present} page={previewPage} pageBackgroundKey={pageBackgroundKey} onPageBackgroundKeyChange={setPageBackgroundKey} surface={selectedSurface} assets={themeAssets} disabled={isSaving} uploading={isUploading} onChange={changeDraft} onAssignAsset={assignAsset} onUpload={handleAssetUpload} onOpenLibrary={() => setShowAssetLibrary(true)} />
          <button className="button theme-editor-reset-section" type="button" disabled={isSaving} onClick={handleResetSection}>Reset Section</button>
        </aside>
      </div>

      {showAssetLibrary && <AssetLibraryDialog assets={themeAssets} draft={history.present} onClose={() => setShowAssetLibrary(false)} onSelect={(asset) => { assignAsset(asset); setShowAssetLibrary(false); }} onDelete={handleDeleteAsset} onNavigate={(surface) => { selectSurface(surface); setShowAssetLibrary(false); }} />}
      {showResetAll && <ConfirmDialog title="Reset Entire Theme?" description="全部自定义配置将进入 Default Theme 草稿。资源文件不会自动删除；只有 Save & Apply 后才影响正式 Appearance。" confirmLabel="Reset Draft" onCancel={() => setShowResetAll(false)} onConfirm={handleResetAll} />}
      {pendingLibraryAction && <DraftTransitionDialog target={pendingLibraryAction.preset.name} onSave={saveThenContinuePendingAction} onDiscard={discardThenContinuePendingAction} onCancel={() => setPendingLibraryAction(null)} />}
      {savePresetDialog && <PresetSaveDialog busy={libraryBusy} onCancel={() => setSavePresetDialog(null)} onSave={(name, description) => void confirmSavePreset(name, description)} />}
      {applyPresetDialog && <ConfirmDialog title="Apply Theme to Site?" description={`「${applyPresetDialog.name}」将通过正式 Appearance 更新路径应用到全站。缺失资源会安全回退。`} confirmLabel="Apply to Site" onCancel={() => setApplyPresetDialog(null)} onConfirm={() => void performApply(applyPresetDialog)} />}
      {importPresetDialog && <ConfirmDialog title="Import Theme Pack?" description={`${importPresetDialog.name} 将由服务器校验 manifest、路径、ZIP 边界、图片类型与签名。成功后仅加入 Library，不会自动应用。`} confirmLabel="Validate & Import" onCancel={() => setImportPresetDialog(null)} onConfirm={() => void performImportPreset(importPresetDialog)} />}
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
    return <InspectorSection title={`${page} Page Background`}><EnumControl label="Page Contract" value={key} disabled={disabled} options={sitePageOptions.map((option) => option.key)} onChange={(next) => onPageBackgroundKeyChange(next as SitePageKey)} /><ToggleControl label="Enabled" checked={value.enabled} disabled={disabled || !value.imageUrl} onChange={(enabled) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], enabled }; })} /><PageBackgroundAsset value={value.imageUrl} disabled={disabled} uploading={uploading} onUpload={onUpload} onClear={() => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], enabled: false, imageUrl: null }; })} /><NumericControl label="Position X" value={value.positionX} min={0} max={100} step={1} disabled={disabled} onChange={(positionX) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], positionX }; })} /><NumericControl label="Position Y" value={value.positionY} min={0} max={100} step={1} disabled={disabled} onChange={(positionY) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], positionY }; })} /><NumericControl label="Scale" value={value.scale} min={0.5} max={2.5} step={0.05} disabled={disabled} onChange={(scale) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], scale }; })} /><NumericControl label="Overlay" value={value.overlayOpacity ?? appearance.theme.backgroundOverlayOpacity} min={0} max={1} step={0.05} disabled={disabled} onChange={(overlayOpacity) => onChange((draft) => { draft.pages[key] = { ...draft.pages[key], overlayOpacity }; })} /></InspectorSection>;
  }
  if (surface === "panel.primary") return <PanelInspector value={appearance.panelSkin} assets={assets} disabled={disabled} uploading={uploading} onChange={(patch) => onChange((draft) => { draft.panelSkin = { ...draft.panelSkin, ...patch }; })} onAssignAsset={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />;
  if (surface === "panel.header" || surface === "panel.border") {
    const value = surface === "panel.header" ? appearance.panelSkin.headerTexture : appearance.panelSkin.borderTexture;
    return <InspectorSection title={surface === "panel.header" ? "Header Texture" : "Border Texture"}><AssetProperty value={value} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /></InspectorSection>;
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
  return <InspectorSection title="Global Background"><ToggleControl label="Enabled" checked={value.enabled} disabled={disabled || !value.asset} onChange={(enabled) => onChange({ enabled })} /><AssetProperty value={value.asset} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /><NumericControl label="Position X" value={value.positionX ?? 50} min={0} max={100} step={1} disabled={disabled} onChange={(positionX) => onChange({ positionX })} /><NumericControl label="Position Y" value={value.positionY ?? 50} min={0} max={100} step={1} disabled={disabled} onChange={(positionY) => onChange({ positionY })} /><EnumControl label="Size" value={value.sizeMode ?? "cover"} disabled={disabled} options={["cover", "contain", "auto"]} onChange={(sizeMode) => onChange({ sizeMode: sizeMode as SiteThemeBackground["sizeMode"] })} /><EnumControl label="Repeat" value={value.repeat ?? "no-repeat"} disabled={disabled} options={["no-repeat", "repeat", "repeat-x", "repeat-y"]} onChange={(repeat) => onChange({ repeat: repeat as SiteThemeBackground["repeat"] })} /><EnumControl label="Attachment" value={value.attachment ?? "scroll"} disabled={disabled} options={["scroll", "fixed"]} onChange={(attachment) => onChange({ attachment: attachment as SiteThemeBackground["attachment"] })} /><ColorControl label="Overlay Color" value={value.overlayColor ?? "#000000"} disabled={disabled} onChange={(overlayColor) => onChange({ overlayColor })} /><NumericControl label="Overlay Opacity" value={value.overlayOpacity ?? 0.45} min={0} max={1} step={0.05} disabled={disabled} onChange={(overlayOpacity) => onChange({ overlayOpacity })} /><NumericControl label="Blur" value={value.blur ?? 0} min={0} max={20} step={1} disabled={disabled} onChange={(blur) => onChange({ blur })} /><NumericControl label="Brightness" value={value.brightness ?? 100} min={50} max={150} step={5} disabled={disabled} onChange={(brightness) => onChange({ brightness })} /></InspectorSection>;
}

function PanelInspector({ value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { value: SitePanelSkin; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (patch: Partial<SitePanelSkin>) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="Primary Panel"><ToggleControl label="Enabled" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ enabled })} /><AssetProperty value={value.backgroundTexture} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} /><NumericControl label="Background Opacity" value={value.backgroundOpacity ?? 1} min={0} max={1} step={0.05} disabled={disabled} onChange={(backgroundOpacity) => onChange({ backgroundOpacity })} /><NumericControl label="Texture Opacity" value={value.textureOpacity ?? 0.15} min={0} max={1} step={0.05} disabled={disabled} onChange={(textureOpacity) => onChange({ textureOpacity })} /><NumericControl label="Radius" value={value.radius ?? 8} min={0} max={32} step={1} disabled={disabled} onChange={(radius) => onChange({ radius })} /><NumericControl label="Shadow" value={value.shadowStrength ?? 0.25} min={0} max={1} step={0.05} disabled={disabled} onChange={(shadowStrength) => onChange({ shadowStrength })} /></InspectorSection>;
}

function TokenInspector({ value, disabled, onChange }: { value: SiteAppearanceTheme; disabled: boolean; onChange: (patch: Partial<SiteAppearanceTheme>) => void }) {
  const warnings = getContrastWarnings(value);
  return <InspectorSection title="Design Tokens"><ToggleControl label="Root Theme Enabled" checked={value.backgroundEnabled} disabled={disabled} onChange={(backgroundEnabled) => onChange({ backgroundEnabled })} /><NumericControl label="Page Overlay" value={value.backgroundOverlayOpacity} min={0} max={1} step={0.05} disabled={disabled} onChange={(backgroundOverlayOpacity) => onChange({ backgroundOverlayOpacity })} /><ColorControl label="Panel Color" value={value.panelColor} disabled={disabled} onChange={(panelColor) => onChange({ panelColor })} /><ColorControl label="Text Primary" value={value.textPrimaryColor} disabled={disabled} onChange={(textPrimaryColor) => onChange({ textPrimaryColor })} /><ColorControl label="Text Secondary" value={value.textSecondaryColor} disabled={disabled} onChange={(textSecondaryColor) => onChange({ textSecondaryColor })} /><ColorControl label="Text Muted" value={value.textMutedColor} disabled={disabled} onChange={(textMutedColor) => onChange({ textMutedColor })} /><ColorControl label="Accent" value={value.accentColor} disabled={disabled} onChange={(accentColor) => onChange({ accentColor })} /><ColorControl label="Navigation Text" value={value.navTextColor} disabled={disabled} onChange={(navTextColor) => onChange({ navTextColor })} /><ColorControl label="Navigation Active" value={value.navActiveColor} disabled={disabled} onChange={(navActiveColor) => onChange({ navActiveColor })} /><NumericControl label="Panel Opacity" value={value.panelOpacity} min={0.35} max={0.95} step={0.05} disabled={disabled} onChange={(panelOpacity) => onChange({ panelOpacity })} /><NumericControl label="Panel Blur" value={value.panelBlur} min={0} max={30} step={1} disabled={disabled} onChange={(panelBlur) => onChange({ panelBlur })} /><NumericControl label="Border Opacity" value={value.panelBorderOpacity} min={0} max={0.5} step={0.01} disabled={disabled} onChange={(panelBorderOpacity) => onChange({ panelBorderOpacity })} /><NumericControl label="Navigation Opacity" value={value.navOpacity} min={0.35} max={1} step={0.05} disabled={disabled} onChange={(navOpacity) => onChange({ navOpacity })} /><NumericControl label="Navigation Blur" value={value.navBlur} min={0} max={30} step={1} disabled={disabled} onChange={(navBlur) => onChange({ navBlur })} /><EnumControl label="Font" value={value.fontPreset} disabled={disabled} options={["system", "readable", "mono"]} onChange={(fontPreset) => onChange({ fontPreset: fontPreset as SiteAppearanceTheme["fontPreset"] })} /><div className="theme-editor-token-presets"><button type="button" disabled={disabled} onClick={() => onChange({ panelColor: "#0F141D", panelOpacity: 0.9, panelBlur: 8, panelBorderOpacity: 0.18, textPrimaryColor: "#F7F9FC", textSecondaryColor: "#C5CCDA", textMutedColor: "#929CAE", accentColor: "#7B86FF", navOpacity: 0.82, navBlur: 10, navTextColor: "#E1E6EF", navActiveColor: "#FFFFFF", fontPreset: "readable" })}>深色高对比</button><button type="button" disabled={disabled} onClick={() => onChange({ panelColor: "#111827", panelOpacity: 0.62, panelBlur: 16, panelBorderOpacity: 0.15, textPrimaryColor: "#F4F7FB", textSecondaryColor: "#BCC5D4", textMutedColor: "#8792A6", accentColor: "#7C87FF", navOpacity: 0.58, navBlur: 18, navTextColor: "#DDE3EE", navActiveColor: "#FFFFFF", fontPreset: "system" })}>轻透玻璃</button><button type="button" disabled={disabled} onClick={() => onChange(createDefaultSiteAppearance().theme)}>恢复 Token 默认</button></div>{warnings.length > 0 && <p className="theme-editor-contrast-warning" role="status">对比度提示：{warnings.join("；")}</p>}</InspectorSection>;
}

function SlotInspector({ value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { value: SiteThemeIconSlot | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (value: SiteThemeIconSlot | null) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="Icon Slot"><AssetProperty value={value?.asset ?? null} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />{value && <><ToggleControl label="Enabled" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ ...value, enabled })} /><SlotNumericProperties value={value} disabled={disabled} onChange={(patch) => onChange({ ...value, ...patch })} /></>}</InspectorSection>;
}

function DecorationInspector({ slot, value, assets, disabled, uploading, onChange, onAssignAsset, onUpload, onOpenLibrary }: { slot: ThemeDecorationSlot; value: SiteThemeDecorationSlot | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChange: (value: SiteThemeDecorationSlot | null) => void; onAssignAsset: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  return <InspectorSection title="Decoration Slot"><AssetProperty value={value?.asset ?? null} assets={assets} disabled={disabled} uploading={uploading} onChoose={onAssignAsset} onUpload={onUpload} onOpenLibrary={onOpenLibrary} />{value && <><ToggleControl label="Enabled" checked={value.enabled} disabled={disabled} onChange={(enabled) => onChange({ ...value, enabled })} /><SlotNumericProperties value={value} disabled={disabled} onChange={(patch) => onChange({ ...value, ...patch })} />{slot === "panelCorner" ? <EnumControl label="Corner" value={value.corner ?? "top-right"} disabled={disabled} options={["top-left", "top-right", "bottom-left", "bottom-right"]} onChange={(corner) => onChange({ ...value, corner: corner as SiteThemeDecorationSlot["corner"] })} /> : <EnumControl label="Alignment" value={value.alignment ?? "end"} disabled={disabled} options={["start", "center", "end"]} onChange={(alignment) => onChange({ ...value, alignment: alignment as SiteThemeDecorationSlot["alignment"] })} />}</>}</InspectorSection>;
}

function SlotNumericProperties({ value, disabled, onChange }: { value: SiteThemeIconSlot; disabled: boolean; onChange: (patch: Partial<SiteThemeIconSlot>) => void }) {
  return <><NumericControl label="Opacity" value={value.opacity ?? 1} min={0} max={1} step={0.05} disabled={disabled} onChange={(opacity) => onChange({ opacity })} /><NumericControl label="Scale" value={value.scale ?? 1} min={0.5} max={2} step={0.05} disabled={disabled} onChange={(scale) => onChange({ scale })} /><NumericControl label="Offset X" value={value.offsetX ?? 0} min={-64} max={64} step={1} disabled={disabled} onChange={(offsetX) => onChange({ offsetX })} /><NumericControl label="Offset Y" value={value.offsetY ?? 0} min={-64} max={64} step={1} disabled={disabled} onChange={(offsetY) => onChange({ offsetY })} /></>;
}

function AssetProperty({ value, assets, disabled, uploading, onChoose, onUpload, onOpenLibrary }: { value: ThemeAssetReference | null; assets: ThemeAssetLibraryItem[]; disabled: boolean; uploading: boolean; onChoose: (asset: ThemeAssetReference | null) => void; onUpload: (file: File) => Promise<void>; onOpenLibrary: () => void }) {
  const metadata = assets.find((asset) => asset.assetId === value?.assetId);
  return <div className="theme-editor-asset-property"><div className="theme-editor-asset-preview">{value ? <AssetThumbnail asset={{ ...metadata, ...value } as ThemeAssetLibraryItem} /> : <span>No Asset</span>}</div><div className="theme-editor-asset-meta"><strong>{value?.assetId ?? "Default / Empty"}</strong><small>{metadata ? `${metadata.contentType} · ${formatBytes(metadata.size)} · Used By ${metadata.usedBy.length}` : "Choose Existing, Upload New, or Clear"}</small></div><select aria-label="Choose Existing" value={value?.assetId ?? ""} disabled={disabled} onChange={(event) => { const asset = assets.find((item) => item.assetId === event.target.value); onChoose(asset ? toReference(asset) : null); }}><option value="">Choose Existing</option>{assets.map((asset) => <option key={asset.assetId} value={asset.assetId}>{asset.assetId} · {formatBytes(asset.size)}</option>)}</select><AssetDropZone disabled={disabled} uploading={uploading} onUpload={onUpload} /><div className="theme-editor-asset-actions"><button className="button" type="button" disabled={disabled} onClick={onOpenLibrary}>Asset Library</button><button className="button" type="button" disabled={disabled || !value} onClick={() => onChoose(null)}>Clear</button></div></div>;
}

function PageBackgroundAsset({ value, disabled, uploading, onUpload, onClear }: { value: string | null; disabled: boolean; uploading: boolean; onUpload: (file: File) => Promise<void>; onClear: () => void }) {
  return <div className="theme-editor-asset-property"><div className="theme-editor-asset-preview">{value ? <img src={resolveSiteAssetUrl(value)} alt="Page background preview" /> : <span>No Image</span>}</div><div className="theme-editor-asset-meta"><strong>Page Background Image</strong><small>{value ?? "Existing page-level upload contract"}</small></div><AssetDropZone disabled={disabled} uploading={uploading} onUpload={onUpload} /><button className="button" type="button" disabled={disabled || !value} onClick={onClear}>Clear</button></div>;
}

function AssetDropZone({ disabled, uploading, onUpload }: { disabled: boolean; uploading: boolean; onUpload: (file: File) => Promise<void> }) {
  function receive(files: FileList | null) { const file = files?.[0]; if (file) void onUpload(file); }
  return <label className={`theme-editor-drop-zone${disabled ? " disabled" : ""}`} onDragOver={(event: DragEvent<HTMLLabelElement>) => { if (!disabled) event.preventDefault(); }} onDrop={(event: DragEvent<HTMLLabelElement>) => { event.preventDefault(); if (!disabled) receive(event.dataTransfer.files); }}><strong>{uploading ? "Uploading..." : "Drop image or browse"}</strong><small>PNG / JPEG / WebP · SecureUploadValidator</small><input type="file" accept="image/png,image/jpeg,image/webp" disabled={disabled || uploading} onChange={(event: ChangeEvent<HTMLInputElement>) => { receive(event.target.files); event.target.value = ""; }} /></label>;
}

function AssetLibraryDialog({ assets, draft, onClose, onSelect, onDelete, onNavigate }: { assets: ThemeAssetLibraryItem[]; draft: SiteAppearance; onClose: () => void; onSelect: (asset: ThemeAssetReference) => void; onDelete: (assetId: string) => Promise<void>; onNavigate: (surface: ThemeEditorSurfaceId) => void }) {
  return <div className="theme-editor-modal-backdrop" role="presentation"><section className="theme-editor-modal asset-library" role="dialog" aria-modal="true" aria-labelledby="asset-library-title"><header><div><span>SHARED ASSET PICKER</span><h2 id="asset-library-title">Theme Asset Library</h2></div><button type="button" onClick={onClose} aria-label="Close asset library">×</button></header>{assets.length === 0 ? <div className="empty-state">暂无 Theme Asset</div> : <div className="theme-editor-asset-library">{assets.map((asset) => { const usages = [...new Set([...asset.usedBy, ...getDraftAssetUsages(draft, asset.assetId)])]; return <article key={asset.assetId}><AssetThumbnail asset={asset} /><div><strong>{asset.assetId}</strong><span>{asset.contentType} · {formatBytes(asset.size)}</span><small>Used By</small><div className="theme-editor-usage-list">{usages.length === 0 ? <em>未引用</em> : usages.map((usage) => { const surface = usageToSurface(usage); return surface ? <button key={usage} type="button" onClick={() => onNavigate(surface)}>{usage}</button> : <span key={usage}>{usage}</span>; })}</div></div><div><button className="button primary" type="button" onClick={() => onSelect(toReference(asset))}>Choose</button><button className="button" type="button" disabled={usages.length > 0} onClick={() => void onDelete(asset.assetId)}>Delete</button></div></article>; })}</div>}</section></div>;
}

function AssetThumbnail({ asset }: { asset: ThemeAssetLibraryItem }) {
  const [resolution, setResolution] = useState<string | null>(null);
  return <figure><img src={resolveSiteAssetUrl(asset.url)} alt="Theme asset thumbnail" onLoad={(event) => setResolution(`${event.currentTarget.naturalWidth}×${event.currentTarget.naturalHeight}`)} /><figcaption>{resolution ?? "Resolution pending"}</figcaption></figure>;
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
  return <label className="theme-editor-enum"><span>{label}</span><select value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)}>{options.map((option) => <option key={option} value={option}>{option}</option>)}</select></label>;
}

function ToggleControl({ label, checked, disabled, onChange }: { label: string; checked: boolean; disabled: boolean; onChange: (checked: boolean) => void }) {
  return <label className="theme-editor-toggle"><span>{label}</span><input type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /><i aria-hidden="true" /></label>;
}

function InspectorSection({ title, children }: { title: string; children: ReactNode }) {
  return <section className="theme-editor-inspector-section"><h2>{title}</h2>{children}</section>;
}

function ToolbarGroup({ label, children }: { label: string; children: ReactNode }) {
  return <div className="theme-editor-toolbar-group"><span>{label}</span><div>{children}</div></div>;
}

function SegmentedButton({ active, children, onClick }: { active: boolean; children: ReactNode; onClick: () => void }) {
  return <button type="button" className={active ? "active" : ""} aria-pressed={active} onClick={onClick}>{children}</button>;
}

function ConfirmDialog({ title, description, confirmLabel, onCancel, onConfirm }: { title: string; description: string; confirmLabel: string; onCancel: () => void; onConfirm: () => void }) {
  return <div className="theme-editor-modal-backdrop" role="presentation"><section className="theme-editor-modal confirm" role="dialog" aria-modal="true" aria-labelledby="theme-editor-confirm-title"><h2 id="theme-editor-confirm-title">{title}</h2><p>{description}</p><div><button className="button" type="button" onClick={onCancel}>Cancel</button><button className="button primary" type="button" onClick={onConfirm}>{confirmLabel}</button></div></section></div>;
}

function DraftTransitionDialog({ target, onSave, onDiscard, onCancel }: { target: string; onSave: () => void; onDiscard: () => void; onCancel: () => void }) {
  return <div className="theme-editor-modal-backdrop" role="presentation"><section className="theme-editor-modal confirm" role="dialog" aria-modal="true" aria-labelledby="theme-draft-transition-title"><h2 id="theme-draft-transition-title">Unsaved Theme Draft</h2><p>切换到「{target}」前，请保存当前 Draft、丢弃修改或取消。</p><div><button className="button" type="button" onClick={onCancel}>Cancel</button><button className="button danger" type="button" onClick={onDiscard}>Discard</button><button className="button primary" type="button" onClick={onSave}>Save Draft</button></div></section></div>;
}

function PresetSaveDialog({ busy, onCancel, onSave }: { busy: boolean; onCancel: () => void; onSave: (name: string, description: string | null) => void }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  return <div className="theme-editor-modal-backdrop" role="presentation"><section className="theme-editor-modal confirm theme-preset-save-dialog" role="dialog" aria-modal="true" aria-labelledby="theme-preset-save-title"><h2 id="theme-preset-save-title">Save Draft as Preset</h2><p>保存到 Theme Library，不会改变当前全站 Appearance。</p><label>Name<input autoFocus maxLength={64} value={name} onChange={(event) => setName(event.target.value)} placeholder="Theme name" /></label><label>Description <small>optional</small><textarea maxLength={256} value={description} onChange={(event) => setDescription(event.target.value)} placeholder="Short plain-text description" /></label><div><button className="button" type="button" disabled={busy} onClick={onCancel}>Cancel</button><button className="button primary" type="button" disabled={busy || !name.trim()} onClick={() => onSave(name, description.trim() || null)}>{busy ? "Saving..." : "Save Preset"}</button></div></section></div>;
}

function useUnsavedAppearanceGuard(dirty: boolean) {
  useEffect(() => {
    function beforeUnload(event: BeforeUnloadEvent) { if (dirty) event.preventDefault(); }
    function captureNavigation(event: MouseEvent) {
      if (!dirty || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = (event.target as Element | null)?.closest("a[href]") as HTMLAnchorElement | null;
      if (!anchor || anchor.target === "_blank") return;
      const destination = new URL(anchor.href, window.location.href);
      if (destination.origin !== window.location.origin || destination.href === window.location.href) return;
      if (!window.confirm("存在未保存的 Theme 修改，确定离开编辑器吗？")) event.preventDefault();
    }
    window.addEventListener("beforeunload", beforeUnload);
    document.addEventListener("click", captureNavigation, true);
    return () => { window.removeEventListener("beforeunload", beforeUnload); document.removeEventListener("click", captureNavigation, true); };
  }, [dirty]);
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
  if (contrastRatio(theme.textPrimaryColor, theme.panelColor) < 4.5) warnings.push("主文字与 Panel 建议至少 4.5:1");
  if (contrastRatio(theme.accentColor, theme.panelColor) < 3) warnings.push("Accent 与 Panel 建议至少 3:1");
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
