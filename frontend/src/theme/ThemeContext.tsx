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
  type SitePageBackground,
  type SitePageKey
} from "../api/siteSettingsApi";

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
  const effectiveBackground = currentTheme === "mystic-background"
    ? resolveEffectiveBackground(userAppearance, siteAppearance, activePageKey)
    : null;
  const activeBackground = effectiveBackground;
  const backgroundUrl = resolveSiteAssetUrl(effectiveBackground?.imageUrl);
  const contentStyle = currentTheme === "mystic-background"
    ? {
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
      } as CSSProperties
    : undefined;

  const value = useMemo<ThemeContextValue>(() => ({
    currentTheme,
    setTheme,
    toggleTheme,
    siteAppearance,
    userAppearance,
    activePageKey,
    activeBackground,
    effectiveBackground,
    reloadSiteAppearance,
    reloadUserAppearance,
    updateUserAppearanceLocal: setUserAppearance
  }), [currentTheme, siteAppearance, userAppearance, activePageKey, activeBackground, effectiveBackground]);

  return (
    <ThemeContext.Provider value={value}>
      {backgroundUrl && effectiveBackground && (
        <div
          className="site-theme-background"
          style={{
            backgroundImage: `url("${backgroundUrl}")`,
            backgroundPosition: `${effectiveBackground.positionX}% ${effectiveBackground.positionY}%`,
            backgroundSize: `${effectiveBackground.scale * 100}% auto`
          }}
        >
          <div style={{ background: `rgba(0, 0, 0, ${effectiveBackground.overlayOpacity})` }} />
        </div>
      )}
      <AppBackground pathname={location.pathname} hasCustomWallpaper={Boolean(backgroundUrl && effectiveBackground)} />
      <div className="site-theme-content" style={contentStyle}>{children}</div>
    </ThemeContext.Provider>
  );
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
