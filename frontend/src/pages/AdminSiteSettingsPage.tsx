import { useEffect, useState } from "react";
import {
  createDefaultPageBackground,
  createDefaultSiteAppearance,
  normalizeSiteAppearance,
  sitePageOptions,
  updateSiteAppearance,
  type SiteAppearance,
  type SiteAppearanceTheme,
  type SitePageBackground,
  type SitePageKey
} from "../api/siteSettingsApi";
import { BackgroundAppearanceEditor, type BackgroundAppearanceValue } from "../components/BackgroundAppearanceEditor";
import { useTheme } from "../theme/ThemeContext";

export function AdminSiteSettingsPage() {
  const { siteAppearance, reloadSiteAppearance, currentTheme } = useTheme();
  const [selectedPageKey, setSelectedPageKey] = useState<SitePageKey>("global");
  const [form, setForm] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setForm(normalizeSiteAppearance(siteAppearance));
  }, [siteAppearance]);

  async function handleSubmit() {
    setIsSaving(true);
    setError(null);
    setNotice(null);

    try {
      const updated = await updateSiteAppearance(form);
      setForm(updated);
      await reloadSiteAppearance();
      setNotice("站点背景配置已保存。选择 Root 配置风格的用户刷新后会看到新背景。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "站点背景配置保存失败。");
    } finally {
      setIsSaving(false);
    }
  }

  function updateTheme(patch: Partial<SiteAppearanceTheme>) {
    setForm((current) => ({
      ...current,
      theme: {
        ...current.theme,
        ...patch
      }
    }));
  }

  function updateSelectedPage(patch: Partial<SitePageBackground>) {
    setForm((current) => ({
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

  function updateSelectedEditorValue(value: BackgroundAppearanceValue) {
    updateSelectedPage({
      enabled: value.enabled,
      imageUrl: value.imageUrl,
      positionX: value.positionX,
      positionY: value.positionY,
      scale: value.scale
    });
    updateTheme({
      backgroundOverlayOpacity: value.overlayOpacity
    });
  }

  const selectedOption = sitePageOptions.find((option) => option.key === selectedPageKey) ?? sitePageOptions[0];
  const selectedPage = form.pages[selectedPageKey] ?? createDefaultPageBackground();
  const selectedEditorValue: BackgroundAppearanceValue = {
    enabled: selectedPage.enabled,
    imageUrl: selectedPage.imageUrl,
    positionX: selectedPage.positionX,
    positionY: selectedPage.positionY,
    scale: selectedPage.scale,
    overlayOpacity: form.theme.backgroundOverlayOpacity
  };

  return (
    <section className="admin-site-settings-page">
      <div className="page-header">
        <div>
          <p className="eyebrow">SITE SETTINGS</p>
          <h1>站点设置</h1>
          <p>配置 Root 自定义背景风格。该配置不会强制覆盖用户，只有用户选择 Root 配置风格时才会显示。</p>
        </div>
      </div>

      {(notice || error) && (
        <div className={error ? "alert error" : "quiet-note success"}>
          {error ?? notice}
        </div>
      )}

      <div className="site-settings-layout">
        <section className="admin-panel site-settings-form">
          <div className="admin-panel-header">
            <h2>分页背景配置</h2>
          </div>

          <div className="site-page-picker">
            {sitePageOptions.map((option) => (
              <button
                className={option.key === selectedPageKey ? "active" : ""}
                key={option.key}
                type="button"
                onClick={() => setSelectedPageKey(option.key)}
              >
                {option.label}
              </button>
            ))}
          </div>

          <div className="site-settings-fields">
            <div className="quiet-note">
              <strong>{selectedOption.label}</strong>
              <span>{selectedOption.description}</span>
            </div>

            <BackgroundAppearanceEditor
              value={selectedEditorValue}
              onChange={updateSelectedEditorValue}
              onSave={handleSubmit}
              isSaving={isSaving}
              title={`${selectedOption.label}背景`}
              description="编辑当前页面在 Root 配置风格下使用的背景。当前页未配置时会自动回退全局默认。"
              previewTitle={`${selectedOption.label}背景预览`}
              previewDescription={`当前主题：${currentTheme === "mystic-background" ? "Root 配置风格" : "默认风格"}。默认风格用户不会看到这些背景。`}
              saveLabel="保存全部配置"
              uploadLabel="上传背景图"
              clearLabel="清除当前页背景"
              onNotice={setNotice}
              onError={setError}
            />
          </div>
        </section>

        <section className="admin-panel site-visual-settings-panel">
          <div className="admin-panel-header">
            <h2>全局视觉参数</h2>
          </div>
          <div className="form-stack site-settings-fields">
            <label className="checkbox-line">
              <input
                type="checkbox"
                checked={form.theme.backgroundEnabled}
                onChange={(event) => updateTheme({ backgroundEnabled: event.target.checked })}
              />
              启用 Root 配置风格背景
            </label>

            <label>
              面板透明度：{form.theme.panelOpacity.toFixed(2)}
              <input
                className="range-input"
                type="range"
                min="0.35"
                max="0.95"
                step="0.05"
                value={form.theme.panelOpacity}
                onChange={(event) => updateTheme({ panelOpacity: Number(event.target.value) })}
              />
            </label>

            <label>
              模糊强度：{form.theme.panelBlur}px
              <input
                className="range-input"
                type="range"
                min="0"
                max="30"
                step="1"
                value={form.theme.panelBlur}
                onChange={(event) => updateTheme({ panelBlur: Number(event.target.value) })}
              />
            </label>

            <p className="muted site-settings-hint">
              遮罩透明度已放入背景编辑器，拖动滑杆即可实时预览。保存后只影响主动选择 Root 配置风格的用户。
            </p>

            <button className="button primary" type="button" disabled={isSaving} onClick={handleSubmit}>
              {isSaving ? "保存中..." : "保存全部配置"}
            </button>
          </div>
        </section>
      </div>
    </section>
  );
}
