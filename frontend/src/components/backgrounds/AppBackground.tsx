import { DarkVeil } from "./DarkVeil";
import { DotField } from "./DotField";
import "./backgrounds.css";

export type AppBackgroundMode = "ambient" | "auth" | "leaderboard" | "challenge" | "data" | "profile" | "quiet" | "wallpaper";

interface AppBackgroundProps {
  pathname: string;
  hasCustomWallpaper: boolean;
  contained?: boolean;
}

export function AppBackground({ pathname, hasCustomWallpaper, contained = false }: AppBackgroundProps) {
  const mode = hasCustomWallpaper ? "wallpaper" : resolveBackgroundMode(pathname);
  const showDots = mode === "leaderboard" || mode === "challenge";

  return (
    <div className={`oj-app-background oj-app-background--${mode}${contained ? " is-contained" : ""}`} aria-hidden="true">
      <div className="oj-bg-base" />
      {mode !== "wallpaper" && (
        <>
          <div className="oj-bg-orb oj-bg-orb-primary" />
          <div className="oj-bg-orb oj-bg-orb-secondary" />
          <div className="oj-bg-orb oj-bg-orb-lower" />
        </>
      )}
      {mode === "auth" && (
        <div className="oj-bg-effect oj-bg-effect-darkveil">
          <DarkVeil speed={0.34} intensity={0.96} resolutionScale={0.72} />
        </div>
      )}
      {showDots && (
        <div className="oj-bg-effect oj-bg-effect-dots">
          <DotField
            dotRadius={mode === "leaderboard" ? 1.55 : 1.35}
            dotSpacing={mode === "leaderboard" ? 17 : 20}
            topSpacingMultiplier={mode === "leaderboard" ? 2.8 : 2.55}
            cursorRadius={mode === "leaderboard" ? 330 : 280}
            bulgeStrength={mode === "leaderboard" ? 26 : 19}
            idleAmplitude={mode === "leaderboard" ? 1.8 : 1.25}
            idleSpeed={mode === "leaderboard" ? 0.46 : 0.36}
            gradientFrom={mode === "leaderboard" ? "rgba(157, 127, 255, 0.92)" : "rgba(118, 102, 245, 0.70)"}
            gradientTo={mode === "leaderboard" ? "rgba(67, 161, 255, 0.72)" : "rgba(61, 137, 226, 0.50)"}
            glowColor={mode === "leaderboard" ? "rgba(125, 103, 255, 0.28)" : "rgba(105, 95, 239, 0.18)"}
          />
        </div>
      )}
      {mode === "data" && <div className="oj-bg-beams" />}
      {mode === "profile" && <div className="oj-bg-rings" />}
      <div className="oj-bg-grid" />
      <div className="oj-bg-noise" />
      <div className="oj-bg-vignette" />
    </div>
  );
}

function resolveBackgroundMode(pathname: string): AppBackgroundMode {
  if (pathname === "/login" || pathname === "/register" || pathname === "/forgot-password") {
    return "auth";
  }

  if (pathname.startsWith("/leaderboards") || /\/challenges\/[^/]+\/leaderboard\/?$/.test(pathname)) {
    return "leaderboard";
  }

  if (pathname.startsWith("/profile") || pathname.startsWith("/account/settings")) {
    return "profile";
  }

  if (/^\/challenges\/[^/]+\/admin(?:\/.*)?$/.test(pathname)) {
    return "data";
  }

  if (/^\/problems\/[^/]+\/?$/.test(pathname)) {
    return "quiet";
  }

  if (/^\/challenges\/[^/]+(?:\/.*)?$/.test(pathname)) {
    return "quiet";
  }

  if (pathname === "/challenges" || pathname === "/challenges/") {
    return "challenge";
  }

  if (pathname === "/problems" || pathname === "/problems/" || pathname.startsWith("/submissions") || pathname.startsWith("/admin")) {
    return "data";
  }

  return "ambient";
}
