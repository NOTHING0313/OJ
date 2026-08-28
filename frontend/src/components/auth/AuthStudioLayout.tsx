import { type ReactNode, useEffect, useState } from "react";
import {
  getCurrentSeasonPublicSummary,
  type LeaderboardSeasonPublicSummary
} from "../../api/leaderboardsApi";
import { AuthParticleField } from "./AuthParticleField";

interface AuthStudioLayoutProps {
  title: string;
  children: ReactNode;
}

interface SeasonPresentation {
  label: string;
  targetAt: string;
  tone: "scheduled" | "active" | "public";
}

const SeasonRefreshIntervalMs = 45_000;

export function AuthStudioLayout({ title, children }: AuthStudioLayoutProps) {
  const [season, setSeason] = useState<LeaderboardSeasonPublicSummary | null>(null);
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    let isActive = true;

    async function refreshSeason() {
      try {
        const response = await getCurrentSeasonPublicSummary();
        if (isActive) setSeason(response.season);
      } catch {
        if (isActive) setSeason(null);
      }
    }

    void refreshSeason();
    const refreshTimer = window.setInterval(() => void refreshSeason(), SeasonRefreshIntervalMs);
    return () => {
      isActive = false;
      window.clearInterval(refreshTimer);
    };
  }, []);

  useEffect(() => {
    const clockTimer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(clockTimer);
  }, []);

  const presentation = getSeasonPresentation(season);

  return (
    <main className="auth-studio-page">
      <div className="auth-studio-glow" aria-hidden="true" />
      <AuthParticleField />
      <div className="auth-studio-shell">
        <div className="auth-studio-brand">
          <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
          <strong>UNREAL STUDIO</strong>
        </div>
        <div className="auth-studio-atmosphere" aria-hidden="true" />

        <section className="auth-studio-auth" aria-labelledby="auth-studio-title">
          <div className="auth-studio-card">
            <header className="auth-studio-card-header">
              <h1 id="auth-studio-title">{title}</h1>
            </header>
            {children}
            {season && presentation && (
              <div className={`auth-studio-season auth-studio-season--${presentation.tone}`}>
                <span>{season.name}</span>
                <strong>{presentation.label} · {formatCountdown(presentation.targetAt, now)}</strong>
              </div>
            )}
          </div>
          <footer className="auth-studio-footer">© 2026 UNREAL STUDIO</footer>
        </section>
      </div>
    </main>
  );
}

function getSeasonPresentation(season: LeaderboardSeasonPublicSummary | null): SeasonPresentation | null {
  if (season?.status === 1) {
    return { label: "STARTS IN", targetAt: season.startAt, tone: "scheduled" };
  }
  if (season?.status === 2) {
    return { label: "ACTIVE", targetAt: season.freezeAt, tone: "active" };
  }
  if (season?.status === 4) {
    return { label: "RESULTS PUBLIC", targetAt: season.publicUntil, tone: "public" };
  }
  return null;
}

function formatCountdown(targetAt: string, now: number) {
  const totalSeconds = Math.max(0, Math.floor((new Date(targetAt).getTime() - now) / 1000));
  const days = Math.floor(totalSeconds / 86400);
  const hours = Math.floor((totalSeconds % 86400) / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return [days, hours, minutes, seconds].map((value) => String(value).padStart(2, "0")).join(" : ");
}
