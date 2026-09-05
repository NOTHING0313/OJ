import { requestFile, request } from "./httpClient";
import { normalizeUploadedImagePath, resolveSiteAssetUrl } from "../utils/uploadedImageUrl";
import {
  isThemeDecorationSlot,
  isThemeIconSlot,
  type ThemeDecorationSlot,
  type ThemeIconSlot
} from "../theme/themeSlots";

export { normalizeUploadedImagePath, resolveSiteAssetUrl };

export type SitePageKey =
  | "global"
  | "problems"
  | "challenges"
  | "leaderboards"
  | "profile"
  | "account-settings"
  | "admin-problems"
  | "admin-challenges"
  | "file-task"
  | "submissions";

export type SiteFontPreset = "system" | "readable" | "mono";
export type ThemeBackgroundSizeMode = "cover" | "contain" | "auto";
export type ThemeBackgroundRepeat = "no-repeat" | "repeat" | "repeat-x" | "repeat-y";
export type ThemeBackgroundAttachment = "scroll" | "fixed";

export interface ThemeAssetReference {
  assetId: string;
  url: string;
}

export interface ThemeAsset extends ThemeAssetReference {
  displayName: string | null;
  contentType: string;
  size: number;
}

export interface ThemeAssetLibraryItem extends ThemeAsset {
  usedBy: string[];
}

export interface SiteThemeIconSlot {
  enabled: boolean;
  asset: ThemeAssetReference | null;
  opacity: number | null;
  scale: number | null;
  offsetX: number | null;
  offsetY: number | null;
}

export type ThemeDecorationAlignment = "start" | "center" | "end";
export type ThemeDecorationCorner = "top-left" | "top-right" | "bottom-left" | "bottom-right";

export interface SiteThemeDecorationSlot extends SiteThemeIconSlot {
  alignment: ThemeDecorationAlignment | null;
  corner: ThemeDecorationCorner | null;
}

export interface SiteThemeBackground {
  enabled: boolean;
  asset: ThemeAssetReference | null;
  positionX: number | null;
  positionY: number | null;
  sizeMode: ThemeBackgroundSizeMode | null;
  repeat: ThemeBackgroundRepeat | null;
  attachment: ThemeBackgroundAttachment | null;
  overlayColor: string | null;
  overlayOpacity: number | null;
  blur: number | null;
  brightness: number | null;
}

export interface SitePanelSkin {
  enabled: boolean;
  backgroundTexture: ThemeAssetReference | null;
  headerTexture: ThemeAssetReference | null;
  borderTexture: ThemeAssetReference | null;
  backgroundOpacity: number | null;
  textureOpacity: number | null;
  radius: number | null;
  shadowStrength: number | null;
}

export interface SiteAppearanceTheme {
  backgroundEnabled: boolean;
  backgroundOverlayOpacity: number;
  panelOpacity: number;
  panelBlur: number;
  panelColor: string;
  panelBorderOpacity: number;
  textPrimaryColor: string;
  textSecondaryColor: string;
  textMutedColor: string;
  accentColor: string;
  navOpacity: number;
  navBlur: number;
  navTextColor: string;
  navActiveColor: string;
  fontPreset: SiteFontPreset;
}

export interface SitePageBackground {
  enabled: boolean;
  imageUrl: string | null;
  positionX: number;
  positionY: number;
  scale: number;
  overlayOpacity: number | null;
}

export interface SiteAppearance {
  theme: SiteAppearanceTheme;
  pages: Record<string, SitePageBackground>;
  background: SiteThemeBackground;
  panelSkin: SitePanelSkin;
  icons: Partial<Record<ThemeIconSlot, SiteThemeIconSlot | null>>;
  decorations: Partial<Record<ThemeDecorationSlot, SiteThemeDecorationSlot | null>>;
}

export type UpdateSiteAppearanceRequest = SiteAppearance;

