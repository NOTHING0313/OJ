import { useEffect, useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { getCurrentSeasonPublicSummary } from "./api/leaderboardsApi";
import { canManageContent, isRoot, useAuth } from "./auth/AuthContext";
import { SiteFooter } from "./components/SiteFooter";
import { ThemeIcon } from "./components/ThemeIcon";
import { ThemeQuickSwitch } from "./components/ThemeQuickSwitch";

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

  const avatarInitial = (currentUser?.userName ?? "U").slice(0, 1).toUpperCase();

  return (
    <div className="app-shell">
      <header className="topbar">
        <NavLink className="brand" to="/challenges">
          <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
          <span>虚幻工作室网上答题平台</span>
        </NavLink>
        <nav className="nav-links" aria-label="Main navigation">
          <NavLink to="/problems"><ThemeIcon slot="problem" />题目</NavLink>
          <NavLink to="/challenges"><ThemeIcon slot="challenge" />挑战</NavLink>
          {(canManageContent(role) || hasPublicLeaderboard) && <NavLink to="/leaderboards"><ThemeIcon slot="leaderboard" />榜单</NavLink>}
          {isAuthenticated && <NavLink to="/teams"><ThemeIcon slot="team" />战队</NavLink>}
          {isAuthenticated && <NavLink to="/submissions/my"><ThemeIcon slot="submission" />我的提交</NavLink>}
          {isAuthenticated && <NavLink to="/help"><ThemeIcon slot="help" />帮助</NavLink>}
          {isAuthenticated && <NavLink to="/profile/me"><ThemeIcon slot="profile" />个人中心</NavLink>}
          {canManageContent(role) && (
            <details className="management-menu">
              <summary>管理</summary>
              <div className="management-menu-panel">
                <strong>内容管理</strong>
                <NavLink to="/admin/problems">题目管理</NavLink>
                <NavLink to="/admin/challenges">挑战管理</NavLink>
                <strong><ThemeIcon slot="reward" />竞赛管理</strong>
                <NavLink to="/admin/leaderboard-seasons"><ThemeIcon slot="season" />榜单管理</NavLink>
                <NavLink to="/admin/teams">战队管理</NavLink>
                {isRoot(role) && <strong>系统管理</strong>}
                {isRoot(role) && <NavLink to="/admin/submissions">提交管理</NavLink>}
                {isRoot(role) && <NavLink to="/admin/users">用户管理</NavLink>}
                {isRoot(role) && <NavLink to="/admin/site-settings">站点设置</NavLink>}
                {isRoot(role) && <NavLink to="/admin/security-audit">安全审计</NavLink>}
              </div>
            </details>
          )}
        </nav>
        <div className="user-area">
          <ThemeQuickSwitch />
          {currentUser ? (
            <>
              <NavLink className="topbar-avatar-link" to="/account/settings" title="账号设置">
                {currentUser.avatarUrl ? (
                  <img className="topbar-avatar" src={currentUser.avatarUrl} alt={currentUser.userName} />
                ) : (
                  <span className="topbar-avatar fallback">{avatarInitial}</span>
                )}
              </NavLink>
              <NavLink className="topbar-user-name" to="/account/settings">{currentUser.userName}</NavLink>
              <button type="button" onClick={() => void handleLogout()}>
                退出登录
              </button>
            </>
          ) : (
            <NavLink to="/login">登录</NavLink>
          )}
        </div>
      </header>
      <main className="page-container">
        <Outlet />
      </main>
      <SiteFooter />
    </div>
  );
}
