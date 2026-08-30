import {
  createDefaultPageBackground,
  createDefaultSiteAppearance,
  normalizeSiteAppearance,
  type SiteAppearance,
  type SitePageKey
} from "../../api/siteSettingsApi";
import {
  themeDecorationSlotOptions,
  themeIconSlotOptions,
  type ThemeDecorationSlot,
  type ThemeIconSlot
} from "../../theme/themeSlots";

export type ThemeEditorPreviewPage = "login" | "problem" | "challenge" | "team" | "leaderboard" | "season" | "help" | "account" | "security-audit";
export type ThemeEditorViewport = "desktop" | "tablet" | "mobile";
export type ThemeEditorMode = "preview" | "select";
export type ThemeEditorCompareMode = "draft" | "saved" | "default";
export type ThemeEditorSurfaceId =
  | "global.background"
  | "global.colors"
  | "page.background"
  | "panel.primary"
  | "panel.header"
  | "panel.border"
  | `icon.${ThemeIconSlot}`
  | `decoration.${ThemeDecorationSlot}`;

export interface ThemeEditableSurface {
  id: ThemeEditorSurfaceId;
  group: "Global" | "Panels" | "Icons" | "Decorations";
  label: string;
  description: string;
  keywords: string[];
}

export const themeEditorPreviewPages: Array<{ key: ThemeEditorPreviewPage; label: string }> = [
  { key: "login", label: "登录页" },
  { key: "problem", label: "题目页" },
  { key: "challenge", label: "挑战页" },
  { key: "team", label: "战队页" },
  { key: "leaderboard", label: "榜单页" },
  { key: "season", label: "赛季页" },
  { key: "help", label: "帮助页" },
  { key: "account", label: "账号页" },
  { key: "security-audit", label: "安全审计页" }
];

export const themeEditorViewports: Array<{ key: ThemeEditorViewport; label: string; width: number }> = [
  { key: "desktop", label: "桌面", width: 1120 },
  { key: "tablet", label: "平板", width: 768 },
  { key: "mobile", label: "手机", width: 375 }
];

export const themeEditableSurfaces: ThemeEditableSurface[] = [
  { id: "global.background", group: "Global", label: "全站背景", description: "背景素材、构图位置与明暗效果", keywords: ["background", "背景", "overlay", "blur", "焦点", "明暗"] },
  { id: "global.colors", group: "Global", label: "全站颜色", description: "文字、强调色、导航与字体", keywords: ["colors", "tokens", "accent", "text", "font", "navigation", "颜色", "文字", "导航"] },
  { id: "page.background", group: "Global", label: "单页背景", description: "只覆盖当前类型页面的背景", keywords: ["page", "background", "position", "scale", "页面", "背景"] },
  { id: "panel.primary", group: "Panels", label: "主要面板", description: "面板纹理、透明度、圆角与阴影", keywords: ["panel", "radius", "opacity", "shadow", "background", "面板", "圆角", "阴影"] },
  { id: "panel.header", group: "Panels", label: "面板标题", description: "面板顶部的标题纹理", keywords: ["panel", "header", "texture", "面板", "标题"] },
  { id: "panel.border", group: "Panels", label: "面板边框", description: "面板四周的边框纹理", keywords: ["panel", "border", "texture", "面板", "边框"] },
  ...themeIconSlotOptions.map(({ key, label }) => ({
    id: `icon.${key}` as ThemeEditorSurfaceId,
    group: "Icons" as const,
    label: `${label}图标`,
    description: `${label}入口使用的主题图标`,
    keywords: ["icon", "图标", key, label]
  })),
  ...themeDecorationSlotOptions.map(({ key, label }) => ({
    id: `decoration.${key}` as ThemeEditorSurfaceId,
    group: "Decorations" as const,
    label: getDecorationLabel(key),
    description: `${getDecorationLabel(key)}使用的主题素材`,
    keywords: ["decoration", "装饰", key, label, getDecorationLabel(key)]
  }))
];

export interface ThemeEditorHistoryState {
  saved: SiteAppearance;
  present: SiteAppearance;
  past: SiteAppearance[];
  future: SiteAppearance[];
  gestureStart: SiteAppearance | null;
}

export type ThemeEditorHistoryAction =
  | { type: "initialize"; value: SiteAppearance }
  | { type: "change"; value: SiteAppearance }
  | { type: "undo" }
  | { type: "redo" }
  | { type: "begin-gesture" }
  | { type: "end-gesture" }
  | { type: "discard" }
  | { type: "save-success"; value: SiteAppearance };

