import { type CSSProperties, type PointerEvent, useEffect, useRef, useState } from "react";
import {
  createDefaultPageBackground,
  resolveSiteAssetUrl,
  type SiteAppearance,
  type SitePageKey,
  type SiteThemeIconSlot
} from "../../api/siteSettingsApi";
import { themeDecorationSlotOptions, type ThemeIconSlot } from "../../theme/themeSlots";
import { AppHeaderView } from "../AppHeaderView";
import { AppBackground } from "../backgrounds/AppBackground";
import { LeaderboardHomeView } from "../leaderboards/LeaderboardHomeView";
import { HelpCenterView } from "../help/HelpCenterView";
import { ProblemDetailView } from "../problems/ProblemDetailView";
import { SiteFooter } from "../SiteFooter";
import {
  type ThemeEditorMode,
  type ThemeEditorPreviewPage,
  type ThemeEditorPreviewZoom,
  type ThemeEditorSurfaceId,
  type ThemeEditorViewport
} from "./themeEditorModel";
import { helpPreviewFixture, leaderboardPreviewFixture, problemPreviewFixture } from "./themePreviewFixtures";

interface ThemeEditorPreviewProps {
  appearance: SiteAppearance;
  page: ThemeEditorPreviewPage;
  pageBackgroundKey: SitePageKey;
  viewport: ThemeEditorViewport;
  zoom: ThemeEditorPreviewZoom;
  mode: ThemeEditorMode;
  selectedSurface: ThemeEditorSurfaceId;
  pulseSurface: ThemeEditorSurfaceId | null;
  onSelect: (surface: ThemeEditorSurfaceId) => void;
  onBackgroundPositionChange: (positionX: number, positionY: number) => void;
  onGestureStart: () => void;
  onGestureEnd: () => void;
}

