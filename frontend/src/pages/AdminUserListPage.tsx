import { useEffect, useState } from "react";
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

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void refreshUsers();
    }, 200);

    return () => window.clearTimeout(handle);
  }, [keyword, roleFilter, blacklistFilter, page]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);

  async function refreshUsers() {
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
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "用户列表加载失败");
    } finally {
      setIsLoading(false);
    }
  }

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

  return (
    <section className="challenge-page admin-user-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">ROOT ADMIN</p>
          <h1>用户管理</h1>
          <p>查看账号角色、黑名单状态，并授权答题人成为出题人。</p>
        </div>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-filter-bar">
        <label>
          搜索
          <input
            placeholder="用户名或邮箱"
            value={keyword}
            onChange={(event) => resetFilters(() => setKeyword(event.target.value))}
          />
        </label>
        <label>
          角色
          <select value={roleFilter} onChange={(event) => resetFilters(() => setRoleFilter(parseRoleFilter(event.target.value)))}>
            <option value="all">全部</option>
            <option value={1}>答题人</option>
            <option value={2}>出题人</option>
            <option value={3}>Root</option>
          </select>
        </label>
        <label>
          状态
          <select value={blacklistFilter} onChange={(event) => resetFilters(() => setBlacklistFilter(event.target.value as BlacklistFilter))}>
            <option value="all">全部</option>
            <option value="active">正常</option>
            <option value="blacklisted">已拉黑</option>
          </select>
        </label>
      </div>

      {isLoading ? (
        <div className="state-line">正在加载用户列表...</div>
      ) : (
        <>
          <div className="table-wrap leaderboard-table-wrap">
            <table className="leaderboard-table">
              <thead>
                <tr>
                  <th>用户</th>
                  <th>邮箱</th>
                  <th>角色</th>
                  <th>拉黑状态</th>
                  <th>创建时间</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <div className="leaderboard-user">
                        {user.avatarUrl ? (
                          <img src={user.avatarUrl} alt={user.userName} />
                        ) : (
                          <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
                        )}
                        <span>{user.userName}</span>
                      </div>
                    </td>
                    <td>{user.email}</td>
                    <td>{roleNames[user.role]}</td>
                    <td>{user.isBlacklisted ? "已拉黑" : "正常"}</td>
                    <td>{formatDate(user.createdAt)}</td>
                    <td>
                      <div className="table-actions">
                        <Link className="button" to={`/admin/users/${user.id}/profile`}>
                          查看主页
                        </Link>
                        {user.role === 1 && (
                          <button className="button" disabled={operatingUserId === user.id} type="button" onClick={() => handlePromote(user)}>
                            提升为出题人
                          </button>
                        )}
                        {user.role === 2 && (
                          <button className="button" disabled={operatingUserId === user.id} type="button" onClick={() => handleDemote(user)}>
                            降级为答题人
                          </button>
                        )}
                        {user.role !== 3 && !user.isBlacklisted && (
                          <button className="button" disabled={operatingUserId === user.id} type="button" onClick={() => handleBlacklist(user)}>
                            拉黑
                          </button>
                        )}
                        {user.role !== 3 && user.isBlacklisted && (
                          <button className="button" disabled={operatingUserId === user.id} type="button" onClick={() => handleUnblacklist(user)}>
                            解除拉黑
                          </button>
                        )}
                        {user.role === 3 && <span className="muted">Root 账号不可操作</span>}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {users.length === 0 && <div className="empty-state">没有匹配的用户</div>}
        </>
      )}

      <Pagination
        page={currentPage}
        pageSize={pageSize}
        totalCount={totalCount}
        totalPages={totalPages}
        onPageChange={setPage}
      />
    </section>
  );
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
    <div className="pagination-row">
      <span>
        共 {totalCount} 条，每页 {pageSize} 条，第 {page} / {totalPages} 页
      </span>
      <div className="button-row">
        <button className="button" disabled={page <= 1} type="button" onClick={() => onPageChange(page - 1)}>
          上一页
        </button>
        <button className="button" disabled={page >= totalPages} type="button" onClick={() => onPageChange(page + 1)}>
          下一页
        </button>
      </div>
    </div>
  );
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
