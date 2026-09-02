import { useEffect, useState } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import { getCurrentSeasonPublicSummary } from "./api/leaderboardsApi";
import { useAuth } from "./auth/AuthContext";
import { AppHeaderView } from "./components/AppHeaderView";
import { SiteFooter } from "./components/SiteFooter";
import { ThemeIcon } from "./components/ThemeIcon";

export function AppLayout() {
  const navigate = useNavigate();
  const { currentUser, isAuthenticated, logout } = useAuth();
  const role = currentUser?.role;
  const [hasPublicLeaderboard, setHasPublicLeaderboard] = useState(false);

  useEffect(() => {
    let ignore = false;
    getCurrentSeasonPublicSummary()
      .then((result) => { if (!ignore) setHasPublicLeaderboard((result.season?.boards.length ?? 0) > 0); })
      .catch(() => { if (!ignore) setHasPublicLeaderboard(false); });
    return () => { ignore = true; };
  }, []);

  async function handleLogout() {
    await logout();
    navigate("/login");
  }

  return (
    <div className="app-shell">
      <AppHeaderView role={role} isAuthenticated={isAuthenticated} hasPublicLeaderboard={hasPublicLeaderboard} userName={currentUser?.userName} avatarUrl={currentUser?.avatarUrl} onLogout={() => void handleLogout()} renderIcon={(slot) => <ThemeIcon slot={slot} />} />
      <main className="page-container">
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  );
}