export function ThemeEditorPreview({ appearance, page, pageBackgroundKey, viewport, zoom, mode, selectedSurface, pulseSurface, onSelect, onBackgroundPositionChange, onGestureStart, onGestureEnd }: ThemeEditorPreviewProps) {
  const previewRef = useRef<HTMLDivElement>(null);
  const genericBackground = appearance.background;
  const pageBackground = appearance.pages[pageBackgroundKey] ?? createDefaultPageBackground();
  const genericUrl = genericBackground.enabled ? resolveSiteAssetUrl(genericBackground.asset?.url) : undefined;
  const pageUrl = !genericUrl && pageBackground.enabled ? resolveSiteAssetUrl(pageBackground.imageUrl) : undefined;
  const backgroundUrl = genericUrl ?? pageUrl;
  const themeStyle = { ...buildPreviewThemeStyle(appearance), "--theme-preview-viewport-width": `${viewport === "desktop" ? 1120 : viewport === "tablet" ? 768 : 375}px` } as CSSProperties;
  const modifierClasses = buildPreviewModifierClasses(appearance);
  const selectedClass = mode === "select" ? ` is-selecting selected-${selectedSurface.replace(/\./g, "-")}` : "";
  const pulseClass = pulseSurface ? ` is-pulsing pulse-${pulseSurface.replace(/\./g, "-")}` : "";
  const viewportWidth = viewport === "desktop" ? 1120 : viewport === "tablet" ? 768 : 375;
  const canvasStyle: CSSProperties = zoom === "fit"
    ? { maxWidth: `${viewportWidth}px` }
    : { width: `${viewportWidth}px`, maxWidth: "none", zoom: Number(zoom) / 100 };

  useEffect(() => {
    const preview = previewRef.current;
    if (!preview) return;
    preview.querySelectorAll<HTMLElement>("[data-theme-editor-selected]").forEach((element) => element.removeAttribute("data-theme-editor-selected"));
    if (mode !== "select") return;
    preview.querySelectorAll<HTMLElement>(`[data-surface="${selectedSurface}"]`).forEach((element) => element.setAttribute("data-theme-editor-selected", "true"));
  }, [mode, page, selectedSurface]);

  function select(surface: ThemeEditorSurfaceId) {
    if (mode === "select") onSelect(surface);
  }

  function handleFocalPointer(event: PointerEvent<HTMLDivElement>) {
    if (mode !== "select" || selectedSurface !== "global.background" || !genericUrl) return;
    const bounds = event.currentTarget.getBoundingClientRect();
    onBackgroundPositionChange(
      clamp(((event.clientX - bounds.left) / bounds.width) * 100, 0, 100),
      clamp(((event.clientY - bounds.top) / bounds.height) * 100, 0, 100)
    );
  }

  return (
    <div className={`theme-editor-canvas viewport-${viewport} zoom-${zoom}`} style={canvasStyle}>
      <div ref={previewRef} data-theme-preview-content className={`theme-editor-preview-site site-theme-content app-shell ${modifierClasses}${selectedClass}${pulseClass}`} style={themeStyle} onClick={() => select("global.background")}>
        <AppBackground pathname={getPreviewRoute(page) ?? "/"} hasCustomWallpaper={Boolean(backgroundUrl)} contained />
        {backgroundUrl && <div className="theme-editor-preview-background" style={genericUrl ? {
          backgroundImage: `url("${genericUrl}")`,
          backgroundPosition: `${genericBackground.positionX ?? 50}% ${genericBackground.positionY ?? 50}%`,
          backgroundSize: genericBackground.sizeMode ?? "cover",
          backgroundRepeat: genericBackground.repeat ?? "no-repeat",
          filter: `blur(${genericBackground.blur ?? 0}px) brightness(${genericBackground.brightness ?? 100}%)`
        } : {
          backgroundImage: `url("${pageUrl}")`,
          backgroundPosition: `${pageBackground.positionX}% ${pageBackground.positionY}%`,
          backgroundSize: `${pageBackground.scale * 100}% auto`
        }} />}
        {backgroundUrl && <div className="theme-editor-preview-background-overlay" style={{
          background: genericUrl
            ? hexToRgba(genericBackground.overlayColor ?? "#000000", genericBackground.overlayOpacity ?? 0)
            : `rgba(0, 0, 0, ${pageBackground.overlayOpacity ?? appearance.theme.backgroundOverlayOpacity})`
        }} />}
        <AppHeaderView role={3} isAuthenticated hasPublicLeaderboard userName="Theme Preview User" avatarUrl={null} onLogout={() => undefined} interactive={false} activePath={getPreviewRoute(page)} renderIcon={(slot) => <span data-surface={`icon.${slot}`} onClick={() => select(`icon.${slot}` as ThemeEditorSurfaceId)}><DraftIcon slot={slot} appearance={appearance} /></span>} />
        <main className={`page-container theme-editor-preview-page${page === "leaderboard" || page === "problem" || page === "help" ? " production-preview" : ""}`} onClick={(event) => { event.stopPropagation(); const surface = (event.target as Element).closest<HTMLElement>("[data-surface]")?.dataset.surface as ThemeEditorSurfaceId | undefined; if (surface) select(surface); }}>
          {page === "leaderboard" ? (
            <LeaderboardHomeView {...leaderboardPreviewFixture} isLoading={false} error={null} canManage showPersonalRecord={false} />
          ) : page === "problem" ? (
            <ProblemDetailView problem={problemPreviewFixture} seasonScore={null} language={1} languages={[{ value: 1, label: "C++17" }, { value: 2, label: "C11" }, { value: 3, label: "C#" }]} isAuthenticated canManage challengeId={null} error={null} isSubmitting={false} editor={<div className="code-editor-shell"><div className="theme-editor-code-preview" aria-label="代码编辑器预览"><pre>{"#include <bits/stdc++.h>\nusing namespace std;\n\nint main() {\n    return 0;\n}"}</pre></div></div>} onSubmit={(event) => event.preventDefault()} onLanguageChange={() => undefined} onClearSource={() => undefined} />
          ) : page === "help" ? (
            <HelpCenterView documents={helpPreviewFixture.documents} document={helpPreviewFixture.document} isLoading={false} error={null} canManage />
          ) : (
            <>
              <PreviewPageHeader page={page} onSelect={() => select("decoration.pageHeader")} />
              <PreviewComposition page={page} appearance={appearance} onSelect={select} />
            </>
          )}
        </main>
        <SiteFooter />
        {genericUrl && mode === "select" && selectedSurface === "global.background" && (
          <div
            className="theme-editor-focal-zone"
            role="slider"
            tabIndex={0}
            aria-label="背景焦点位置"
            aria-valuetext={`${Math.round(genericBackground.positionX ?? 50)}, ${Math.round(genericBackground.positionY ?? 50)}`}
            onPointerDown={(event) => { onGestureStart(); event.currentTarget.setPointerCapture(event.pointerId); handleFocalPointer(event); }}
            onPointerMove={(event) => { if (event.currentTarget.hasPointerCapture(event.pointerId)) handleFocalPointer(event); }}
            onPointerUp={(event) => { if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId); onGestureEnd(); }}
            onPointerCancel={onGestureEnd}
          >
            <span style={{ left: `${genericBackground.positionX ?? 50}%`, top: `${genericBackground.positionY ?? 50}%` }} />
          </div>
        )}
      </div>
    </div>
  );
}