export interface ThemePreset {
  id: string | null;
  name: string;
  description: string | null;
  schemaVersion: number;
  appearance: SiteAppearance;
  createdAt: string | null;
  updatedAt: string | null;
  isBuiltIn: boolean;
  assetCount: number;
}

export interface ThemePresetList {
  items: ThemePreset[];
  lastAppliedPresetId: string | null;
}

export interface ThemePackPreflight {
  name: string;
  description: string | null;
  format: string;
  version: number;
  schemaVersion: number;
  assetCount: number;
  totalAssetBytes: number;
  hasBackground: boolean;
  panelAssetCount: number;
  iconOverrideCount: number;
  decorationCount: number;
  hasNameCollision: boolean;
  resolvedName: string;
  warnings: string[];
}

export const sitePageOptions: Array<{ key: SitePageKey; label: string; description: string }> = [
  { key: "global", label: "全局默认", description: "页面没有单独配置时使用的背景。" },
  { key: "problems", label: "题目页面", description: "题目列表、题目详情和答题页面。" },
  { key: "challenges", label: "挑战页面", description: "挑战列表、棋盘和普通任务详情。" },
  { key: "leaderboards", label: "榜单页面", description: "全站榜单和挑战榜单。" },
  { key: "profile", label: "个人中心", description: "个人中心和用户主页。" },
  { key: "account-settings", label: "账号设置", description: "账号设置与安全中心。" },
  { key: "admin-problems", label: "题目管理", description: "Root 和出题管理的题目相关页面。" },
  { key: "admin-challenges", label: "挑战管理", description: "Root 和出题管理的挑战相关页面。" },
  { key: "file-task", label: "文件题页面", description: "Challenge ZIP 文件题答题页。" },
  { key: "submissions", label: "提交记录", description: "我的提交、提交详情和提交管理。" }
];

export function getSiteAppearance() {
  return request<SiteAppearance>("/api/site-settings/appearance").then(normalizeSiteAppearance);
}

export function updateSiteAppearance(payload: UpdateSiteAppearanceRequest) {
  return request<SiteAppearance>("/api/site-settings/appearance", {
    method: "PUT",
    body: JSON.stringify(normalizeSiteAppearance(payload))
  }).then(normalizeSiteAppearance);
}

export function uploadThemeAsset(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<ThemeAsset>("/api/site-settings/theme-assets", { method: "POST", body });
}

export function listThemeAssets() {
  return request<ThemeAssetLibraryItem[]>("/api/site-settings/theme-assets");
}

export function deleteThemeAsset(assetId: string) {
  return request<void>(`/api/site-settings/theme-assets/${encodeURIComponent(assetId)}`, { method: "DELETE" });
}

export function renameThemeAsset(assetId: string, displayName: string) {
  return request<ThemeAsset>(`/api/site-settings/theme-assets/${encodeURIComponent(assetId)}/name`, {
    method: "PATCH",
    body: JSON.stringify({ displayName })
  });
}

export function listThemePresets() {
  return request<ThemePresetList>("/api/site-settings/theme-presets").then((value) => ({
    ...value,
    items: value.items.map(normalizeThemePreset)
  }));
}

export function createThemePreset(name: string, description: string | null, appearance: SiteAppearance) {
  return request<ThemePreset>("/api/site-settings/theme-presets", {
    method: "POST",
    body: JSON.stringify({ name, description, appearance: normalizeSiteAppearance(appearance) })
  }).then(normalizeThemePreset);
}

export function updateThemePreset(presetId: string, name: string, description: string | null, appearance: SiteAppearance) {
  return request<ThemePreset>(`/api/site-settings/theme-presets/${encodeURIComponent(presetId)}`, {
    method: "PUT",
    body: JSON.stringify({ name, description, appearance: normalizeSiteAppearance(appearance) })
  }).then(normalizeThemePreset);
}

export function duplicateThemePreset(presetId: string) {
  return request<ThemePreset>(`/api/site-settings/theme-presets/${encodeURIComponent(presetId)}/duplicate`, { method: "POST" }).then(normalizeThemePreset);
}

