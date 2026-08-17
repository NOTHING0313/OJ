import { request } from "./httpClient";
import { normalizeUploadedImagePath, resolveSiteAssetUrl } from "../utils/uploadedImageUrl";

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

export interface SiteAppearanceTheme {
  backgroundEnabled: boolean;
  backgroundOverlayOpacity: number;
  panelOpacity: number;
  panelBlur: number;
}

export interface SitePageBackground {
  enabled: boolean;
  imageUrl: string | null;
  positionX: number;
  positionY: number;
  scale: number;
}

export interface SiteAppearance {
  theme: SiteAppearanceTheme;
  pages: Record<string, SitePageBackground>;
}

export type UpdateSiteAppearanceRequest = SiteAppearance;

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

export function createDefaultPageBackground(): SitePageBackground {
  return {
    enabled: false,
    imageUrl: null,
    positionX: 50,
    positionY: 50,
    scale: 1
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
      panelBlur: 12
    },
    pages
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
    backgroundOverlayOpacity: readNumber(value.theme?.backgroundOverlayOpacity, 0.65),
    panelOpacity: readNumber(value.theme?.panelOpacity, 0.72),
    panelBlur: readNumber(value.theme?.panelBlur, 12)
  };

  for (const { key } of sitePageOptions) {
    const source = value.pages?.[key] ?? readLegacyPage(value.pages, key) ?? {};
    fallback.pages[key] = {
      enabled: Boolean(source.enabled),
      imageUrl: normalizeUploadedImagePath(source.imageUrl),
      positionX: readNumber(source.positionX, 50),
      positionY: readNumber(source.positionY, 50),
      scale: readNumber(source.scale, 1)
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