function PreviewPageHeader({ page, onSelect }: { page: ThemeEditorPreviewPage; onSelect: () => void }) {
  const labels: Record<ThemeEditorPreviewPage, [string, string, string]> = {
    login: ["AUTHENTICATION", "欢迎回来", "进入工作室"],
    problem: ["PROBLEMS", "题目列表", "查看当前可用题目并提交代码"],
    challenge: ["CHALLENGE", "挑战中心", "完成任务并推进挑战进度"],
    team: ["TEAM WORKSPACE", "战队空间", "协作、讨论与项目进度"],
    leaderboard: ["LEADERBOARD", "排行榜", "查看赛季积分与排名"],
    season: ["SEASON", "赛季管理", "管理挑战、题目与奖励规则"],
    help: ["HELP CENTER", "帮助中心", "阅读平台使用与规则说明"],
    account: ["ACCOUNT", "账号设置", "管理个人资料与安全选项"],
    "security-audit": ["SECURITY", "安全审计", "查看不可变安全事件记录"]
  };
  const [, title] = labels[page];
  return <div className="page-header theme-editor-surface" data-surface="decoration.pageHeader" onClick={onSelect}><div><h2>{title}</h2></div></div>;
}

function getPreviewRoute(page: ThemeEditorPreviewPage) {
  if (page === "problem") return "/problems/theme-preview-problem";
  if (page === "challenge") return "/challenges";
  if (page === "leaderboard" || page === "season") return "/leaderboards";
  if (page === "team") return "/teams";
  if (page === "help") return "/help";
  if (page === "account") return "/profile/me";
  return undefined;
}

function PreviewComposition({ page, appearance, onSelect }: { page: ThemeEditorPreviewPage; appearance: SiteAppearance; onSelect: (surface: ThemeEditorSurfaceId) => void }) {
  if (page === "login") return <div className="theme-editor-preview-login"><section className="content-block theme-editor-surface" onClick={() => onSelect("panel.primary")}><div className="workspace-section-header" onClick={(event) => { event.stopPropagation(); onSelect("panel.header"); }}><strong>账号登录</strong></div><label>账号 / 邮箱<input value="UnrealStudio" readOnly /></label><label>密码<input value="••••••••" readOnly /></label><button className="button primary" type="button">进入工作室</button></section></div>;
  if (page === "team") return <div className="theme-editor-preview-grid"><section className="content-block theme-editor-surface" onClick={() => onSelect("panel.primary")}><div className="workspace-section-header"><DraftIcon slot="chat" appearance={appearance} /><strong>战队聊天</strong></div><button className="button" type="button"><DraftIcon slot="git" appearance={appearance} />Git 项目</button></section><EmptyPreview onSelect={onSelect} /></div>;
  if (page === "account") return <div className="theme-editor-preview-grid"><section className="content-block theme-editor-surface" onClick={() => onSelect("panel.primary")}><strong>个人资料</strong><label>用户名<input value="UnrealStudio" readOnly /></label><button className="button primary" type="button">保存资料</button></section><section className="content-block"><strong>安全设置</strong><span className="theme-editor-badge">已启用</span></section></div>;
  if (page === "security-audit") return <PreviewTable onSelect={onSelect} security />;
  if (page === "season") return <div className="theme-editor-preview-grid"><section className="content-block theme-editor-surface" onClick={() => onSelect("panel.primary")}><div className="workspace-section-header"><DraftIcon slot="season" appearance={appearance} /><strong>2026 秋季赛</strong></div><p>未开始 · 3 道题</p><button className="button primary" type="button"><DraftIcon slot="reward" appearance={appearance} />奖励设置</button></section><PreviewTable onSelect={onSelect} /></div>;
  if (page === "challenge") return <div className="theme-editor-preview-grid"><section className="content-block theme-editor-surface" onClick={() => onSelect("panel.primary")}><div className="workspace-section-header"><DraftIcon slot="challenge" appearance={appearance} /><strong>算法挑战</strong></div><p>4 个任务 · 当前进度 75%</p><div className="theme-editor-progress"><span /></div></section><EmptyPreview onSelect={onSelect} /></div>;
  return null;
}

