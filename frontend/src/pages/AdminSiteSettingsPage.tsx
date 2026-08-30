import { type ChangeEvent, type CSSProperties, type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import {
  createDefaultPageBackground,
  createDefaultSiteAppearance,
  normalizeSiteAppearance,
  resolveSiteAssetUrl,
  sitePageOptions,
  updateSiteAppearance,
  uploadThemeAsset,
  type SiteAppearance,
  type SiteAppearanceTheme,
  type SitePanelSkin,
  type SitePageBackground,
  type SitePageKey,
  type SiteThemeBackground,
  type ThemeAssetReference
} from "../api/siteSettingsApi";
import { uploadImage } from "../api/uploadsApi";
import { normalizeUploadedImagePath } from "../utils/uploadedImageUrl";
import { useTheme } from "../theme/ThemeContext";

interface SettingSliderProps {
  label: string;
  value: number;
  valueLabel: string;
  min: number;
  max: number;
  step: number;
  disabled?: boolean;
  onChange: (value: number) => void;
}

interface SettingsSwitchProps {
  checked: boolean;
  label: string;
  description: string;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}

type VisualSettingsTab = "text" | "panel" | "navigation" | "presets";
type ThemeAssetSlot = "background" | "panelBackground" | "panelHeader" | "panelBorder";

export function AdminSiteSettingsPage() {
  const { siteAppearance, reloadSiteAppearance, currentTheme } = useTheme();
  const [selectedPageKey, setSelectedPageKey] = useState<SitePageKey>("global");
  const [initialConfig, setInitialConfig] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [draftConfig, setDraftConfig] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadingThemeSlot, setUploadingThemeSlot] = useState<ThemeAssetSlot | null>(null);
  const [visualTab, setVisualTab] = useState<VisualSettingsTab>("text");
  const fileInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    const normalized = normalizeSiteAppearance(siteAppearance);
    setInitialConfig(normalized);
    setDraftConfig(normalizeSiteAppearance(normalized));
  }, [siteAppearance]);

  const isDirty = useMemo(
    () => JSON.stringify(initialConfig) !== JSON.stringify(draftConfig),
    [draftConfig, initialConfig]
  );

  const selectedOption = sitePageOptions.find((option) => option.key === selectedPageKey) ?? sitePageOptions[0];
  const selectedPage = draftConfig.pages[selectedPageKey] ?? createDefaultPageBackground();
  const selectedEditorValue = {
    enabled: selectedPage.enabled,
    imageUrl: selectedPage.imageUrl,
    positionX: selectedPage.positionX,
    positionY: selectedPage.positionY,
    scale: selectedPage.scale,
    overlayOpacity: selectedPage.overlayOpacity ?? draftConfig.theme.backgroundOverlayOpacity
  };
  const pageControlsDisabled = !selectedEditorValue.enabled || isSaving;

  function updateTheme(patch: Partial<SiteAppearanceTheme>) {
    setDraftConfig((current) => ({ ...current, theme: { ...current.theme, ...patch } }));
  }

  function updateGenericBackground(patch: Partial<SiteThemeBackground>) {
    setDraftConfig((current) => ({ ...current, background: { ...current.background, ...patch } }));
  }

  function updatePanelSkin(patch: Partial<SitePanelSkin>) {
    setDraftConfig((current) => ({ ...current, panelSkin: { ...current.panelSkin, ...patch } }));
  }

  async function handleThemeAssetUpload(slot: ThemeAssetSlot, file: File) {
    setUploadingThemeSlot(slot);
    setError(null);
    setNotice(null);
    try {
      const asset = await uploadThemeAsset(file);
      const reference = { assetId: asset.assetId, url: asset.url };
      applyThemeAsset(slot, reference);
      setNotice("主题图片已安全上传并加入本地草稿，请保存配置后启用。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "主题图片上传失败。");
    } finally {
      setUploadingThemeSlot(null);
    }
  }

  function applyThemeAsset(slot: ThemeAssetSlot, asset: ThemeAssetReference | null) {
    if (slot === "background") updateGenericBackground({ asset });
    if (slot === "panelBackground") updatePanelSkin({ backgroundTexture: asset });
    if (slot === "panelHeader") updatePanelSkin({ headerTexture: asset });
    if (slot === "panelBorder") updatePanelSkin({ borderTexture: asset });
  }

  function updateSelectedPage(patch: Partial<SitePageBackground>) {
    setDraftConfig((current) => ({
      ...current,
      pages: {
        ...current.pages,
        [selectedPageKey]: {
          ...(current.pages[selectedPageKey] ?? createDefaultPageBackground()),
          ...patch
        }
      }
    }));
  }

  async function handleUpload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setIsUploading(true);
    setError(null);
    setNotice(null);

    try {
      const result = await uploadImage(file);
      updateSelectedPage({ imageUrl: normalizeUploadedImagePath(result.url), enabled: true });
      setNotice("背景图已上传，请保存配置。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "背景图上传失败。");
    } finally {
      setIsUploading(false);
      event.target.value = "";
    }
  }

  function handleClearBackground() {
    updateSelectedPage({ imageUrl: null, enabled: false });
  }

  function handleDiscard() {
    setDraftConfig(normalizeSiteAppearance(initialConfig));
    setError(null);
    setNotice("已放弃未保存修改。");
  }

  function handleRestoreDefault() {
    const defaults = createDefaultSiteAppearance();
    setDraftConfig((current) => ({
      ...current,
      theme: {
        ...current.theme,
        backgroundOverlayOpacity: defaults.theme.backgroundOverlayOpacity
      },
      pages: {
        ...current.pages,
        [selectedPageKey]: createDefaultPageBackground()
      }
    }));
    setError(null);
    setNotice("已恢复当前页面的默认背景草稿，保存后生效。");
  }

  function handleResetGenericBackground() {
    updateGenericBackground(createDefaultSiteAppearance().background);
    setNotice("已恢复默认背景草稿，保存后生效。");
  }

  function handleResetPanelSkin() {
    updatePanelSkin(createDefaultSiteAppearance().panelSkin);
    setNotice("已恢复默认 Panel 草稿，保存后生效。");
  }

  async function handleSubmit() {
    setIsSaving(true);
    setError(null);
    setNotice(null);

    try {
      const updated = normalizeSiteAppearance(await updateSiteAppearance(draftConfig));
      setInitialConfig(updated);
      setDraftConfig(normalizeSiteAppearance(updated));
      await reloadSiteAppearance();
      setNotice("站点外观配置已保存。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "站点背景配置保存失败。");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="admin-site-settings-page site-settings-workspace-page">
      <div className="page-header site-settings-page-header">
        <div>
          <h1>站点设置</h1>
          <p>管理站点视觉、背景和页面级展示效果。</p>
        </div>
      </div>

      {(notice || error) && <div className={error ? "alert error" : "quiet-note success"}>{error ?? notice}</div>}

      <div className="site-settings-workspace">
        <div className="site-settings-workspace-grid">
          <section className="site-settings-config-column" aria-label="页面背景配置">
            <div className="site-settings-column-header">
              <div>
                <h2>页面背景配置</h2>
                <p>调整当前页面在 Root 配置风格下使用的背景。</p>
              </div>
            </div>

            <details className="site-settings-theme-section" open>
              <summary>全站 Background</summary>
              <div className="site-settings-theme-section-body">
                <SettingsSwitch
                  checked={draftConfig.background.enabled}
                  label="启用全站自定义背景"
                  description="由 Root 管理；未上传图片时不能启用。"
                  disabled={isSaving || !draftConfig.background.asset}
                  onChange={(enabled) => updateGenericBackground({
                    enabled,
                    positionX: draftConfig.background.positionX ?? 50,
                    positionY: draftConfig.background.positionY ?? 50,
                    sizeMode: draftConfig.background.sizeMode ?? "cover",
                    repeat: draftConfig.background.repeat ?? "no-repeat",
                    attachment: draftConfig.background.attachment ?? "scroll",
                    overlayColor: draftConfig.background.overlayColor ?? "#000000",
                    overlayOpacity: draftConfig.background.overlayOpacity ?? 0.45,
                    blur: draftConfig.background.blur ?? 0,
                    brightness: draftConfig.background.brightness ?? 100
                  })}
                />
                <ThemeAssetField label="背景图片" asset={draftConfig.background.asset} slot="background" busySlot={uploadingThemeSlot} disabled={isSaving} onUpload={handleThemeAssetUpload} onRemove={() => updateGenericBackground({ enabled: false, asset: null })} />
                <div className="site-settings-theme-grid">
                  <label>尺寸<select value={draftConfig.background.sizeMode ?? "cover"} disabled={isSaving} onChange={(event) => updateGenericBackground({ sizeMode: event.target.value as SiteThemeBackground["sizeMode"] })}><option value="cover">Cover</option><option value="contain">Contain</option><option value="auto">Auto</option></select></label>
                  <label>重复<select value={draftConfig.background.repeat ?? "no-repeat"} disabled={isSaving} onChange={(event) => updateGenericBackground({ repeat: event.target.value as SiteThemeBackground["repeat"] })}><option value="no-repeat">不重复</option><option value="repeat">重复</option><option value="repeat-x">横向重复</option><option value="repeat-y">纵向重复</option></select></label>
                  <label>附着<select value={draftConfig.background.attachment ?? "scroll"} disabled={isSaving} onChange={(event) => updateGenericBackground({ attachment: event.target.value as SiteThemeBackground["attachment"] })}><option value="scroll">Scroll</option><option value="fixed">Fixed</option></select></label>
                  <ColorSetting label="遮罩颜色" value={draftConfig.background.overlayColor ?? "#000000"} disabled={isSaving} onChange={(overlayColor) => updateGenericBackground({ overlayColor })} />
                </div>
                <SettingSlider label="X 位置" value={draftConfig.background.positionX ?? 50} valueLabel={`${draftConfig.background.positionX ?? 50}%`} min={0} max={100} step={1} disabled={isSaving} onChange={(positionX) => updateGenericBackground({ positionX })} />
                <SettingSlider label="Y 位置" value={draftConfig.background.positionY ?? 50} valueLabel={`${draftConfig.background.positionY ?? 50}%`} min={0} max={100} step={1} disabled={isSaving} onChange={(positionY) => updateGenericBackground({ positionY })} />
                <SettingSlider label="遮罩" value={draftConfig.background.overlayOpacity ?? 0.45} valueLabel={`${Math.round((draftConfig.background.overlayOpacity ?? 0.45) * 100)}%`} min={0} max={1} step={0.05} disabled={isSaving} onChange={(overlayOpacity) => updateGenericBackground({ overlayOpacity })} />
                <SettingSlider label="背景模糊" value={draftConfig.background.blur ?? 0} valueLabel={`${draftConfig.background.blur ?? 0}px`} min={0} max={20} step={1} disabled={isSaving} onChange={(blur) => updateGenericBackground({ blur })} />
                <SettingSlider label="背景亮度" value={draftConfig.background.brightness ?? 100} valueLabel={`${draftConfig.background.brightness ?? 100}%`} min={50} max={150} step={5} disabled={isSaving} onChange={(brightness) => updateGenericBackground({ brightness })} />
                <p className="appearance-contrast-note">建议使用足够强度的遮罩保证正文可读性。背景无动画，不会影响 reduced-motion。</p>
                <button className="button site-settings-reset-button" type="button" disabled={isSaving} onClick={handleResetGenericBackground}>恢复默认背景</button>
              </div>
            </details>

            <details className="site-settings-theme-section">
              <summary>全站 Panel Skin</summary>
              <div className="site-settings-theme-section-body">
                <SettingsSwitch checked={draftConfig.panelSkin.enabled} label="启用通用 Panel Skin" description="仅作用于现有 Primary Panel，不为页面创建额外 Header。" disabled={isSaving} onChange={(enabled) => updatePanelSkin({ enabled })} />
                <ThemeAssetField label="Panel 背景纹理" asset={draftConfig.panelSkin.backgroundTexture} slot="panelBackground" busySlot={uploadingThemeSlot} disabled={isSaving} onUpload={handleThemeAssetUpload} onRemove={() => updatePanelSkin({ backgroundTexture: null })} />
                <ThemeAssetField label="Panel Header 纹理" asset={draftConfig.panelSkin.headerTexture} slot="panelHeader" busySlot={uploadingThemeSlot} disabled={isSaving} onUpload={handleThemeAssetUpload} onRemove={() => updatePanelSkin({ headerTexture: null })} />
                <ThemeAssetField label="Panel Border 纹理" asset={draftConfig.panelSkin.borderTexture} slot="panelBorder" busySlot={uploadingThemeSlot} disabled={isSaving} onUpload={handleThemeAssetUpload} onRemove={() => updatePanelSkin({ borderTexture: null })} />
                <SettingSlider label="Panel 背景透明度" value={draftConfig.panelSkin.backgroundOpacity ?? 1} valueLabel={(draftConfig.panelSkin.backgroundOpacity ?? 1).toFixed(2)} min={0} max={1} step={0.05} disabled={isSaving} onChange={(backgroundOpacity) => updatePanelSkin({ backgroundOpacity })} />
                <SettingSlider label="纹理强度" value={draftConfig.panelSkin.textureOpacity ?? 0.15} valueLabel={(draftConfig.panelSkin.textureOpacity ?? 0.15).toFixed(2)} min={0} max={1} step={0.05} disabled={isSaving} onChange={(textureOpacity) => updatePanelSkin({ textureOpacity })} />
                <SettingSlider label="圆角" value={draftConfig.panelSkin.radius ?? 8} valueLabel={`${draftConfig.panelSkin.radius ?? 8}px`} min={0} max={32} step={1} disabled={isSaving} onChange={(radius) => updatePanelSkin({ radius })} />
                <SettingSlider label="阴影强度" value={draftConfig.panelSkin.shadowStrength ?? 0.18} valueLabel={(draftConfig.panelSkin.shadowStrength ?? 0.18).toFixed(2)} min={0} max={1} step={0.05} disabled={isSaving} onChange={(shadowStrength) => updatePanelSkin({ shadowStrength })} />
                <p className="appearance-contrast-note">纹理可能降低文字可读性；建议保持较低纹理强度。</p>
                <button className="button site-settings-reset-button" type="button" disabled={isSaving} onClick={handleResetPanelSkin}>恢复默认 Panel</button>
              </div>
            </details>

            <SettingsSection title="当前页面" description="切换页面不会丢失其他页面尚未保存的草稿。">
              <label className="site-settings-page-select">
                <span>配置页面</span>
                <select value={selectedPageKey} disabled={isSaving} onChange={(event) => setSelectedPageKey(event.target.value as SitePageKey)}>
                  {sitePageOptions.map((option) => <option key={option.key} value={option.key}>{option.label}</option>)}
                </select>
              </label>
              <p className="site-settings-section-note">{selectedOption.description}</p>
            </SettingsSection>

            <SettingsSection title="页面背景" description="关闭后保留当前草稿值，再次开启即可继续调整。">
              <SettingsSwitch
                checked={selectedEditorValue.enabled}
                label="启用当前页面背景"
                description={selectedEditorValue.enabled ? "当前页面背景已启用" : "当前页面背景已关闭"}
                disabled={isSaving}
                onChange={(enabled) => updateSelectedPage({ enabled })}
              />
            </SettingsSection>

            <div className={pageControlsDisabled ? "site-settings-disabled-group" : undefined} aria-disabled={pageControlsDisabled}>
              <SettingsSection title="背景图片" description="支持 PNG、JPEG 和 WebP，继续使用现有图片上传服务。">
                <div className="site-settings-image-info">
                  {selectedEditorValue.imageUrl ? (
                    <><strong>{getFileName(selectedEditorValue.imageUrl)}</strong><span>{selectedEditorValue.imageUrl}</span></>
                  ) : <span>尚未设置背景图片</span>}
                </div>
                <div className="site-settings-image-actions">
                  <button className="button" type="button" disabled={pageControlsDisabled || isUploading} onClick={() => fileInputRef.current?.click()}>
                    {isUploading ? "上传中..." : "上传新图片"}
                  </button>
                  <button className="button site-settings-danger-button" type="button" disabled={pageControlsDisabled || !selectedEditorValue.imageUrl} onClick={handleClearBackground}>
                    移除背景
                  </button>
                  <input ref={fileInputRef} className="visually-hidden-file" type="file" accept="image/png,image/jpeg,image/webp" disabled={pageControlsDisabled} onChange={handleUpload} />
                </div>
              </SettingsSection>

              <SettingsSection title="背景位置" description="定位图片在预览区域中的视觉中心。">
                <div className="site-settings-slider-stack">
                  <SettingSlider label="X 位置" value={selectedEditorValue.positionX} valueLabel={`${selectedEditorValue.positionX.toFixed(0)}%`} min={0} max={100} step={1} disabled={pageControlsDisabled} onChange={(positionX) => updateSelectedPage({ positionX })} />
                  <SettingSlider label="Y 位置" value={selectedEditorValue.positionY} valueLabel={`${selectedEditorValue.positionY.toFixed(0)}%`} min={0} max={100} step={1} disabled={pageControlsDisabled} onChange={(positionY) => updateSelectedPage({ positionY })} />
                </div>
              </SettingsSection>

              <SettingsSection title="显示效果" description="缩放背景，并可按页面覆盖全局遮罩。">
                <div className="site-settings-slider-stack">
                  <SettingSlider label="缩放" value={selectedEditorValue.scale} valueLabel={`${selectedEditorValue.scale.toFixed(2)}×`} min={0.5} max={2.5} step={0.05} disabled={pageControlsDisabled} onChange={(scale) => updateSelectedPage({ scale })} />
                  <SettingsSwitch
                    checked={selectedPage.overlayOpacity != null}
                    label="页面独立遮罩"
                    description={selectedPage.overlayOpacity != null ? "当前页面使用独立遮罩" : "当前页面继承全局遮罩"}
                    disabled={pageControlsDisabled}
                    onChange={(enabled) => updateSelectedPage({ overlayOpacity: enabled ? draftConfig.theme.backgroundOverlayOpacity : null })}
                  />
                  <SettingSlider
                    label={selectedPage.overlayOpacity != null ? "页面遮罩" : "全局遮罩（继承）"}
                    value={selectedEditorValue.overlayOpacity}
                    valueLabel={`${Math.round(selectedEditorValue.overlayOpacity * 100)}%`}
                    min={0}
                    max={1}
                    step={0.05}
                    disabled={pageControlsDisabled || selectedPage.overlayOpacity == null}
                    onChange={(overlayOpacity) => updateSelectedPage({ overlayOpacity })}
                  />
                </div>
              </SettingsSection>
            </div>

            <SettingsSection title="Root 风格总开关" description="全局 UI 适配参数移动到右侧“视觉适配”面板。">
              <SettingsSwitch checked={draftConfig.theme.backgroundEnabled} label="启用 Root 配置风格背景" description="只影响主动选择 Root 配置风格的用户" disabled={isSaving} onChange={(backgroundEnabled) => updateTheme({ backgroundEnabled })} />
              <SettingSlider label="全局默认遮罩" value={draftConfig.theme.backgroundOverlayOpacity} valueLabel={`${Math.round(draftConfig.theme.backgroundOverlayOpacity * 100)}%`} min={0} max={1} step={0.05} disabled={isSaving} onChange={(backgroundOverlayOpacity) => updateTheme({ backgroundOverlayOpacity })} />
            </SettingsSection>
          </section>

          <aside className="site-settings-preview-column" aria-label="实时预览">
            <div className="site-settings-column-header">
              <div><h2>实时预览</h2><p>完整展示当前视觉草稿，保存前不会影响线上配置。</p></div>
              <span className="site-settings-theme-chip">{currentTheme === "mystic-background" ? "Root 风格" : "默认风格"}</span>
            </div>

            <BackgroundPreview value={selectedEditorValue} theme={draftConfig.theme} background={draftConfig.background} panelSkin={draftConfig.panelSkin} pageKey={selectedPageKey} />

            <div className="site-settings-draft-summary">
              <h3>当前状态</h3>
              <dl>
                <div><dt>背景</dt><dd>{selectedEditorValue.enabled ? "已启用" : "已关闭"}</dd></div>
                <div><dt>X</dt><dd>{selectedEditorValue.positionX.toFixed(0)}%</dd></div>
                <div><dt>Y</dt><dd>{selectedEditorValue.positionY.toFixed(0)}%</dd></div>
                <div><dt>缩放</dt><dd>{selectedEditorValue.scale.toFixed(2)}×</dd></div>
                <div><dt>遮罩</dt><dd>{Math.round(selectedEditorValue.overlayOpacity * 100)}%</dd></div>
              </dl>
              <button className="button site-settings-reset-button" type="button" disabled={isSaving} onClick={handleRestoreDefault}>恢复当前背景</button>
            </div>

            <VisualAdaptationPanel
              tab={visualTab}
              theme={draftConfig.theme}
              disabled={isSaving}
              onTabChange={setVisualTab}
              onChange={updateTheme}
            />
          </aside>
        </div>

        <div className="site-settings-action-bar">
          <div className={isDirty ? "site-settings-dirty-status dirty" : "site-settings-dirty-status"}>
            <span aria-hidden="true" />{isDirty ? "有未保存修改" : "配置已同步"}
          </div>
          <div className="site-settings-action-buttons">
            <button className="button" type="button" disabled={!isDirty || isSaving} onClick={handleDiscard}>放弃修改</button>
            <button className="button primary" type="button" disabled={!isDirty || isSaving || isUploading || uploadingThemeSlot !== null} onClick={handleSubmit}>{isSaving ? "保存中..." : "保存配置"}</button>
          </div>
        </div>
      </div>
    </section>
  );
}

function SettingsSection({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return <section className="site-settings-section"><div className="site-settings-section-heading"><h3>{title}</h3>{description && <p>{description}</p>}</div>{children}</section>;
}

function SettingsSwitch({ checked, label, description, disabled = false, onChange }: SettingsSwitchProps) {
  return (
    <div className="site-settings-switch-row">
      <div><strong>{label}</strong><span>{description}</span></div>
      <button className={checked ? "site-settings-switch active" : "site-settings-switch"} type="button" role="switch" aria-checked={checked} aria-label={label} disabled={disabled} onClick={() => onChange(!checked)}><span /></button>
    </div>
  );
}

function SettingSlider({ label, value, valueLabel, min, max, step, disabled = false, onChange }: SettingSliderProps) {
  return (
    <label className="site-settings-slider">
      <span><strong>{label}</strong><output>{valueLabel}</output></span>
      <input className="range-input" type="range" min={min} max={max} step={step} value={value} disabled={disabled} onChange={(event) => onChange(Number(event.target.value))} />
    </label>
  );
}

function VisualAdaptationPanel({
  tab,
  theme,
  disabled,
  onTabChange,
  onChange
}: {
  tab: VisualSettingsTab;
  theme: SiteAppearanceTheme;
  disabled: boolean;
  onTabChange: (tab: VisualSettingsTab) => void;
  onChange: (patch: Partial<SiteAppearanceTheme>) => void;
}) {
  return (
    <section className="site-settings-visual-panel">
      <div className="site-settings-visual-heading">
        <div><span>VISUAL ADAPTATION</span><h3>视觉适配</h3></div>
        <small>调整文字、面板与导航，使 UI 适配当前背景。</small>
      </div>

      <div className="site-settings-visual-tabs" role="tablist" aria-label="视觉适配分类">
        {([
          ["text", "文字"],
          ["panel", "面板"],
          ["navigation", "导航"],
          ["presets", "预设"]
        ] as Array<[VisualSettingsTab, string]>).map(([key, label]) => (
          <button key={key} type="button" className={tab === key ? "active" : ""} role="tab" aria-selected={tab === key} onClick={() => onTabChange(key)}>{label}</button>
        ))}
      </div>

      <div className="site-settings-visual-body">
        {tab === "text" && (
          <>
            <ColorSetting label="主文字" value={theme.textPrimaryColor} disabled={disabled} onChange={(textPrimaryColor) => onChange({ textPrimaryColor })} />
            <ColorSetting label="次级文字" value={theme.textSecondaryColor} disabled={disabled} onChange={(textSecondaryColor) => onChange({ textSecondaryColor })} />
            <ColorSetting label="弱化文字" value={theme.textMutedColor} disabled={disabled} onChange={(textMutedColor) => onChange({ textMutedColor })} />
            <ColorSetting label="强调色" value={theme.accentColor} disabled={disabled} onChange={(accentColor) => onChange({ accentColor })} />
            <label className="site-settings-select-setting">
              <span><strong>字体方案</strong><small>受控字体栈，不上传字体文件。</small></span>
              <select value={theme.fontPreset} disabled={disabled} onChange={(event) => onChange({ fontPreset: event.target.value as SiteAppearanceTheme["fontPreset"] })}>
                <option value="system">系统默认</option>
                <option value="readable">清晰阅读</option>
                <option value="mono">等宽代码</option>
              </select>
            </label>
          </>
        )}

        {tab === "panel" && (
          <>
            <ColorSetting label="面板底色" value={theme.panelColor} disabled={disabled} onChange={(panelColor) => onChange({ panelColor })} />
            <SettingSlider label="面板透明度" value={theme.panelOpacity} valueLabel={theme.panelOpacity.toFixed(2)} min={0.35} max={0.95} step={0.05} disabled={disabled} onChange={(panelOpacity) => onChange({ panelOpacity })} />
            <SettingSlider label="面板模糊" value={theme.panelBlur} valueLabel={`${theme.panelBlur.toFixed(0)}px`} min={0} max={30} step={1} disabled={disabled} onChange={(panelBlur) => onChange({ panelBlur })} />
            <SettingSlider label="边框强度" value={theme.panelBorderOpacity} valueLabel={theme.panelBorderOpacity.toFixed(2)} min={0} max={0.5} step={0.02} disabled={disabled} onChange={(panelBorderOpacity) => onChange({ panelBorderOpacity })} />
          </>
        )}

        {tab === "navigation" && (
          <>
            <ColorSetting label="导航文字" value={theme.navTextColor} disabled={disabled} onChange={(navTextColor) => onChange({ navTextColor })} />
            <ColorSetting label="激活文字" value={theme.navActiveColor} disabled={disabled} onChange={(navActiveColor) => onChange({ navActiveColor })} />
            <SettingSlider label="导航透明度" value={theme.navOpacity} valueLabel={theme.navOpacity.toFixed(2)} min={0.35} max={1} step={0.05} disabled={disabled} onChange={(navOpacity) => onChange({ navOpacity })} />
            <SettingSlider label="导航模糊" value={theme.navBlur} valueLabel={`${theme.navBlur.toFixed(0)}px`} min={0} max={30} step={1} disabled={disabled} onChange={(navBlur) => onChange({ navBlur })} />
          </>
        )}

        {tab === "presets" && (
          <div className="site-settings-preset-grid">
            <PresetButton title="深色高对比" description="复杂亮色背景优先保证可读性。" disabled={disabled} onClick={() => onChange({
              panelColor: "#0F141D", panelOpacity: 0.9, panelBlur: 8, panelBorderOpacity: 0.18,
              textPrimaryColor: "#F7F9FC", textSecondaryColor: "#C5CCDA", textMutedColor: "#929CAE",
              accentColor: "#7B86FF", navOpacity: 0.82, navBlur: 10, navTextColor: "#E1E6EF", navActiveColor: "#FFFFFF", fontPreset: "readable"
            })} />
            <PresetButton title="轻透玻璃" description="保留更多背景细节与空间感。" disabled={disabled} onClick={() => onChange({
              panelColor: "#111827", panelOpacity: 0.62, panelBlur: 16, panelBorderOpacity: 0.15,
              textPrimaryColor: "#F4F7FB", textSecondaryColor: "#BCC5D4", textMutedColor: "#8792A6",
              accentColor: "#7C87FF", navOpacity: 0.58, navBlur: 18, navTextColor: "#DDE3EE", navActiveColor: "#FFFFFF", fontPreset: "system"
            })} />
            <PresetButton title="深色背景" description="减少面板遮挡，让暗色背景更突出。" disabled={disabled} onClick={() => onChange({
              panelColor: "#11141A", panelOpacity: 0.7, panelBlur: 10, panelBorderOpacity: 0.12,
              textPrimaryColor: "#F2F4F8", textSecondaryColor: "#AEB6CA", textMutedColor: "#7F8798",
              accentColor: "#6E7BFF", navOpacity: 0.62, navBlur: 14, navTextColor: "#D9DEE9", navActiveColor: "#F2F4F8", fontPreset: "system"
            })} />
            <PresetButton title="恢复主题默认" description="恢复视觉参数，不清除页面背景。" disabled={disabled} onClick={() => {
              const defaults = createDefaultSiteAppearance().theme;
              onChange(defaults);
            }} />
          </div>
        )}
      </div>
    </section>
  );
}

function ColorSetting({ label, value, disabled, onChange }: { label: string; value: string; disabled: boolean; onChange: (value: string) => void }) {
  return (
    <label className="site-settings-color-setting">
      <span><strong>{label}</strong><code>{value}</code></span>
      <input type="color" value={value} disabled={disabled} onChange={(event) => onChange(event.target.value.toUpperCase())} />
    </label>
  );
}

function PresetButton({ title, description, disabled, onClick }: { title: string; description: string; disabled: boolean; onClick: () => void }) {
  return <button className="site-settings-preset-card" type="button" disabled={disabled} onClick={onClick}><strong>{title}</strong><span>{description}</span></button>;
}

function BackgroundPreview({
  value,
  theme,
  background,
  panelSkin,
  pageKey
}: {
  value: SitePageBackground & { overlayOpacity: number };
  theme: SiteAppearanceTheme;
  background: SiteThemeBackground;
  panelSkin: SitePanelSkin;
  pageKey: SitePageKey;
}) {
  const previewUrl = resolveSiteAssetUrl(value.imageUrl);
  const genericPreviewUrl = resolveSiteAssetUrl(background.asset?.url);
  const useGenericBackground = Boolean(background.enabled && genericPreviewUrl);
  const previewStyle: CSSProperties = useGenericBackground ? {
    backgroundImage: `url("${genericPreviewUrl}")`,
    backgroundPosition: `${background.positionX ?? 50}% ${background.positionY ?? 50}%`,
    backgroundSize: background.sizeMode ?? "cover",
    backgroundRepeat: background.repeat ?? "no-repeat",
    filter: `blur(${background.blur ?? 0}px) brightness(${background.brightness ?? 100}%)`
  } : value.enabled && previewUrl ? {
    backgroundImage: `url("${previewUrl}")`,
    backgroundPosition: `${value.positionX}% ${value.positionY}%`,
    backgroundSize: `${value.scale * 100}% auto`
  } : {};
  const panelTextureUrl = resolveSiteAssetUrl(panelSkin.backgroundTexture?.url);
  const panelHeaderUrl = resolveSiteAssetUrl(panelSkin.headerTexture?.url);
  const panelBorderUrl = resolveSiteAssetUrl(panelSkin.borderTexture?.url);
  const textureOpacity = panelSkin.textureOpacity ?? 0.15;
  const panelStyle: CSSProperties = {
    backgroundColor: hexToRgba(theme.panelColor, panelSkin.enabled ? panelSkin.backgroundOpacity ?? theme.panelOpacity : theme.panelOpacity),
    backgroundImage: panelSkin.enabled && panelTextureUrl
      ? `linear-gradient(rgba(5, 6, 8, ${1 - textureOpacity}), rgba(5, 6, 8, ${1 - textureOpacity})), url("${panelTextureUrl}")`
      : undefined,
    backgroundSize: panelSkin.enabled && panelTextureUrl ? "cover" : undefined,
    borderColor: `rgba(255, 255, 255, ${theme.panelBorderOpacity})`,
    borderImageSource: panelSkin.enabled && panelBorderUrl ? `url("${panelBorderUrl}")` : undefined,
    borderImageSlice: panelSkin.enabled && panelBorderUrl ? 30 : undefined,
    borderImageWidth: panelSkin.enabled && panelBorderUrl ? 1 : undefined,
    borderImageRepeat: panelSkin.enabled && panelBorderUrl ? "round" : undefined,
    borderRadius: panelSkin.enabled && panelSkin.radius != null ? `${panelSkin.radius}px` : undefined,
    boxShadow: panelSkin.enabled && panelSkin.shadowStrength != null ? `0 18px 48px rgba(0, 0, 0, ${panelSkin.shadowStrength})` : undefined,
    backdropFilter: `blur(${theme.panelBlur}px)`
  };
  const previewShellStyle = {
    color: theme.textPrimaryColor,
    fontFamily: resolvePreviewFont(theme.fontPreset),
    "--theme-preview-header-layer": panelSkin.enabled && panelHeaderUrl
      ? `linear-gradient(rgba(5, 6, 8, ${1 - textureOpacity}), rgba(5, 6, 8, ${1 - textureOpacity})), url("${panelHeaderUrl}")`
      : "none"
  } as CSSProperties;
  const tokenPreviewStyle = {
    "--oj-panel-bg": hexToRgba(theme.panelColor, theme.panelOpacity),
    "--oj-input-bg": hexToRgba(theme.panelColor, Math.min(theme.panelOpacity + 0.08, 0.98)),
    "--oj-panel-border": `rgba(255, 255, 255, ${theme.panelBorderOpacity})`,
    "--oj-text-primary": theme.textPrimaryColor,
    "--oj-text-secondary": theme.textSecondaryColor,
    "--oj-text-muted": theme.textMutedColor,
    "--oj-accent": theme.accentColor,
    "--oj-font-family": resolvePreviewFont(theme.fontPreset)
  } as CSSProperties;
  const pageLabel = sitePageOptions.find((option) => option.key === pageKey)?.label ?? "页面";

  return (
    <section className="site-settings-preview-showcase site-settings-preview-showcase-v3">
      <div className="site-settings-preview-caption">
        <div>
          <span>LIVE PAGE PREVIEW</span>
          <strong>{pageLabel} · 实时 UI</strong>
        </div>
        <p>只预览当前页面内容区域，不重复模拟站点顶部导航；结构与文案尽量对齐真实页面。</p>
      </div>

      <div className={useGenericBackground || value.enabled ? "site-settings-preview-card active site-settings-preview-card-v3" : "site-settings-preview-card site-settings-preview-card-v3"}>
        <div className="site-settings-preview-theme-background" style={previewStyle} />
        <div className="site-settings-preview-overlay" style={{ background: useGenericBackground
          ? hexToRgba(background.overlayColor ?? "#000000", background.overlayOpacity ?? 0)
          : `rgba(0, 0, 0, ${value.enabled ? value.overlayOpacity : 0})` }} />
        <div className={`site-settings-preview-shell site-settings-preview-shell-v3${panelSkin.enabled ? " theme-preview-panel-enabled" : ""}`} style={previewShellStyle}>

          <main className="site-settings-preview-page site-settings-preview-page-v3">
            <PreviewPageContent pageKey={pageKey} theme={theme} panelStyle={panelStyle} />
          </main>
        </div>
      </div>

      <AppearanceTokenPreview theme={theme} style={tokenPreviewStyle} />
    </section>
  );
}

function ThemeAssetField({
  label,
  asset,
  slot,
  busySlot,
  disabled,
  onUpload,
  onRemove
}: {
  label: string;
  asset: ThemeAssetReference | null;
  slot: ThemeAssetSlot;
  busySlot: ThemeAssetSlot | null;
  disabled: boolean;
  onUpload: (slot: ThemeAssetSlot, file: File) => Promise<void>;
  onRemove: () => void;
}) {
  return (
    <div className="site-settings-theme-asset-field">
      <div><strong>{label}</strong><span>{asset ? getFileName(asset.url) : "未设置"}</span></div>
      {asset && <img src={resolveSiteAssetUrl(asset.url)} alt={`${label}预览`} />}
      <div className="site-settings-image-actions">
        <label className={`button${disabled || busySlot !== null ? " disabled" : ""}`}>
          {busySlot === slot ? "上传中..." : "上传图片"}
          <input className="visually-hidden-file" type="file" accept="image/png,image/jpeg,image/webp" disabled={disabled || busySlot !== null} onChange={(event) => {
            const file = event.target.files?.[0];
            if (file) void onUpload(slot, file);
            event.target.value = "";
          }} />
        </label>
        <button className="button site-settings-danger-button" type="button" disabled={disabled || !asset} onClick={onRemove}>移除引用</button>
      </div>
    </div>
  );
}

function AppearanceTokenPreview({ theme, style }: { theme: SiteAppearanceTheme; style: CSSProperties }) {
  const warnings = getContrastWarnings(theme);

  return (
    <section className="appearance-token-preview" style={style} aria-label="通用组件主题预览">
      <header>
        <strong>通用组件</strong>
        <span>颜色草稿即时传播</span>
      </header>
      <div className="appearance-token-preview-grid">
        <div className="appearance-token-preview-card">
          <div className="appearance-token-preview-controls">
            <button className="button primary" type="button">Button</button>
            <span className="appearance-token-preview-badge">Badge</span>
            <button className="appearance-token-preview-toggle" type="button" role="switch" aria-checked="true" aria-label="Toggle preview"><span /></button>
            <a href="#appearance-preview" onClick={(event) => event.preventDefault()}>Link</a>
          </div>
          <label>Input<input value="Input" readOnly aria-label="Input preview" /></label>
        </div>
        <div className="appearance-token-preview-card">
          <strong>Card</strong>
          <p style={{ color: "var(--oj-text-secondary)" }}>Surface / Border / Text</p>
        </div>
        <div className="appearance-token-preview-table" role="table" aria-label="Table preview">
          <span role="columnheader">Table</span>
          <span role="cell">Row content</span>
        </div>
        <pre className="appearance-token-preview-code"><code>const themed = true;</code></pre>
      </div>
      {warnings.length > 0 && <p className="appearance-contrast-note" role="status">对比度提示：{warnings.join("；")}</p>}
    </section>
  );
}

function PreviewPageContent({
  pageKey,
  theme,
  panelStyle
}: {
  pageKey: SitePageKey;
  theme: SiteAppearanceTheme;
  panelStyle: CSSProperties;
}) {
  if (pageKey === "problems" || pageKey === "admin-problems") {
    const isAdminProblems = pageKey === "admin-problems";

    return (
      <>
        <PreviewPageHeader
          eyebrow="PROBLEMS"
          title={isAdminProblems ? "题目管理" : "题目列表"}
          description={isAdminProblems ? "管理题目内容、测试数据与发布状态。" : "查看当前可用题目，进入详情后提交代码。"}
          action="创建题目"
          theme={theme}
        />

        <div className="site-settings-preview-toolbar-v4" style={panelStyle}>
          <div className="site-settings-preview-search-v4" style={{ color: theme.textMutedColor, borderColor: `rgba(255,255,255,${theme.panelBorderOpacity})` }}>
            <span>⌕</span>
            <span>搜索题目标题...</span>
          </div>
        </div>

        <section className="site-settings-preview-problem-table-v4" style={panelStyle}>
          <div className="header" style={{ color: theme.textMutedColor }}>
            <span>#</span>
            <span>题目标题</span>
            <span>时间限制</span>
            <span>内存限制</span>
            <span>公开状态</span>
            <span>创建时间</span>
            <span>操作</span>
          </div>
          {[
            ["1", "[E2E] Standard A+B", "1000 ms", "128 MB", "公开", "2026/5/31", "查看  编辑"],
            ["2", "[E2E] Function Invert Tree", "1000 ms", "128 MB", "公开", "2026/5/31", "查看  编辑"],
            ["3", "指令之意", "1500 ms", "256 MB", "公开", "2026/5/27", "查看  编辑"]
          ].map((row) => (
            <div className="row" key={row[0]} style={{ borderColor: `rgba(255,255,255,${theme.panelBorderOpacity})` }}>
              <span style={{ color: theme.textMutedColor }}>{row[0]}</span>
              <strong style={{ color: theme.textPrimaryColor }}>{row[1]}</strong>
              <span className="badge time">{row[2]}</span>
              <span className="badge memory">{row[3]}</span>
              <span className="badge published">{row[4]}</span>
              <span style={{ color: theme.textMutedColor }}>{row[5]}</span>
              <span className="actions" style={{ color: theme.textSecondaryColor }}>{row[6]}</span>
            </div>
          ))}
        </section>
      </>
    );
  }

  if (pageKey === "challenges" || pageKey === "admin-challenges" || pageKey === "file-task") {
    return (
      <>
        <PreviewHero eyebrow="CHALLENGE" title={pageKey === "admin-challenges" ? "挑战管理" : "挑战棋盘"} description="选择任务进入挑战，并查看当前挑战进度。" theme={theme} panelStyle={panelStyle} action={pageKey === "admin-challenges" ? "新建挑战" : undefined} />
        <div className="site-settings-preview-challenge-grid-v3">
          <section className="site-settings-preview-board-v3" style={panelStyle}>
            {Array.from({ length: 16 }).map((_, index) => <span key={index} className={index === 6 ? "selected" : ""} style={index === 6 ? { borderColor: theme.accentColor, boxShadow: `inset 0 0 0 1px ${theme.accentColor}` } : undefined}>{index === 6 ? "♜" : ""}</span>)}
          </section>
          <aside className="site-settings-preview-side-v3" style={panelStyle}>
            <span className="site-settings-preview-kicker" style={{ color: theme.accentColor }}>SELECTED TASK</span>
            <strong style={{ color: theme.textPrimaryColor }}>指针与结构体</strong>
            <p style={{ color: theme.textSecondaryColor }}>小题描述、分数和状态区域。</p>
            <div style={{ color: theme.textMutedColor }}><span>得分</span><b style={{ color: theme.textPrimaryColor }}>0 / 80</b></div>
            <button style={{ background: theme.accentColor, borderColor: theme.accentColor }}>进入任务</button>
          </aside>
        </div>
      </>
    );
  }

  if (pageKey === "leaderboards") {
    return (
      <>
        <PreviewHero eyebrow="LEADERBOARD" title="排行榜" description="展示用户积分、完成进度与挑战排名。" theme={theme} panelStyle={panelStyle} />
        <section className="site-settings-preview-ranking-v3" style={panelStyle}>
          {[
            ["01", "UnrealStudio", "980"],
            ["02", "PlayerAlpha", "820"],
            ["03", "PlayerBeta", "740"]
          ].map(([rank, name, score]) => (
            <div key={rank} style={{ borderColor: `rgba(255,255,255,${theme.panelBorderOpacity})` }}>
              <b style={{ color: theme.accentColor }}>{rank}</b>
              <strong style={{ color: theme.textPrimaryColor }}>{name}</strong>
              <span style={{ color: theme.textMutedColor }}>完成进度</span>
              <em style={{ color: theme.textSecondaryColor }}>{score}</em>
            </div>
          ))}
        </section>
      </>
    );
  }

  if (pageKey === "profile" || pageKey === "account-settings") {
    return (
      <>
        <PreviewHero eyebrow="ACCOUNT" title={pageKey === "profile" ? "个人中心" : "账号设置"} description="个人资料、外观配置和账号安全。" theme={theme} panelStyle={panelStyle} />
        <div className="site-settings-preview-profile-grid-v3">
          <section className="site-settings-preview-profile-card-v3" style={panelStyle}>
            <div className="avatar" style={{ background: hexToRgba(theme.accentColor, 0.22), borderColor: hexToRgba(theme.accentColor, 0.55) }}>U</div>
            <div><strong style={{ color: theme.textPrimaryColor }}>UnrealStudio</strong><span style={{ color: theme.textMutedColor }}>Root · OnlineJudge</span></div>
          </section>
          <section className="site-settings-preview-form-v3" style={panelStyle}>
            <label style={{ color: theme.textMutedColor }}>显示名称</label>
            <div style={{ background: hexToRgba(theme.panelColor, Math.min(theme.panelOpacity + 0.08, 1)), borderColor: `rgba(255,255,255,${theme.panelBorderOpacity})`, color: theme.textPrimaryColor }}>UnrealStudio</div>
            <button style={{ background: theme.accentColor, borderColor: theme.accentColor }}>保存修改</button>
          </section>
        </div>
      </>
    );
  }

  if (pageKey === "submissions") {
    return (
      <>
        <PreviewHero eyebrow="SUBMISSIONS" title="提交记录" description="查看判题状态、语言、耗时和得分。" theme={theme} panelStyle={panelStyle} />
        <section className="site-settings-preview-table-v3" style={panelStyle}>
          <div className="header" style={{ color: theme.textMutedColor }}><span>#</span><span>题目</span><span>语言</span><span>结果</span><span>耗时</span></div>
          {[
            ["128", "A+B", "C++17", "Accepted", "18 ms"],
            ["127", "指令之意", "C#11", "Accepted", "31 ms"],
            ["126", "Invert Tree", "C11", "Wrong", "14 ms"]
          ].map((row) => (
            <div className="row" key={row[0]} style={{ color: theme.textSecondaryColor, borderColor: `rgba(255,255,255,${theme.panelBorderOpacity})` }}>
              <span style={{ color: theme.textMutedColor }}>{row[0]}</span><strong style={{ color: theme.textPrimaryColor }}>{row[1]}</strong><span>{row[2]}</span><b style={{ color: row[3] === "Accepted" ? "#86EFAC" : "#FCA5A5" }}>{row[3]}</b><span>{row[4]}</span>
            </div>
          ))}
        </section>
      </>
    );
  }

  return (
    <>
      <PreviewHero eyebrow="ONLINEJUDGE" title="首页概览" description="快速访问题目、挑战、榜单和个人数据。" theme={theme} panelStyle={panelStyle} action="开始答题" />
      <div className="site-settings-preview-dashboard-v3">
        {[
          ["今日题目", "12"],
          ["进行中挑战", "2"],
          ["最近提交", "Accepted"]
        ].map(([label, value]) => (
          <section key={label} style={panelStyle}><span style={{ color: theme.textMutedColor }}>{label}</span><strong style={{ color: theme.textPrimaryColor }}>{value}</strong></section>
        ))}
      </div>
    </>
  );
}

function PreviewHero({
  eyebrow,
  title,
  description,
  theme,
  panelStyle: _panelStyle,
  action
}: {
  eyebrow: string;
  title: string;
  description: string;
  theme: SiteAppearanceTheme;
  panelStyle: CSSProperties;
  action?: string;
}) {
  return <PreviewPageHeader eyebrow={eyebrow} title={title} description={description} action={action} theme={theme} />;
}

function PreviewPageHeader({
  eyebrow,
  title,
  description,
  action,
  theme
}: {
  eyebrow: string;
  title: string;
  description: string;
  action?: string;
  theme: SiteAppearanceTheme;
}) {
  return (
    <section className="site-settings-preview-page-header-v4">
      <div>
        <span className="site-settings-preview-kicker" style={{ color: theme.accentColor }}>{eyebrow}</span>
        <h3 style={{ color: theme.textPrimaryColor }}>{title}</h3>
        <p style={{ color: theme.textSecondaryColor }}>{description}</p>
      </div>
      {action && <button type="button" style={{ background: theme.accentColor, borderColor: theme.accentColor }}>{action}</button>}
    </section>
  );
}

function hexToRgba(hex: string, opacity: number) {
  const normalized = /^#[0-9a-fA-F]{6}$/.test(hex) ? hex.slice(1) : "11141A";
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}

function getContrastWarnings(theme: SiteAppearanceTheme) {
  const warnings: string[] = [];
  if (contrastRatio(theme.textPrimaryColor, theme.panelColor) < 4.5) warnings.push("主文字与面板建议至少 4.5:1");
  if (contrastRatio(theme.textSecondaryColor, theme.panelColor) < 3) warnings.push("次级文字与面板建议至少 3:1");
  if (contrastRatio("#FFFFFF", theme.accentColor) < 3) warnings.push("强调色按钮与白字建议至少 3:1");
  return warnings;
}

function contrastRatio(first: string, second: string) {
  const firstLuminance = relativeLuminance(first);
  const secondLuminance = relativeLuminance(second);
  return (Math.max(firstLuminance, secondLuminance) + 0.05) / (Math.min(firstLuminance, secondLuminance) + 0.05);
}

function relativeLuminance(hex: string) {
  const normalized = /^#[0-9a-fA-F]{6}$/.test(hex) ? hex.slice(1) : "000000";
  const channels = [0, 2, 4].map((offset) => Number.parseInt(normalized.slice(offset, offset + 2), 16) / 255);
  const [red, green, blue] = channels.map((channel) => channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
  return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
}

function resolvePreviewFont(preset: SiteAppearanceTheme["fontPreset"]) {
  if (preset === "readable") return '"Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", system-ui, sans-serif';
  if (preset === "mono") return '"Cascadia Code", "JetBrains Mono", Consolas, monospace';
  return 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
}

function getFileName(imageUrl: string) {
  const cleanPath = imageUrl.split(/[?#]/)[0];
  return cleanPath.split("/").filter(Boolean).pop() ?? imageUrl;
}
