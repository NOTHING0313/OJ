import { type ChangeEvent, type CSSProperties, type ReactNode, useEffect, useMemo, useRef, useState } from "react";
import {
  createDefaultPageBackground,
  createDefaultSiteAppearance,
  normalizeSiteAppearance,
  resolveSiteAssetUrl,
  sitePageOptions,
  updateSiteAppearance,
  type SiteAppearance,
  type SiteAppearanceTheme,
  type SitePageBackground,
  type SitePageKey
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

export function AdminSiteSettingsPage() {
  const { siteAppearance, reloadSiteAppearance, currentTheme } = useTheme();
  const [selectedPageKey, setSelectedPageKey] = useState<SitePageKey>("global");
  const [initialConfig, setInitialConfig] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [draftConfig, setDraftConfig] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
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
    overlayOpacity: draftConfig.theme.backgroundOverlayOpacity
  };
  const pageControlsDisabled = !selectedEditorValue.enabled || isSaving;

  function updateTheme(patch: Partial<SiteAppearanceTheme>) {
    setDraftConfig((current) => ({ ...current, theme: { ...current.theme, ...patch } }));
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

  async function handleSubmit() {
    setIsSaving(true);
    setError(null);
    setNotice(null);

    try {
      const updated = normalizeSiteAppearance(await updateSiteAppearance(draftConfig));
      setInitialConfig(updated);
      setDraftConfig(normalizeSiteAppearance(updated));
      await reloadSiteAppearance();
      setNotice("站点背景配置已保存。选择 Root 配置风格的用户刷新后会看到新背景。");
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

              <SettingsSection title="显示效果" description="缩放背景并调整页面内容上方的暗色遮罩。">
                <div className="site-settings-slider-stack">
                  <SettingSlider label="缩放" value={selectedEditorValue.scale} valueLabel={`${selectedEditorValue.scale.toFixed(2)}×`} min={0.5} max={2.5} step={0.05} disabled={pageControlsDisabled} onChange={(scale) => updateSelectedPage({ scale })} />
                  <SettingSlider label="遮罩" value={selectedEditorValue.overlayOpacity} valueLabel={`${Math.round(selectedEditorValue.overlayOpacity * 100)}%`} min={0} max={1} step={0.05} disabled={pageControlsDisabled} onChange={(backgroundOverlayOpacity) => updateTheme({ backgroundOverlayOpacity })} />
                </div>
              </SettingsSection>
            </div>

            <SettingsSection title="全局视觉参数" description="保留现有 Root 配置风格总开关与面板效果。">
              <SettingsSwitch checked={draftConfig.theme.backgroundEnabled} label="启用 Root 配置风格背景" description="只影响主动选择 Root 配置风格的用户" disabled={isSaving} onChange={(backgroundEnabled) => updateTheme({ backgroundEnabled })} />
              <div className="site-settings-slider-stack">
                <SettingSlider label="面板透明度" value={draftConfig.theme.panelOpacity} valueLabel={draftConfig.theme.panelOpacity.toFixed(2)} min={0.35} max={0.95} step={0.05} disabled={isSaving} onChange={(panelOpacity) => updateTheme({ panelOpacity })} />
                <SettingSlider label="模糊强度" value={draftConfig.theme.panelBlur} valueLabel={`${draftConfig.theme.panelBlur.toFixed(0)}px`} min={0} max={30} step={1} disabled={isSaving} onChange={(panelBlur) => updateTheme({ panelBlur })} />
              </div>
            </SettingsSection>
          </section>

          <aside className="site-settings-preview-column" aria-label="实时预览">
            <div className="site-settings-column-header">
              <div><h2>实时预览</h2><p>所有变化仅应用到本地草稿，保存时才会提交。</p></div>
              <span className="site-settings-theme-chip">{currentTheme === "mystic-background" ? "Root 风格" : "默认风格"}</span>
            </div>

            <BackgroundPreview value={selectedEditorValue} panelOpacity={draftConfig.theme.panelOpacity} panelBlur={draftConfig.theme.panelBlur} />

            <div className="site-settings-draft-summary">
              <h3>当前状态</h3>
              <dl>
                <div><dt>背景</dt><dd>{selectedEditorValue.enabled ? "已启用" : "已关闭"}</dd></div>
                <div><dt>X</dt><dd>{selectedEditorValue.positionX.toFixed(0)}%</dd></div>
                <div><dt>Y</dt><dd>{selectedEditorValue.positionY.toFixed(0)}%</dd></div>
                <div><dt>缩放</dt><dd>{selectedEditorValue.scale.toFixed(2)}×</dd></div>
                <div><dt>遮罩</dt><dd>{Math.round(selectedEditorValue.overlayOpacity * 100)}%</dd></div>
              </dl>
              <button className="button site-settings-reset-button" type="button" disabled={isSaving} onClick={handleRestoreDefault}>恢复默认</button>
            </div>
          </aside>
        </div>

        <div className="site-settings-action-bar">
          <div className={isDirty ? "site-settings-dirty-status dirty" : "site-settings-dirty-status"}>
            <span aria-hidden="true" />{isDirty ? "有未保存修改" : "配置已同步"}
          </div>
          <div className="site-settings-action-buttons">
            <button className="button" type="button" disabled={!isDirty || isSaving} onClick={handleDiscard}>放弃修改</button>
            <button className="button primary" type="button" disabled={!isDirty || isSaving || isUploading} onClick={handleSubmit}>{isSaving ? "保存中..." : "保存配置"}</button>
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

function BackgroundPreview({ value, panelOpacity, panelBlur }: { value: SitePageBackground & { overlayOpacity: number }; panelOpacity: number; panelBlur: number }) {
  const previewUrl = resolveSiteAssetUrl(value.imageUrl);
  const previewStyle: CSSProperties = value.enabled && previewUrl ? {
    backgroundImage: `url("${previewUrl}")`,
    backgroundPosition: `${value.positionX}% ${value.positionY}%`,
    backgroundSize: `${value.scale * 100}% auto`
  } : {};

  return (
    <div className={value.enabled ? "site-settings-preview-card active" : "site-settings-preview-card"} style={previewStyle}>
      <div className="site-settings-preview-overlay" style={{ background: `rgba(0, 0, 0, ${value.enabled ? value.overlayOpacity : 0})` }} />
      <div className="site-settings-preview-shell">
        <header><strong>ONLINEJUDGE</strong><span>题目　提交　榜单</span></header>
        <main>
          <span className="site-settings-preview-eyebrow">PAGE PREVIEW</span>
          <h3>页面内容预览</h3>
          <p>背景位置、缩放、遮罩和面板效果会即时反映在这里。</p>
          <article style={{ background: `rgba(17, 21, 29, ${panelOpacity})`, backdropFilter: `blur(${panelBlur}px)` }}>
            <strong>内容卡片</strong><span>用于确认正文在当前背景上的可读性。</span>
          </article>
        </main>
      </div>
    </div>
  );
}

function getFileName(imageUrl: string) {
  const cleanPath = imageUrl.split(/[?#]/)[0];
  return cleanPath.split("/").filter(Boolean).pop() ?? imageUrl;
}