function PreviewTable({ onSelect, security = false }: { onSelect: (surface: ThemeEditorSurfaceId) => void; security?: boolean }) {
  return <div className="table-wrap theme-editor-surface" onClick={() => onSelect("panel.border")}><table><thead><tr><th>{security ? "事件" : "名称"}</th><th>状态</th><th>时间</th></tr></thead><tbody><tr><td>{security ? "SiteAppearance.Updated" : "2026 秋季赛"}</td><td><span className="theme-editor-badge">PASS</span></td><td>16:05</td></tr><tr><td>{security ? "User.Login" : "测试挑战"}</td><td>Ready</td><td>16:03</td></tr></tbody></table></div>;
}

function EmptyPreview({ onSelect }: { onSelect: (surface: ThemeEditorSurfaceId) => void }) {
  return <div className="empty-state theme-editor-surface" onClick={() => onSelect("decoration.emptyState")}><strong>暂无更多内容</strong></div>;
}

function DraftIcon({ slot, appearance }: { slot: ThemeIconSlot; appearance: SiteAppearance }) {
  const assignment = appearance.icons[slot];
  const url = resolveSiteAssetUrl(assignment?.asset?.url);
  return assignment?.enabled && url ? <PreviewImage assignment={assignment} url={url} /> : null;
}

function PreviewImage({ assignment, url }: { assignment: SiteThemeIconSlot; url: string }) {
  const [available, setAvailable] = useState(true);

  useEffect(() => setAvailable(true), [url]);

  if (!available) return null;
  return <span className="theme-icon-slot" aria-hidden="true"><img src={url} alt="" onError={() => setAvailable(false)} style={{ opacity: assignment.opacity ?? 1, transform: `translate(${assignment.offsetX ?? 0}px, ${assignment.offsetY ?? 0}px) scale(${assignment.scale ?? 1})` }} /></span>;
}

