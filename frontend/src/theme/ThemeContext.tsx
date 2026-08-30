import { createContext, type CSSProperties, type ReactNode, useContext, useEffect, useMemo, useState } from "react";
import { useLocation } from "react-router-dom";
import {
  getMyAppearance,
  type UserAppearance
} from "../api/accountApi";
import { useAuth } from "../auth/AuthContext";
import { AppBackground } from "../components/backgrounds/AppBackground";
import {
  createDefaultSiteAppearance,
  getSiteAppearance,
  resolveSiteAssetUrl,
  type SiteAppearance,
  type SitePanelSkin,
  type SitePageBackground,
  type SitePageKey
} from "../api/siteSettingsApi";
import { themeDecorationSlotOptions } from "./themeSlots";

export type ThemeName = "default" | "mystic-background";

interface ThemeContextValue {
  currentTheme: ThemeName;
  setTheme: (theme: ThemeName) => void;
  toggleTheme: () => void;
  siteAppearance: SiteAppearance;
  userAppearance: UserAppearance | null;
  activePageKey: SitePageKey;
  activeBackground: SitePageBackground | null;
  effectiveBackground: EffectiveBackground | null;
  availableThemeAssetUrls: ReadonlySet<string>;
  reloadSiteAppearance: () => Promise<void>;
  reloadUserAppearance: () => Promise<void>;
  updateUserAppearanceLocal: (appearance: UserAppearance | null) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);
const ThemeStorageKey = "onlinejudge.theme";

type EffectiveBackground = SitePageBackground & {
  overlayOpacity: number;
  source: "user" | "site";
};

