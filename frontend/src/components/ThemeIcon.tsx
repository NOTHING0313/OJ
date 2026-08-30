import { type CSSProperties, type ReactNode } from "react";
import { resolveSiteAssetUrl } from "../api/siteSettingsApi";
import { useTheme } from "../theme/ThemeContext";
import { type ThemeIconSlot } from "../theme/themeSlots";

export function ThemeIcon({ slot, fallback = null, className }: { slot: ThemeIconSlot; fallback?: ReactNode; className?: string }) {
  const { siteAppearance, availableThemeAssetUrls } = useTheme();
  const assignment = siteAppearance.icons[slot];
  const url = resolveSiteAssetUrl(assignment?.asset?.url);
  if (!assignment?.enabled || !url || !availableThemeAssetUrls.has(url)) {
    return <>{fallback}</>;
  }

  const style = {
    opacity: assignment.opacity ?? 1,
    transform: `translate(${assignment.offsetX ?? 0}px, ${assignment.offsetY ?? 0}px) scale(${assignment.scale ?? 1})`
  } as CSSProperties;

  return (
    <span className={`theme-icon-slot${className ? ` ${className}` : ""}`} aria-hidden="true">
      <img src={url} alt="" style={style} />
    </span>
  );
}
