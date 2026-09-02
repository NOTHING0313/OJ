import { type MouseEvent, type ReactNode } from "react";
import { NavLink } from "react-router-dom";
import { isRoot } from "../auth/AuthContext";
import { ThemeQuickSwitch } from "./ThemeQuickSwitch";

interface AppHeaderViewProps {
  role?: number;
  isAuthenticated: boolean;
  hasPublicLeaderboard: boolean;
  userName?: string;
  avatarUrl?: string | null;
  onLogout: () => void;
  renderIcon: (slot: "problem" | "challenge" | "leaderboard" | "team" | "submission" | "help" | "profile" | "reward" | "season") => ReactNode;
  interactive?: boolean;
  activePath?: string;
}

export function AppHeaderView({ role, isAuthenticated, hasPublicLeaderboard, userName, avatarUrl, onLogout, renderIcon, interactive = true, activePath }: AppHeaderViewProps) {
  const canManage = role === 2 || role === 3;
  const avatarInitial = (userName ?? "U").slice(0, 1).toUpperCase();

  return (
    <header className="topbar">
      <NavLink className="brand" to="/challenges" onClick={interactive ? undefined : preventNavigation}>
        <img src="/brand/unrealstudio-logo.png" alt="UNREALSTUDIO" />
        <span>虚幻工作室网上答题平台</span>
      </NavLink>
      <nav className="nav-links" aria-label="Main navigation">
        <NavLink className={activePath?.startsWith("/problems") ? "active" : undefined} to="/problems" onClick={interactive ? undefined : preventNavigation}>{renderIcon("problem")}题目</NavLink>
        <NavLink className={activePath === "/challenges" ? "active" : undefined} to="/challenges" onClick={interactive ? undefined : preventNavigation}>{renderIcon("challenge")}挑战</NavLink>
        {(canManage || hasPublicLeaderboard) && <NavLink className={activePath === "/leaderboards" ? "active" : undefined} to="/leaderboards" onClick={interactive ? undefined : preventNavigation}>{renderIcon("leaderboard")}榜单</NavLink>}
        {isAuthenticated && <NavLink className={activePath === "/teams" ? "active" : undefined} to="/teams" onClick={interactive ? undefined : preventNavigation}>{renderIcon("team")}战队</NavLink>}
        {isAuthenticated && <NavLink className={activePath === "/submissions/my" ? "active" : undefined} to="/submissions/my" onClick={interactive ? undefined : preventNavigation}>{renderIcon("submission")}我的提交</NavLink>}
        {isAuthenticated && <NavLink className={activePath === "/help" ? "active" : undefined} to="/help" onClick={interactive ? undefined : preventNavigation}>{renderIcon("help")}帮助</NavLink>}
        {isAuthenticated && <NavLink className={activePath === "/profile/me" ? "active" : undefined} to="/profile/me" onClick={interactive ? undefined : preventNavigation}>{renderIcon("profile")}个人中心</NavLink>}
        {canManage && (
          <details className="management-menu">
            <summary onClick={interactive ? undefined : preventNavigation}>管理</summary>
            <div className="management-menu-panel">
              <strong>内容管理</strong>
              <NavLink to="/admin/problems" onClick={interactive ? undefined : preventNavigation}>题目管理</NavLink>
              <NavLink to="/admin/challenges" onClick={interactive ? undefined : preventNavigation}>挑战管理</NavLink>
              <strong>{renderIcon("reward")}竞赛管理</strong>
              <NavLink to="/admin/leaderboard-seasons" onClick={interactive ? undefined : preventNavigation}>{renderIcon("season")}榜单管理</NavLink>
              <NavLink to="/admin/teams" onClick={interactive ? undefined : preventNavigation}>战队管理</NavLink>
              {isRoot(role) && <strong>系统管理</strong>}
              {isRoot(role) && <NavLink to="/admin/submissions" onClick={interactive ? undefined : preventNavigation}>提交管理</NavLink>}
              {isRoot(role) && <NavLink to="/admin/users" onClick={interactive ? undefined : preventNavigation}>用户管理</NavLink>}
              {isRoot(role) && <NavLink to="/admin/site-settings" onClick={interactive ? undefined : preventNavigation}>站点设置</NavLink>}
              {isRoot(role) && <NavLink to="/admin/security-audit" onClick={interactive ? undefined : preventNavigation}>安全审计</NavLink>}
            </div>
          </details>
        )}
      </nav>
      <div className="user-area">
        <ThemeQuickSwitch interactive={interactive} />
        {userName ? (
          <>
            <NavLink className="topbar-avatar-link" to="/account/settings" title="账号设置" onClick={interactive ? undefined : preventNavigation}>
              {avatarUrl ? (
                <img className="topbar-avatar" src={avatarUrl} alt={userName} />
              ) : (
                <span className="topbar-avatar fallback">{avatarInitial}</span>
              )}
            </NavLink>
            <NavLink className="topbar-user-name" to="/account/settings" onClick={interactive ? undefined : preventNavigation}>{userName}</NavLink>
            <button type="button" onClick={interactive ? onLogout : undefined}>退出登录</button>
          </>
        ) : (
          <NavLink to="/login" onClick={interactive ? undefined : preventNavigation}>登录</NavLink>
        )}
      </div>
    </header>
  );
}

function preventNavigation(event: MouseEvent) {
  event.preventDefault();
}
