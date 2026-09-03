import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  blacklistUser,
  demoteToAnswerer,
  getUsers,
  promoteToProblemSetter,
  unblacklistUser,
  type AdminUserDto,
  type UserRole
} from "../api/usersApi";

const pageSize = 20;

const roleNames: Record<UserRole, string> = {
  1: "答题人",
  2: "出题人",
  3: "Root"
};

type RoleFilter = "all" | UserRole;
type BlacklistFilter = "all" | "active" | "blacklisted";

export function AdminUserListPage() {
  const [users, setUsers] = useState<AdminUserDto[]>([]);
  const [keyword, setKeyword] = useState("");
  const [roleFilter, setRoleFilter] = useState<RoleFilter>("all");
  const [blacklistFilter, setBlacklistFilter] = useState<BlacklistFilter>("all");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [operatingUserId, setOperatingUserId] = useState<string | null>(null);
  const [openMenuUserId, setOpenMenuUserId] = useState<string | null>(null);

  const refreshUsers = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await getUsers({
        keyword,
        role: roleFilter,
        isBlacklisted: toBlacklistQuery(blacklistFilter),
        page,
        pageSize
      });
      setUsers(data.items);
      setTotalCount(data.totalCount);
      setOpenMenuUserId(null);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "用户列表加载失败");
    } finally {
      setIsLoading(false);
    }
  }, [keyword, roleFilter, blacklistFilter, page]);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void refreshUsers();
    }, 200);

    return () => window.clearTimeout(handle);
  }, [refreshUsers]);

  useEffect(() => {
    if (!openMenuUserId) {
      return;
    }

    function handlePointerDown(event: PointerEvent) {
      if (event.target instanceof Element && !event.target.closest(".admin-user-actions")) {
        setOpenMenuUserId(null);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setOpenMenuUserId(null);
      }
    }

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [openMenuUserId]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);
  const filtersAreDefault = !keyword && roleFilter === "all" && blacklistFilter === "all";

  async function handlePromote(user: AdminUserDto) {
    if (!window.confirm(`确定将「${user.userName}」提升为出题人吗？`)) {
      return;
    }

    await runUserAction(user.id, "用户已提升为出题人。", () => promoteToProblemSetter(user.id));
  }

  async function handleDemote(user: AdminUserDto) {
    if (!window.confirm(`确定将「${user.userName}」降级为答题人吗？`)) {
      return;
    }

    await runUserAction(user.id, "用户已降级为答题人。", () => demoteToAnswerer(user.id));
  }

  async function handleBlacklist(user: AdminUserDto) {
    if (!window.confirm(`确定拉黑「${user.userName}」吗？`)) {
      return;
    }

    await runUserAction(user.id, "用户已拉黑。", () => blacklistUser(user.id));
  }

  async function handleUnblacklist(user: AdminUserDto) {
    if (!window.confirm(`确定解除「${user.userName}」的黑名单吗？`)) {
      return;
    }

    await runUserAction(user.id, "黑名单已解除。", () => unblacklistUser(user.id));
  }

  async function runUserAction(userId: string, successMessage: string, action: () => Promise<unknown>) {
    try {
      setOperatingUserId(userId);
      setOpenMenuUserId(null);
      setError(null);
      setNotice(null);
      await action();
      await refreshUsers();
      setNotice(successMessage);
    } catch (err) {
      setError(err instanceof Error ? err.message : "操作失败");
    } finally {
      setOperatingUserId(null);
    }
  }

  function resetFilters(next: () => void) {
    next();
    setPage(1);
  }

  function resetAllFilters() {
    setKeyword("");
    setRoleFilter("all");
    setBlacklistFilter("all");
    setPage(1);
    setOpenMenuUserId(null);
  }

  return (
    <section className="challenge-page admin-user-page admin-user-v2-page">
      <div className="leaderboard-header admin-user-header">
        <div>
          <p className="eyebrow">ROOT ADMIN</p>
          <h1>用户管理</h1>
          <p>管理用户角色、账号状态与访问权限。</p>
        </div>
        <span className="admin-user-total">共 {totalCount} 名用户</span>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-user-toolbar">
        <label className="admin-user-search-field">
          <span>搜索</span>
          <input
            placeholder="搜索用户名或邮箱"
            value={keyword}
            onChange={(event) => resetFilters(() => setKeyword(event.target.value))}
          />
        </label>
        <label>
          <span>角色</span>
          <select value={roleFilter} onChange={(event) => resetFilters(() => setRoleFilter(parseRoleFilter(event.target.value)))}>
            <option value="all">角色：全部</option>
            <option value={1}>角色：答题人</option>
            <option value={2}>角色：出题人</option>
            <option value={3}>角色：Root</option>
          </select>
        </label>
        <label>
          <span>状态</span>
          <select value={blacklistFilter} onChange={(event) => resetFilters(() => setBlacklistFilter(event.target.value as BlacklistFilter))}>
            <option value="all">状态：全部</option>
            <option value="active">状态：正常</option>
            <option value="blacklisted">状态：已拉黑</option>
          </select>
        </label>
        <button className="button admin-user-toolbar-reset" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>
          重置
        </button>
      </div>

      {isLoading ? (
        <div className="admin-user-state-panel">正在加载用户...</div>
      ) : users.length === 0 ? (
        <div className="admin-user-state-panel admin-user-empty-state">
          <strong>未找到匹配用户</strong>
          <p>调整搜索条件或重置筛选后重试。</p>
          <button className="button" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置筛选</button>
        </div>
      ) : (
        <div className="table-wrap admin-user-table-wrap">
          <table className="admin-user-table">
            <thead>
              <tr>
                <th>用户</th>
                <th>角色</th>
                <th>状态</th>
                <th>注册时间</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {users.map((user, index) => {
                const createdAt = formatUserCreatedAt(user.createdAt);
                const isOperating = operatingUserId === user.id;
                const isMenuOpen = openMenuUserId === user.id;

                return (
                  <tr className={user.role === 3 ? "admin-user-root-row" : undefined} key={user.id}>
                    <td>
                      <div className="admin-user-identity">
                        {user.avatarUrl ? (
                          <img className="admin-user-avatar" src={user.avatarUrl} alt={user.userName} />
                        ) : (
                          <span className="admin-user-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
                        )}
                        <div className="admin-user-identity-copy">
                          <strong title={user.userName}>{user.userName}</strong>
                          <span className="admin-user-email" title={user.email}>{user.email}</span>
                        </div>
                      </div>
                    </td>
                    <td><RoleBadge role={user.role} /></td>
                    <td><UserStatusBadge isBlacklisted={user.isBlacklisted} /></td>
                    <td>
                      <time className="admin-user-created-at" dateTime={user.createdAt}>
                        <strong>{createdAt.date}</strong>
                        <span>{createdAt.time}</span>
                      </time>
                    </td>
                    <td>
                      <div className="admin-user-row-actions">
                        <Link className="button admin-user-view-link" to={`/admin/users/${user.id}/profile`}>查看</Link>
                        {user.role === 3 ? (
                          <span className="admin-user-root-note">Root 账号不可管理</span>
                        ) : (
                          <div className="admin-user-actions">
                            <button
                              className="button admin-user-more-button"
                              type="button"
                              aria-haspopup="menu"
                              aria-expanded={isMenuOpen}
                              aria-label={`管理 ${user.userName}`}
                              disabled={isOperating}
                              onClick={() => setOpenMenuUserId(isMenuOpen ? null : user.id)}
                            >
                              …
                            </button>
                            {isMenuOpen && (
                              <div className={index >= users.length - 2 ? "admin-user-action-menu admin-user-action-menu-align-up" : "admin-user-action-menu"} role="menu">
                                {user.role === 1 && (
                                  <button type="button" role="menuitem" disabled={isOperating} onClick={() => void handlePromote(user)}>提升为出题人</button>
                                )}
                                {user.role === 2 && (
                                  <button type="button" role="menuitem" disabled={isOperating} onClick={() => void handleDemote(user)}>降级为答题人</button>
                                )}
                                {user.isBlacklisted ? (
                                  <button type="button" role="menuitem" disabled={isOperating} onClick={() => void handleUnblacklist(user)}>解除拉黑</button>
                                ) : (
                                  <button className="admin-user-danger-action" type="button" role="menuitem" disabled={isOperating} onClick={() => void handleBlacklist(user)}>拉黑用户</button>
                                )}
                              </div>
                            )}
                          </div>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <Pagination page={currentPage} pageSize={pageSize} totalCount={totalCount} totalPages={totalPages} onPageChange={setPage} />
    </section>
  );
}

function RoleBadge({ role }: { role: UserRole }) {
  const tone = role === 3 ? "root" : role === 2 ? "problem-setter" : "answerer";
  return <span className={`admin-user-badge admin-user-role-${tone}`}>{roleNames[role]}</span>;
}

function UserStatusBadge({ isBlacklisted }: { isBlacklisted: boolean }) {
  return <span className={`admin-user-badge admin-user-status-${isBlacklisted ? "blacklisted" : "active"}`}>{isBlacklisted ? "已拉黑" : "正常"}</span>;
}

function parseRoleFilter(value: string): RoleFilter {
  if (value === "1" || value === "2" || value === "3") {
    return Number(value) as UserRole;
  }

  return "all";
}

function toBlacklistQuery(value: BlacklistFilter) {
  if (value === "active") {
    return false;
  }

  if (value === "blacklisted") {
    return true;
  }

  return "all";
}

function Pagination({
  page,
  pageSize,
  totalCount,
  totalPages,
  onPageChange
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <div className="pagination-row admin-user-pagination">
      <span>共 {totalCount} 条 · 每页 {pageSize} 条 · 第 {page} / {totalPages} 页</span>
      <div className="button-row">
        <button className="button" disabled={page <= 1} type="button" onClick={() => onPageChange(page - 1)}>上一页</button>
        <button className="button" disabled={page >= totalPages} type="button" onClick={() => onPageChange(page + 1)}>下一页</button>
      </div>
    </div>
  );
}

function formatUserCreatedAt(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return { date: "-", time: "-" };
  }

  const parts = new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23"
  }).formatToParts(date);
  const readPart = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value ?? "";

  return {
    date: `${readPart("year")}-${readPart("month")}-${readPart("day")}`,
    time: `${readPart("hour")}:${readPart("minute")}`
  };
}