function buildPreviewThemeStyle(appearance: SiteAppearance) {
  const theme = appearance.theme;
  const style: Record<string, string> = {
    "--site-panel-opacity": String(theme.panelOpacity),
    "--site-panel-blur": `${theme.panelBlur}px`,
    "--site-panel-color": theme.panelColor,
    "--oj-panel-bg": hexToRgba(theme.panelColor, theme.panelOpacity),
    "--oj-input-bg": hexToRgba(theme.panelColor, Math.min(theme.panelOpacity + 0.08, 0.98)),
    "--oj-panel-border": `rgba(255, 255, 255, ${theme.panelBorderOpacity})`,
    "--oj-text-primary": theme.textPrimaryColor,
    "--oj-text-secondary": theme.textSecondaryColor,
    "--oj-text-muted": theme.textMutedColor,
    "--oj-accent": theme.accentColor,
    "--oj-nav-bg": hexToRgba("#05080F", theme.navOpacity),
    "--oj-nav-blur": `${theme.navBlur}px`,
    "--oj-nav-text": theme.navTextColor,
    "--oj-nav-active": theme.navActiveColor,
    "--oj-font-family": resolvePreviewFont(theme.fontPreset)
  };
  const panel = appearance.panelSkin;
  if (panel.enabled) {
    const background = resolveSiteAssetUrl(panel.backgroundTexture?.url);
    const header = resolveSiteAssetUrl(panel.headerTexture?.url);
    const border = resolveSiteAssetUrl(panel.borderTexture?.url);
    const opacity = panel.textureOpacity ?? 0.15;
    if (background) style["--theme-panel-bg-layer"] = `linear-gradient(rgba(5, 6, 8, ${1 - opacity}), rgba(5, 6, 8, ${1 - opacity})), url("${background}")`;
    if (header) style["--theme-panel-header-layer"] = `linear-gradient(rgba(5, 6, 8, ${1 - opacity}), rgba(5, 6, 8, ${1 - opacity})), url("${header}")`;
    if (border) style["--theme-panel-border-image"] = `url("${border}")`;
    if (panel.backgroundOpacity != null) style["--theme-panel-bg-color"] = `color-mix(in srgb, var(--oj-panel-bg) ${panel.backgroundOpacity * 100}%, transparent)`;
    if (panel.radius != null) style["--theme-panel-radius"] = `${panel.radius}px`;
    if (panel.shadowStrength != null) style["--theme-panel-shadow"] = `0 18px 48px rgba(0, 0, 0, ${panel.shadowStrength})`;
  }
  for (const { key } of themeDecorationSlotOptions) {
    const slot = appearance.decorations[key];
    const url = resolveSiteAssetUrl(slot?.asset?.url);
    if (!slot?.enabled || !url) continue;
    const cssKey = key.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
    style[`--theme-decoration-${cssKey}-image`] = `url("${url}")`;
    style[`--theme-decoration-${cssKey}-opacity`] = String(slot.opacity ?? 1);
    style[`--theme-decoration-${cssKey}-scale`] = String(slot.scale ?? 1);
    style[`--theme-decoration-${cssKey}-offset-x`] = `${slot.offsetX ?? 0}px`;
    style[`--theme-decoration-${cssKey}-offset-y`] = `${slot.offsetY ?? 0}px`;
    if (key !== "panelCorner") {
      const alignment = slot.alignment ?? "end";
      style[`--theme-decoration-${cssKey}-anchor`] = alignment === "start" ? "0%" : alignment === "center" ? "50%" : "100%";
      style[`--theme-decoration-${cssKey}-translate`] = alignment === "start" ? "0%" : alignment === "center" ? "-50%" : "-100%";
    }
  }
  return style as CSSProperties;
}

function buildPreviewModifierClasses(appearance: SiteAppearance) {
  const classes: string[] = [];
  const panel = appearance.panelSkin;
  if (panel.enabled) {
    classes.push("theme-panel-skin");
    if (panel.backgroundTexture) classes.push("theme-panel-bg-texture");
    if (panel.headerTexture) classes.push("theme-panel-header-texture");
    if (panel.borderTexture) classes.push("theme-panel-border-texture");
    if (panel.backgroundOpacity != null) classes.push("theme-panel-bg-opacity");
    if (panel.radius != null) classes.push("theme-panel-radius");
    if (panel.shadowStrength != null) classes.push("theme-panel-shadow");
  }
  for (const { key } of themeDecorationSlotOptions) {
    const slot = appearance.decorations[key];
    if (!slot?.enabled || !slot.asset) continue;
    const cssKey = key.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
    classes.push(`theme-decoration-${cssKey}`);
    if (key === "panelCorner") classes.push(`theme-decoration-panel-corner-${slot.corner ?? "top-right"}`);
  }
  return classes.join(" ");
}

function hexToRgba(hex: string, opacity: number) {
  const value = /^#[0-9a-fA-F]{6}$/.test(hex) ? hex : "#000000";
  const red = Number.parseInt(value.slice(1, 3), 16);
  const green = Number.parseInt(value.slice(3, 5), 16);
  const blue = Number.parseInt(value.slice(5, 7), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}

function resolvePreviewFont(preset: SiteAppearance["theme"]["fontPreset"]) {
  if (preset === "readable") return '"Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", system-ui, sans-serif';
  if (preset === "mono") return '"Cascadia Code", "JetBrains Mono", "SFMono-Regular", Consolas, monospace';
  return 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}