export function renameThemePreset(presetId: string, name: string) {
  return request<ThemePreset>(`/api/site-settings/theme-presets/${encodeURIComponent(presetId)}/name`, {
    method: "PATCH",
    body: JSON.stringify({ name })
  }).then(normalizeThemePreset);
}

export function deleteThemePreset(presetId: string) {
  return request<void>(`/api/site-settings/theme-presets/${encodeURIComponent(presetId)}`, { method: "DELETE" });
}

export function applyThemePreset(presetId: string | null) {
  const path = presetId
    ? `/api/site-settings/theme-presets/${encodeURIComponent(presetId)}/apply`
    : "/api/site-settings/theme-presets/default/apply";
  return request<SiteAppearance>(path, { method: "POST" }).then(normalizeSiteAppearance);
}

export async function exportThemePreset(presetId: string, name: string) {
  const response = await requestFile(`/api/site-settings/theme-presets/${encodeURIComponent(presetId)}/export`);
  const blob = response.blob;
  const url = URL.createObjectURL(blob);
  try {
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${name.replace(/[^\p{L}\p{N}._-]+/gu, "-") || "theme"}.oj-theme.zip`;
    anchor.click();
  } finally {
    URL.revokeObjectURL(url);
  }
}

export function importThemePreset(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<ThemePreset>("/api/site-settings/theme-presets/import", { method: "POST", body }).then(normalizeThemePreset);
}

export function preflightThemePresetImport(file: File) {
  const body = new FormData();
  body.append("file", file);
  return request<ThemePackPreflight>("/api/site-settings/theme-presets/import/preflight", { method: "POST", body });
}

function normalizeThemePreset(value: ThemePreset): ThemePreset {
  return {
    ...value,
    appearance: normalizeSiteAppearance(value.appearance),
    assetCount: Number.isFinite(value.assetCount) ? Math.max(0, value.assetCount) : 0,
    isBuiltIn: Boolean(value.isBuiltIn)
  };
}

export function createDefaultPageBackground(): SitePageBackground {
  return {
    enabled: false,
    imageUrl: null,
    positionX: 50,
    positionY: 50,
    scale: 1,
    overlayOpacity: null
  };
}

export function createDefaultSiteAppearance(): SiteAppearance {
  const pages: Record<string, SitePageBackground> = {};

  for (const pageKey of sitePageOptions.map((option) => option.key)) {
    pages[pageKey] = createDefaultPageBackground();
  }

  return {
    theme: {
      backgroundEnabled: false,
      backgroundOverlayOpacity: 0.65,
      panelOpacity: 0.72,
      panelBlur: 12,
      panelColor: "#11141A",
      panelBorderOpacity: 0.14,
      textPrimaryColor: "#F2F4F8",
      textSecondaryColor: "#AEB6CA",
      textMutedColor: "#7F8798",
      accentColor: "#6E7BFF",
      navOpacity: 0.58,
      navBlur: 18,
      navTextColor: "#D9DEE9",
      navActiveColor: "#F2F4F8",
      fontPreset: "system"
    },
    pages,
    background: {
      enabled: false,
      asset: null,
      positionX: null,
      positionY: null,
      sizeMode: null,
      repeat: null,
      attachment: null,
      overlayColor: null,
      overlayOpacity: null,
      blur: null,
      brightness: null
    },
    panelSkin: {
      enabled: false,
      backgroundTexture: null,
      headerTexture: null,
      borderTexture: null,
      backgroundOpacity: null,
      textureOpacity: null,
      radius: null,
      shadowStrength: null
    },
    icons: {},
    decorations: {}
  };
}

export function normalizeSiteAppearance(value: SiteAppearance | any): SiteAppearance {
  const fallback = createDefaultSiteAppearance();

  if (!value || typeof value !== "object") {
    return fallback;
  }

  if ("backgroundImageUrl" in value || "backgroundEnabled" in value || "backgroundOverlayOpacity" in value) {
    fallback.theme.backgroundEnabled = Boolean(value.backgroundEnabled);
    fallback.theme.backgroundOverlayOpacity = readNumber(value.backgroundOverlayOpacity, 0.65);
    fallback.pages.global.enabled = Boolean(value.backgroundEnabled);
    fallback.pages.global.imageUrl = normalizeUploadedImagePath(value.backgroundImageUrl);
    return fallback;
  }

  fallback.theme = {
    backgroundEnabled: Boolean(value.theme?.backgroundEnabled),
    backgroundOverlayOpacity: readBoundedNumber(value.theme?.backgroundOverlayOpacity, 0.65, 0, 1),
    panelOpacity: readBoundedNumber(value.theme?.panelOpacity, 0.72, 0.35, 0.95),
    panelBlur: readBoundedNumber(value.theme?.panelBlur, 12, 0, 30),
    panelColor: readColor(value.theme?.panelColor, "#11141A"),
    panelBorderOpacity: readBoundedNumber(value.theme?.panelBorderOpacity, 0.14, 0, 0.5),
    textPrimaryColor: readColor(value.theme?.textPrimaryColor, "#F2F4F8"),
    textSecondaryColor: readColor(value.theme?.textSecondaryColor, "#AEB6CA"),
    textMutedColor: readColor(value.theme?.textMutedColor, "#7F8798"),
    accentColor: readColor(value.theme?.accentColor, "#6E7BFF"),
    navOpacity: readBoundedNumber(value.theme?.navOpacity, 0.58, 0.35, 1),
    navBlur: readBoundedNumber(value.theme?.navBlur, 18, 0, 30),
    navTextColor: readColor(value.theme?.navTextColor, "#D9DEE9"),
    navActiveColor: readColor(value.theme?.navActiveColor, "#F2F4F8"),
    fontPreset: readFontPreset(value.theme?.fontPreset)
  };

  const background = value.background ?? {};
  fallback.background = {
    enabled: Boolean(background.enabled),
    asset: readThemeAsset(background.asset),
    positionX: readOptionalBoundedNumber(background.positionX, 0, 100),
    positionY: readOptionalBoundedNumber(background.positionY, 0, 100),
    sizeMode: readBackgroundSizeMode(background.sizeMode),
    repeat: readBackgroundRepeat(background.repeat),
    attachment: readBackgroundAttachment(background.attachment),
    overlayColor: background.overlayColor == null ? null : readColor(background.overlayColor, "#000000"),
    overlayOpacity: readOptionalBoundedNumber(background.overlayOpacity, 0, 1),
    blur: readOptionalBoundedNumber(background.blur, 0, 20),
    brightness: readOptionalBoundedNumber(background.brightness, 50, 150)
  };

  const panelSkin = value.panelSkin ?? {};
  fallback.panelSkin = {
    enabled: Boolean(panelSkin.enabled),
    backgroundTexture: readThemeAsset(panelSkin.backgroundTexture),
    headerTexture: readThemeAsset(panelSkin.headerTexture),
    borderTexture: readThemeAsset(panelSkin.borderTexture),
    backgroundOpacity: readOptionalBoundedNumber(panelSkin.backgroundOpacity, 0, 1),
    textureOpacity: readOptionalBoundedNumber(panelSkin.textureOpacity, 0, 1),
    radius: readOptionalBoundedNumber(panelSkin.radius, 0, 32),
    shadowStrength: readOptionalBoundedNumber(panelSkin.shadowStrength, 0, 1)
  };

  const icons = value.icons ?? {};
  for (const [key, slot] of Object.entries(icons)) {
    if (!isThemeIconSlot(key)) continue;
    fallback.icons[key] = readThemeIconSlot(slot);
  }

  const decorations = value.decorations ?? {};
  for (const [key, slot] of Object.entries(decorations)) {
    if (!isThemeDecorationSlot(key)) continue;
    fallback.decorations[key] = readThemeDecorationSlot(slot);
  }

  for (const { key } of sitePageOptions) {
    const source = value.pages?.[key] ?? readLegacyPage(value.pages, key) ?? {};
    fallback.pages[key] = {
      enabled: Boolean(source.enabled),
      imageUrl: normalizeUploadedImagePath(source.imageUrl),
      positionX: readBoundedNumber(source.positionX, 50, 0, 100),
      positionY: readBoundedNumber(source.positionY, 50, 0, 100),
      scale: readBoundedNumber(source.scale, 1, 0.5, 2.5),
      overlayOpacity: source.overlayOpacity == null ? null : readBoundedNumber(source.overlayOpacity, fallback.theme.backgroundOverlayOpacity, 0, 1)
    };
  }

  return fallback;
}

function readLegacyPage(pages: Record<string, SitePageBackground> | undefined, key: SitePageKey) {
  if (!pages) {
    return undefined;
  }

  if (key === "problems") {
    return pages["problems-list"] ?? pages["problem-detail"];
  }

  if (key === "admin-problems" || key === "admin-challenges") {
    return pages.admin;
  }

  if (key === "file-task") {
    return pages.challenges;
  }

  return undefined;
}

function readNumber(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function readBoundedNumber(value: unknown, fallback: number, min: number, max: number) {
  const number = readNumber(value, fallback);
  return number >= min && number <= max ? number : fallback;
}

function readOptionalBoundedNumber(value: unknown, min: number, max: number) {
  return typeof value === "number" && Number.isFinite(value) && value >= min && value <= max ? value : null;
}

function readThemeAsset(value: unknown): ThemeAssetReference | null {
  if (!value || typeof value !== "object") return null;
  const asset = value as { assetId?: unknown; url?: unknown };
  if (typeof asset.assetId !== "string" || !/^[0-9a-f]{32}\.(png|jpg|webp)$/.test(asset.assetId)) return null;
  const url = `/theme-assets/${asset.assetId}`;
  return asset.url === url ? { assetId: asset.assetId, url } : null;
}

function readThemeIconSlot(value: unknown): SiteThemeIconSlot | null {
  if (!value || typeof value !== "object") return null;
  const slot = value as Record<string, unknown>;
  return {
    enabled: Boolean(slot.enabled),
    asset: readThemeAsset(slot.asset),
    opacity: readOptionalBoundedNumber(slot.opacity, 0, 1),
    scale: readOptionalBoundedNumber(slot.scale, 0.5, 2),
    offsetX: readOptionalBoundedNumber(slot.offsetX, -64, 64),
    offsetY: readOptionalBoundedNumber(slot.offsetY, -64, 64)
  };
}

function readThemeDecorationSlot(value: unknown): SiteThemeDecorationSlot | null {
  const slot = readThemeIconSlot(value);
  if (!slot || !value || typeof value !== "object") return null;
  const source = value as Record<string, unknown>;
  return {
    ...slot,
    alignment: readDecorationAlignment(source.alignment),
    corner: readDecorationCorner(source.corner)
  };
}

function readDecorationAlignment(value: unknown): ThemeDecorationAlignment | null {
  return value === "start" || value === "center" || value === "end" ? value : null;
}

function readDecorationCorner(value: unknown): ThemeDecorationCorner | null {
  return value === "top-left" || value === "top-right" || value === "bottom-left" || value === "bottom-right" ? value : null;
}

function readBackgroundSizeMode(value: unknown): ThemeBackgroundSizeMode | null {
  return value === "cover" || value === "contain" || value === "auto" ? value : null;
}

function readBackgroundRepeat(value: unknown): ThemeBackgroundRepeat | null {
  return value === "no-repeat" || value === "repeat" || value === "repeat-x" || value === "repeat-y" ? value : null;
}

function readBackgroundAttachment(value: unknown): ThemeBackgroundAttachment | null {
  return value === "scroll" || value === "fixed" ? value : null;
}

function readColor(value: unknown, fallback: string) {
  return typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value) ? value.toUpperCase() : fallback;
}

function readFontPreset(value: unknown): SiteFontPreset {
  return value === "readable" || value === "mono" ? value : "system";
}