export function ThemeProvider({ children }: { children: ReactNode }) {
  const location = useLocation();
  const { currentUser, isAuthenticated } = useAuth();
  const [currentTheme, setCurrentTheme] = useState<ThemeName>(() => readStoredTheme());
  const [siteAppearance, setSiteAppearance] = useState<SiteAppearance>(() => createDefaultSiteAppearance());
  const [userAppearance, setUserAppearance] = useState<UserAppearance | null>(null);

  async function reloadSiteAppearance() {
    try {
      const appearance = await getSiteAppearance();
      setSiteAppearance(appearance);
    } catch {
      setSiteAppearance(createDefaultSiteAppearance());
    }
  }

  useEffect(() => {
    void reloadSiteAppearance();
  }, []);

  async function reloadUserAppearance() {
    if (!isAuthenticated || !currentUser) {
      setUserAppearance(null);
      return;
    }

    try {
      const appearance = await getMyAppearance();
      setUserAppearance(appearance);
    } catch {
      setUserAppearance(null);
    }
  }

  useEffect(() => {
    if (!isAuthenticated || !currentUser) {
      setUserAppearance(null);
      return;
    }

    setUserAppearance(null);
    void reloadUserAppearance();
  }, [isAuthenticated, currentUser?.id]);

  useEffect(() => {
    localStorage.setItem(ThemeStorageKey, currentTheme);
    document.body.classList.remove("theme-default", "theme-mystic-background");
    document.body.classList.add(`theme-${currentTheme}`);

    return () => {
      document.body.classList.remove("theme-default", "theme-mystic-background");
    };
  }, [currentTheme]);

  function setTheme(theme: ThemeName) {
    setCurrentTheme(theme);
  }

  function toggleTheme() {
    setCurrentTheme((theme) => (theme === "default" ? "mystic-background" : "default"));
  }

  const activePageKey = resolvePageThemeKey(location.pathname);
  const legacyBackground = currentTheme === "mystic-background"
    ? resolveEffectiveBackground(userAppearance, siteAppearance, activePageKey)
    : null;
  const genericBackgroundUrl = useAvailableAssetUrl(siteAppearance.background.enabled ? siteAppearance.background.asset?.url : null);
  const legacyBackgroundUrl = useAvailableAssetUrl(legacyBackground?.imageUrl);
  const panelBackgroundUrl = useAvailableAssetUrl(siteAppearance.panelSkin.enabled ? siteAppearance.panelSkin.backgroundTexture?.url : null);
  const panelHeaderUrl = useAvailableAssetUrl(siteAppearance.panelSkin.enabled ? siteAppearance.panelSkin.headerTexture?.url : null);
  const panelBorderUrl = useAvailableAssetUrl(siteAppearance.panelSkin.enabled ? siteAppearance.panelSkin.borderTexture?.url : null);
  const slotAssetUrls = useMemo(() => [
    ...Object.values(siteAppearance.icons),
    ...Object.values(siteAppearance.decorations)
  ].flatMap((slot) => slot?.enabled && slot.asset?.url ? [slot.asset.url] : []), [siteAppearance.icons, siteAppearance.decorations]);
  const availableThemeAssetUrls = useAvailableAssetUrls(slotAssetUrls);
  const usesGenericBackground = Boolean(genericBackgroundUrl && siteAppearance.background.enabled);
  const effectiveBackground = usesGenericBackground ? null : legacyBackground;
  const activeBackground = effectiveBackground;
  const backgroundUrl = usesGenericBackground ? genericBackgroundUrl : legacyBackgroundUrl;
  const contentStyle = {
    ...(currentTheme === "mystic-background" ? {
        "--site-panel-opacity": String(siteAppearance.theme.panelOpacity),
        "--site-panel-blur": `${siteAppearance.theme.panelBlur}px`,
        "--site-panel-color": siteAppearance.theme.panelColor,
        "--oj-panel-bg": hexToRgba(siteAppearance.theme.panelColor, siteAppearance.theme.panelOpacity),
        "--oj-input-bg": hexToRgba(siteAppearance.theme.panelColor, Math.min(siteAppearance.theme.panelOpacity + 0.08, 0.98)),
        "--oj-panel-border": `rgba(255, 255, 255, ${siteAppearance.theme.panelBorderOpacity})`,
        "--oj-text-primary": siteAppearance.theme.textPrimaryColor,
        "--oj-text-secondary": siteAppearance.theme.textSecondaryColor,
        "--oj-text-muted": siteAppearance.theme.textMutedColor,
        "--oj-accent": siteAppearance.theme.accentColor,
        "--oj-nav-bg": hexToRgba("#05080F", siteAppearance.theme.navOpacity),
        "--oj-nav-blur": `${siteAppearance.theme.navBlur}px`,
        "--oj-nav-text": siteAppearance.theme.navTextColor,
        "--oj-nav-active": siteAppearance.theme.navActiveColor,
        "--oj-font-family": resolveFontFamily(siteAppearance.theme.fontPreset)
      } : {}),
    ...buildPanelSkinStyle(siteAppearance.panelSkin, panelBackgroundUrl, panelHeaderUrl, panelBorderUrl),
    ...buildDecorationStyle(siteAppearance, availableThemeAssetUrls)
  } as CSSProperties;
  const panelSkinActive = siteAppearance.panelSkin.enabled && hasPanelSkinStyle(siteAppearance.panelSkin, panelBackgroundUrl, panelHeaderUrl, panelBorderUrl);
  const panelSkinClassName = buildPanelSkinClassName(siteAppearance.panelSkin, panelBackgroundUrl, panelHeaderUrl, panelBorderUrl);
  const decorationClassName = buildDecorationClassName(siteAppearance, availableThemeAssetUrls);

  const value = useMemo<ThemeContextValue>(() => ({
    currentTheme,
    setTheme,
    toggleTheme,
    siteAppearance,
    userAppearance,
    activePageKey,
    activeBackground,
    effectiveBackground,
    availableThemeAssetUrls,
    reloadSiteAppearance,
    reloadUserAppearance,
    updateUserAppearanceLocal: setUserAppearance
  }), [currentTheme, siteAppearance, userAppearance, activePageKey, activeBackground, effectiveBackground, availableThemeAssetUrls]);

  return (
    <ThemeContext.Provider value={value}>
      <AppBackground pathname={location.pathname} hasCustomWallpaper={Boolean(backgroundUrl && (usesGenericBackground || effectiveBackground))} />
      <div className={`site-theme-content${usesGenericBackground ? " theme-generic-background-active" : ""}${panelSkinActive ? ` ${panelSkinClassName}` : ""}${decorationClassName ? ` ${decorationClassName}` : ""}`} style={Object.keys(contentStyle).length > 0 ? contentStyle : undefined}>
        {backgroundUrl && (usesGenericBackground || effectiveBackground) && (
          <div className="site-theme-background">
            <div
              className="site-theme-background-image"
              style={usesGenericBackground
                ? buildGenericBackgroundStyle(siteAppearance)
                : {
                    backgroundImage: `url("${backgroundUrl}")`,
                    backgroundPosition: `${effectiveBackground!.positionX}% ${effectiveBackground!.positionY}%`,
                    backgroundSize: `${effectiveBackground!.scale * 100}% auto`
                  }}
            />
            <div
              className="site-theme-background-overlay"
              style={usesGenericBackground
                ? { background: hexToRgba(siteAppearance.background.overlayColor ?? "#000000", siteAppearance.background.overlayOpacity ?? 0) }
                : { background: `rgba(0, 0, 0, ${effectiveBackground!.overlayOpacity})` }}
            />
          </div>
        )}
        {children}
      </div>
    </ThemeContext.Provider>
  );
}