export const ThemeEditorHistoryLimit = 50;

export function createThemeEditorHistory(value: SiteAppearance): ThemeEditorHistoryState {
  const normalized = normalizeSiteAppearance(value);
  return { saved: normalized, present: normalized, past: [], future: [], gestureStart: null };
}

export function reduceThemeEditorHistory(state: ThemeEditorHistoryState, action: ThemeEditorHistoryAction): ThemeEditorHistoryState {
  if (action.type === "initialize") return createThemeEditorHistory(action.value);
  if (action.type === "discard") return { ...state, present: state.saved, past: [], future: [], gestureStart: null };
  if (action.type === "save-success") return createThemeEditorHistory(action.value);
  if (action.type === "begin-gesture") return state.gestureStart ? state : { ...state, gestureStart: state.present };
  if (action.type === "end-gesture") {
    if (!state.gestureStart) return state;
    return appearanceEquals(state.gestureStart, state.present)
      ? { ...state, gestureStart: null }
      : { ...state, past: boundedHistory([...state.past, state.gestureStart]), future: [], gestureStart: null };
  }
  if (action.type === "undo") {
    const previous = state.past[state.past.length - 1];
    return previous ? { ...state, present: previous, past: state.past.slice(0, -1), future: [state.present, ...state.future], gestureStart: null } : state;
  }
  if (action.type === "redo") {
    const next = state.future[0];
    return next ? { ...state, present: next, past: boundedHistory([...state.past, state.present]), future: state.future.slice(1), gestureStart: null } : state;
  }

  const next = normalizeSiteAppearance(action.value);
  if (appearanceEquals(state.present, next)) return state;
  if (state.gestureStart) return { ...state, present: next, future: [] };
  return { ...state, present: next, past: boundedHistory([...state.past, state.present]), future: [] };
}

export function appearanceEquals(first: SiteAppearance, second: SiteAppearance) {
  return JSON.stringify(first) === JSON.stringify(second);
}

export function getThemeSurface(id: ThemeEditorSurfaceId) {
  return themeEditableSurfaces.find((surface) => surface.id === id) ?? themeEditableSurfaces[0];
}

export function getThemeSurfaceBreadcrumb(id: ThemeEditorSurfaceId) {
  const surface = getThemeSurface(id);
  return `${getSurfaceGroupLabel(surface.group)} > ${surface.label}`;
}

export function getSurfaceGroupLabel(group: ThemeEditableSurface["group"]) {
  if (group === "Global") return "全局";
  if (group === "Panels") return "面板";
  if (group === "Icons") return "图标";
  return "装饰";
}

export function getPreviewPageKey(page: ThemeEditorPreviewPage): SitePageKey {
  switch (page) {
    case "problem": return "problems";
    case "challenge": return "challenges";
    case "leaderboard": return "leaderboards";
    case "account": return "account-settings";
    case "security-audit": return "global";
    case "season": return "admin-challenges";
    case "team":
    case "help":
    case "login":
    default: return "global";
  }
}

export function resetThemeSurface(appearance: SiteAppearance, surfaceId: ThemeEditorSurfaceId, pageKey: SitePageKey) {
  const next = normalizeSiteAppearance(appearance);
  const defaults = createDefaultSiteAppearance();
  if (surfaceId === "global.background") next.background = defaults.background;
  if (surfaceId === "global.colors") next.theme = defaults.theme;
  if (surfaceId === "page.background") next.pages[pageKey] = createDefaultPageBackground();
  if (surfaceId === "panel.primary") next.panelSkin = defaults.panelSkin;
  if (surfaceId === "panel.header") next.panelSkin.headerTexture = null;
  if (surfaceId === "panel.border") next.panelSkin.borderTexture = null;
  if (surfaceId.startsWith("icon.")) next.icons[surfaceId.slice("icon.".length) as ThemeIconSlot] = null;
  if (surfaceId.startsWith("decoration.")) next.decorations[surfaceId.slice("decoration.".length) as ThemeDecorationSlot] = null;
  return normalizeSiteAppearance(next);
}

function boundedHistory(history: SiteAppearance[]) {
  return history.length > ThemeEditorHistoryLimit ? history.slice(-ThemeEditorHistoryLimit) : history;
}

function getDecorationLabel(slot: ThemeDecorationSlot) {
  if (slot === "pageHeader") return "页面标题装饰";
  if (slot === "cardHeader") return "卡片标题装饰";
  if (slot === "panelCorner") return "面板角落装饰";
  return "空状态插画";
}
