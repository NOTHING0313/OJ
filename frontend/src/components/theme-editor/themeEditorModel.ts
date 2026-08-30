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
  { key: "login", label: "Login" },
  { key: "problem", label: "Problem" },
  { key: "challenge", label: "Challenge" },
  { key: "team", label: "Team" },
  { key: "leaderboard", label: "Leaderboard" },
  { key: "season", label: "Season" },
  { key: "help", label: "Help" },
  { key: "account", label: "Account" },
  { key: "security-audit", label: "Security Audit" }
];

export const themeEditorViewports: Array<{ key: ThemeEditorViewport; label: string; width: number }> = [
  { key: "desktop", label: "Desktop", width: 1120 },
  { key: "tablet", label: "Tablet", width: 768 },
  { key: "mobile", label: "Mobile", width: 375 }
];

export const themeEditableSurfaces: ThemeEditableSurface[] = [
  { id: "global.background", group: "Global", label: "Background", description: "全站受控背景与焦点位置", keywords: ["background", "背景", "overlay", "blur"] },
  { id: "global.colors", group: "Global", label: "Colors / Tokens", description: "文字、Accent、导航与字体", keywords: ["colors", "tokens", "accent", "text", "font", "navigation"] },
  { id: "page.background", group: "Global", label: "Page Background", description: "当前预览页面的既有背景覆盖", keywords: ["page", "background", "position", "scale"] },
  { id: "panel.primary", group: "Panels", label: "Primary Panel", description: "Panel 背景、透明度、圆角与阴影", keywords: ["panel", "radius", "opacity", "shadow", "background"] },
  { id: "panel.header", group: "Panels", label: "Panel Header", description: "Panel Header 纹理", keywords: ["panel", "header", "texture"] },
  { id: "panel.border", group: "Panels", label: "Panel Border", description: "Panel Border 纹理", keywords: ["panel", "border", "texture"] },
  ...themeIconSlotOptions.map(({ key, label }) => ({
    id: `icon.${key}` as ThemeEditorSurfaceId,
    group: "Icons" as const,
    label: `${label} Icon`,
    description: `${label}受控图标 Slot`,
    keywords: ["icon", key, label]
  })),
  ...themeDecorationSlotOptions.map(({ key, label }) => ({
    id: `decoration.${key}` as ThemeEditorSurfaceId,
    group: "Decorations" as const,
    label,
    description: `${label} 受控装饰区域`,
    keywords: ["decoration", key, label]
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
  return `${surface.group} > ${surface.label}`;
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
