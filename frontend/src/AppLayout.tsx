import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { canManageContent, isRoot, useAuth } from "./auth/AuthContext";
import { SiteFooter } from "./components/SiteFooter";
import { ThemeQuickSwitch } from "./components/ThemeQuickSwitch";

export function AppLayout() {
  const navigate = useNavigate();
  const { currentUser, isAuthenticated, logout } = useAuth();
  const role = currentUser?.role;

  function handleLogout() {
    logout();
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
          <NavLink to="/problems">题目</NavLink>
          <NavLink to="/challenges">挑战</NavLink>
          <NavLink to="/leaderboards">榜单</NavLink>
          {isAuthenticated && <NavLink to="/profile/me">个人中心</NavLink>}
          {isAuthenticated && <NavLink to="/teams">战队</NavLink>}
          {isAuthenticated && <NavLink to="/submissions/my">我的提交</NavLink>}
          {canManageContent(role) && <NavLink to="/admin/problems">题目管理</NavLink>}
          {canManageContent(role) && <NavLink to="/admin/challenges">挑战管理</NavLink>}
          {canManageContent(role) && <NavLink to="/admin/leaderboard-seasons">赛季榜</NavLink>}
          {canManageContent(role) && <NavLink to="/admin/teams">战队管理</NavLink>}
          {isRoot(role) && <NavLink to="/admin/submissions">提交管理</NavLink>}
          {isRoot(role) && <NavLink to="/admin/users">用户管理</NavLink>}
          {isRoot(role) && <NavLink to="/admin/site-settings">站点设置</NavLink>}
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
              <button type="button" onClick={handleLogout}>
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
