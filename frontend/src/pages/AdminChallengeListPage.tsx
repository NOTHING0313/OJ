import { type MouseEvent, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { deleteChallenge, getChallenges, type ChallengeListItemDto } from "../api/challengesApi";
import { useAuth } from "../auth/AuthContext";

const pageSize = 10;

type PublishFilter = "all" | "published" | "draft";
type TimeFilter = "all" | "upcoming" | "open" | "ended";
type ChallengeTimeState = Exclude<TimeFilter, "all">;

export function AdminChallengeListPage() {
  const { currentUser } = useAuth();
  const [challenges, setChallenges] = useState<ChallengeListItemDto[]>([]);
  const [keyword, setKeyword] = useState("");
  const [publishFilter, setPublishFilter] = useState<PublishFilter>("all");
  const [timeFilter, setTimeFilter] = useState<TimeFilter>("all");
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [openMenuChallengeId, setOpenMenuChallengeId] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    getChallenges()
      .then((data) => {
        if (!ignore) {
          setChallenges(data);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "挑战列表加载失败");
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, []);

  useEffect(() => {
    function closeMenu(event: globalThis.MouseEvent) {
      if (event.target instanceof Element && !event.target.closest(".management-actions")) {
        setOpenMenuChallengeId(null);
      }
    }

    document.addEventListener("mousedown", closeMenu);
    return () => document.removeEventListener("mousedown", closeMenu);
  }, []);

  const manageableChallenges = useMemo(() => {
    if (currentUser?.role === 3) {
      return challenges;
    }

    return challenges.filter((challenge) => challenge.canManage);
  }, [challenges, currentUser?.role]);

  const filteredChallenges = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    const now = Date.now();

    return manageableChallenges.filter((challenge) => {
      if (normalizedKeyword && !challenge.title.toLowerCase().includes(normalizedKeyword)) {
        return false;
      }

      if (publishFilter === "published" && !challenge.isPublished) {
        return false;
      }

      if (publishFilter === "draft" && challenge.isPublished) {
        return false;
      }

      const start = new Date(challenge.startAt).getTime();
      const end = new Date(challenge.endAt).getTime();

      if (timeFilter === "upcoming" && start <= now) {
        return false;
      }

      if (timeFilter === "open" && (start > now || end < now)) {
        return false;
      }

      if (timeFilter === "ended" && end >= now) {
        return false;
      }

      return true;
    });
  }, [keyword, manageableChallenges, publishFilter, timeFilter]);

  const totalPages = Math.max(1, Math.ceil(filteredChallenges.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const pagedChallenges = filteredChallenges.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const filtersAreDefault = keyword.length === 0 && publishFilter === "all" && timeFilter === "all";

  function resetFilters(update: () => void) {
    update();
    setPage(1);
    setOpenMenuChallengeId(null);
  }

  function resetAllFilters() {
    setKeyword("");
    setPublishFilter("all");
    setTimeFilter("all");
    setPage(1);
    setOpenMenuChallengeId(null);
  }

  async function handleDeleteClick(event: MouseEvent, challenge: ChallengeListItemDto) {
    event.preventDefault();
    event.stopPropagation();

    if (!window.confirm(`确定删除挑战「${challenge.title}」吗？`)) {
      return;
    }

    try {
      setDeletingId(challenge.id);
      setOpenMenuChallengeId(null);
      setNotice(null);
      await deleteChallenge(challenge.id);
      setChallenges((current) => current.filter((item) => item.id !== challenge.id));
      setNotice("挑战已删除。");
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "删除挑战失败");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <section className="challenge-page management-v2-page admin-challenge-v2-page">
      <div className="leaderboard-header management-header">
        <div>
          <p className="eyebrow">CHALLENGE ADMIN</p>
          <h1>挑战管理</h1>
          <p>维护挑战、棋盘任务、开放时间与发布状态。</p>
        </div>
        <div className="management-header-actions">
          <span className="management-total">共 {manageableChallenges.length} 个挑战</span>
          <Link className="button primary" to="/admin/challenges/new">创建挑战</Link>
        </div>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="management-toolbar challenge-management-toolbar">
        <label className="management-search-field">
          <span>搜索</span>
          <input placeholder="搜索挑战标题" value={keyword} onChange={(event) => resetFilters(() => setKeyword(event.target.value))} />
        </label>
        <label>
          <span>发布状态</span>
          <select value={publishFilter} onChange={(event) => resetFilters(() => setPublishFilter(event.target.value as PublishFilter))}>
            <option value="all">发布：全部</option>
            <option value="published">发布：已发布</option>
            <option value="draft">发布：未发布</option>
          </select>
        </label>
        <label>
          <span>时间状态</span>
          <select value={timeFilter} onChange={(event) => resetFilters(() => setTimeFilter(event.target.value as TimeFilter))}>
            <option value="all">时间：全部</option>
            <option value="upcoming">时间：未开始</option>
            <option value="open">时间：进行中</option>
            <option value="ended">时间：已结束</option>
          </select>
        </label>
        <button className="button management-toolbar-reset" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置</button>
      </div>

      {isLoading ? (
        <div className="management-state-panel">正在加载挑战...</div>
      ) : filteredChallenges.length === 0 ? (
        <div className="management-state-panel management-empty-state">
          <strong>未找到匹配挑战</strong>
          <p>调整搜索条件或重置筛选后重试。</p>
          <button className="button" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置筛选</button>
        </div>
      ) : (
        <div className="table-wrap management-table-wrap">
          <table className="management-table challenge-management-table">
            <thead>
              <tr>
                <th>挑战</th>
                <th>发布状态</th>
                <th>时间状态</th>
                <th>任务进度</th>
                <th>截止时间</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {pagedChallenges.map((challenge, index) => {
                const endAt = formatDateTime(challenge.endAt);
                const timeState = getChallengeTimeState(challenge);
                const isMenuOpen = openMenuChallengeId === challenge.id;
                const isDeleting = deletingId === challenge.id;
                const progress = challenge.totalTaskCount > 0 ? Math.min(100, Math.round((challenge.completedTaskCount / challenge.totalTaskCount) * 100)) : 0;

                return (
                  <tr key={challenge.id}>
                    <td>
                      <div className="management-identity-copy management-challenge-copy">
                        <Link className="management-title-link" to={`/challenges/${challenge.id}`} title={challenge.title}>{challenge.title}</Link>
                        <span title={challenge.description || "暂无描述"}>{truncateText(challenge.description || "暂无描述", 54)}</span>
                      </div>
                    </td>
                    <td><PublishBadge isPublished={challenge.isPublished} /></td>
                    <td><ChallengeTimeBadge state={timeState} /></td>
                    <td>
                      <div className="management-progress-cell">
                        <div className="management-progress-copy">
                          <strong>{challenge.totalTaskCount === 0 ? "暂无任务" : `${challenge.completedTaskCount} / ${challenge.totalTaskCount}`}</strong>
                          {challenge.totalTaskCount > 0 && <span>{progress}%</span>}
                        </div>
                        {challenge.totalTaskCount > 0 && (
                          <div className="management-progress-track" aria-label={`任务完成 ${progress}%`}>
                            <span style={{ width: `${progress}%` }} />
                          </div>
                        )}
                      </div>
                    </td>
                    <td>
                      <time className="management-date-time" dateTime={challenge.endAt}>
                        <strong>{endAt.date}</strong>
                        <span>{endAt.time}</span>
                      </time>
                    </td>
                    <td>
                      <div className="management-row-actions">
                        <Link className="button management-view-link" to={`/challenges/${challenge.id}`}>查看</Link>
                        <div className="management-actions">
                          <button
                            className="button management-more-button"
                            type="button"
                            aria-haspopup="menu"
                            aria-expanded={isMenuOpen}
                            aria-label={`管理挑战 ${challenge.title}`}
                            disabled={isDeleting}
                            onClick={() => setOpenMenuChallengeId(isMenuOpen ? null : challenge.id)}
                          >
                            …
                          </button>
                          {isMenuOpen && (
                            <div className={index >= pagedChallenges.length - 2 ? "management-action-menu management-action-menu-align-up" : "management-action-menu"} role="menu">
                              <Link to={`/admin/challenges/${challenge.id}/edit`} role="menuitem" onClick={() => setOpenMenuChallengeId(null)}>编辑挑战</Link>
                              <Link to={`/admin/challenges/${challenge.id}/tasks/new`} role="menuitem" onClick={() => setOpenMenuChallengeId(null)}>新建任务</Link>
                              <Link to={`/challenges/${challenge.id}/admin`} role="menuitem" onClick={() => setOpenMenuChallengeId(null)}>管理统计</Link>
                              <button className="management-danger-action" type="button" role="menuitem" disabled={isDeleting} onClick={(event) => void handleDeleteClick(event, challenge)}>
                                {isDeleting ? "删除中..." : "删除挑战"}
                              </button>
                            </div>
                          )}
                        </div>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <Pagination page={currentPage} pageSize={pageSize} totalCount={filteredChallenges.length} totalPages={totalPages} onPageChange={setPage} />
    </section>
  );
}

function PublishBadge({ isPublished }: { isPublished: boolean }) {
  return <span className={`management-badge management-status-${isPublished ? "published" : "draft"}`}>{isPublished ? "已发布" : "未发布"}</span>;
}

function ChallengeTimeBadge({ state }: { state: ChallengeTimeState }) {
  const labels: Record<ChallengeTimeState, string> = { upcoming: "未开始", open: "进行中", ended: "已结束" };
  return <span className={`management-badge management-time-${state}`}>{labels[state]}</span>;
}

function getChallengeTimeState(challenge: ChallengeListItemDto): ChallengeTimeState {
  const now = Date.now();
  const start = new Date(challenge.startAt).getTime();
  const end = new Date(challenge.endAt).getTime();

  if (start > now) {
    return "upcoming";
  }

  if (end < now) {
    return "ended";
  }

  return "open";
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
    <div className="pagination-row management-pagination">
      <span>共 {totalCount} 条 · 每页 {pageSize} 条 · 第 {page} / {totalPages} 页</span>
      <div className="button-row">
        <button className="button" disabled={page <= 1} type="button" onClick={() => onPageChange(page - 1)}>上一页</button>
        <button className="button" disabled={page >= totalPages} type="button" onClick={() => onPageChange(page + 1)}>下一页</button>
      </div>
    </div>
  );
}

function formatDateTime(value: string) {
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

function truncateText(value: string, maxLength: number) {
  const trimmed = value.trim();
  return trimmed.length <= maxLength ? trimmed : `${trimmed.slice(0, maxLength)}…`;
}