function useAvailableAssetUrl(url: string | null | undefined) {
  const resolved = resolveSiteAssetUrl(url);
  const [available, setAvailable] = useState<string | undefined>();

  useEffect(() => {
    if (!resolved) {
      setAvailable(undefined);
      return;
    }

    let active = true;
    const image = new Image();
    image.onload = () => { if (active) setAvailable(resolved); };
    image.onerror = () => { if (active) setAvailable(undefined); };
    image.src = resolved;
    return () => { active = false; };
  }, [resolved]);

  return available;
}

function useAvailableAssetUrls(urls: string[]) {
  const key = [...new Set(urls.map((url) => resolveSiteAssetUrl(url)).filter(Boolean))].sort().join("|");
  const [available, setAvailable] = useState<ReadonlySet<string>>(() => new Set());

  useEffect(() => {
    const resolvedUrls = key ? key.split("|") : [];
    if (resolvedUrls.length === 0) {
      setAvailable(new Set());
      return;
    }

    let active = true;
    const next = new Set<string>();
    const images = resolvedUrls.map((url) => {
      const image = new Image();
      image.onload = () => {
        if (!active) return;
        next.add(url);
        setAvailable(new Set(next));
      };
      image.src = url;
      return image;
    });

    setAvailable(new Set());
    return () => {
      active = false;
      for (const image of images) image.onload = null;
    };
  }, [key]);

  return available;
}

function buildGenericBackgroundStyle(appearance: SiteAppearance) {
  const background = appearance.background;
  const style: CSSProperties = {
    backgroundImage: `url("${resolveSiteAssetUrl(background.asset?.url)}")`
  };
  if (background.positionX != null || background.positionY != null) style.backgroundPosition = `${background.positionX ?? 50}% ${background.positionY ?? 50}%`;
  if (background.sizeMode) style.backgroundSize = background.sizeMode;
  if (background.repeat) style.backgroundRepeat = background.repeat;
  if (background.attachment) style.backgroundAttachment = background.attachment;
  if (background.blur != null || background.brightness != null) style.filter = `blur(${background.blur ?? 0}px) brightness(${background.brightness ?? 100}%)`;
  return style;
}

function buildPanelSkinStyle(panelSkin: SitePanelSkin, backgroundUrl?: string, headerUrl?: string, borderUrl?: string) {
  if (!panelSkin.enabled) return {};
  const style: Record<string, string> = {};
  const textureOpacity = panelSkin.textureOpacity ?? 0.15;
  if (backgroundUrl) style["--theme-panel-bg-layer"] = `linear-gradient(rgba(5, 6, 8, ${1 - textureOpacity}), rgba(5, 6, 8, ${1 - textureOpacity})), url("${backgroundUrl}")`;
  if (headerUrl) style["--theme-panel-header-layer"] = `linear-gradient(rgba(5, 6, 8, ${1 - textureOpacity}), rgba(5, 6, 8, ${1 - textureOpacity})), url("${headerUrl}")`;
  if (borderUrl) style["--theme-panel-border-image"] = `url("${borderUrl}")`;
  if (panelSkin.backgroundOpacity != null) style["--theme-panel-bg-color"] = `color-mix(in srgb, var(--oj-panel-bg) ${panelSkin.backgroundOpacity * 100}%, transparent)`;
  if (panelSkin.radius != null) style["--theme-panel-radius"] = `${panelSkin.radius}px`;
  if (panelSkin.shadowStrength != null) style["--theme-panel-shadow"] = `0 18px 48px rgba(0, 0, 0, ${panelSkin.shadowStrength})`;
  return style;
}

function hasPanelSkinStyle(panelSkin: SitePanelSkin, backgroundUrl?: string, headerUrl?: string, borderUrl?: string) {
  return Boolean(backgroundUrl || headerUrl || borderUrl
    || panelSkin.backgroundOpacity != null || panelSkin.radius != null || panelSkin.shadowStrength != null);
}

function buildPanelSkinClassName(panelSkin: SitePanelSkin, backgroundUrl?: string, headerUrl?: string, borderUrl?: string) {
  const classes = ["theme-panel-skin"];
  if (backgroundUrl) classes.push("theme-panel-bg-texture");
  if (headerUrl) classes.push("theme-panel-header-texture");
  if (borderUrl) classes.push("theme-panel-border-texture");
  if (panelSkin.backgroundOpacity != null) classes.push("theme-panel-bg-opacity");
  if (panelSkin.radius != null) classes.push("theme-panel-radius");
  if (panelSkin.shadowStrength != null) classes.push("theme-panel-shadow");
  return classes.join(" ");
}

function buildDecorationStyle(appearance: SiteAppearance, availableUrls: ReadonlySet<string>) {
  const style: Record<string, string> = {};
  for (const { key } of themeDecorationSlotOptions) {
    const slot = appearance.decorations[key];
    const url = resolveSiteAssetUrl(slot?.asset?.url);
    if (!slot?.enabled || !url || !availableUrls.has(url)) continue;
    const cssKey = toKebabCase(key);
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
  return style;
}

function buildDecorationClassName(appearance: SiteAppearance, availableUrls: ReadonlySet<string>) {
  const classes: string[] = [];
  for (const { key } of themeDecorationSlotOptions) {
    const slot = appearance.decorations[key];
    const url = resolveSiteAssetUrl(slot?.asset?.url);
    if (!slot?.enabled || !url || !availableUrls.has(url)) continue;
    classes.push(`theme-decoration-${toKebabCase(key)}`);
    if (key === "panelCorner") classes.push(`theme-decoration-panel-corner-${slot.corner ?? "top-right"}`);
  }
  return classes.join(" ");
}

function toKebabCase(value: string) {
  return value.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`);
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error("useTheme must be used within ThemeProvider.");
  }

  return context;
}

export function resolvePageThemeKey(pathname: string): SitePageKey {
  if (pathname === "/" || pathname.startsWith("/home")) {
    return "global";
  }

  if (/^\/challenges\/[^/]+\/tasks\/[^/]+\/answer\/?$/.test(pathname)) {
    return "file-task";
  }

  if (pathname.startsWith("/admin/problems")) {
    return "admin-problems";
  }

  if (pathname.startsWith("/admin/challenges")) {
    return "admin-challenges";
  }

  if (/^\/challenges\/[^/]+\/admin(\/.*)?$/.test(pathname)) {
    return "admin-challenges";
  }

  if (pathname.startsWith("/problems")) {
    return "problems";
  }

  if (pathname.startsWith("/profile")) {
    return "profile";
  }

  if (pathname.startsWith("/account/settings")) {
    return "account-settings";
  }

  if (pathname.startsWith("/submissions")) {
    return "submissions";
  }

  if (pathname.startsWith("/leaderboards")) {
    return "leaderboards";
  }

  if (pathname.startsWith("/challenges")) {
    return "challenges";
  }

  if (pathname.startsWith("/admin")) {
    return "admin-problems";
  }

  return "global";
}

function resolveActiveBackground(appearance: SiteAppearance, pageKey: SitePageKey) {
  const page = appearance.pages[pageKey];
  if (page?.enabled && page.imageUrl) {
    return page;
  }

  const global = appearance.pages.global;
  if (global?.enabled && global.imageUrl) {
    return global;
  }

  return null;
}

function resolveEffectiveBackground(userAppearance: UserAppearance | null, siteAppearance: SiteAppearance, pageKey: SitePageKey): EffectiveBackground | null {
  if (userAppearance?.backgroundEnabled && userAppearance.backgroundImageUrl) {
    return {
      enabled: true,
      imageUrl: userAppearance.backgroundImageUrl,
      positionX: userAppearance.positionX,
      positionY: userAppearance.positionY,
      scale: userAppearance.scale,
      overlayOpacity: userAppearance.overlayOpacity,
      source: "user"
    };
  }

  if (!siteAppearance.theme.backgroundEnabled) {
    return null;
  }

  const siteBackground = resolveActiveBackground(siteAppearance, pageKey);
  if (!siteBackground) {
    return null;
  }

  return {
    ...siteBackground,
    overlayOpacity: siteBackground.overlayOpacity ?? siteAppearance.theme.backgroundOverlayOpacity,
    source: "site"
  };
}

function hexToRgba(hex: string, opacity: number) {
  const normalized = /^#[0-9a-fA-F]{6}$/.test(hex) ? hex.slice(1) : "11141A";
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${opacity})`;
}

function resolveFontFamily(preset: SiteAppearance["theme"]["fontPreset"]) {
  if (preset === "readable") {
    return '"Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", system-ui, sans-serif';
  }

  if (preset === "mono") {
    return '"Cascadia Code", "JetBrains Mono", "SFMono-Regular", Consolas, monospace';
  }

  return 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif';
}

function readStoredTheme(): ThemeName {
  const stored = localStorage.getItem(ThemeStorageKey);
  return stored === "mystic-background" || stored === "root-config" ? "mystic-background" : "default";
}
