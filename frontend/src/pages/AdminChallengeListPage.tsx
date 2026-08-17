import { type MouseEvent, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { deleteChallenge, getChallenges, type ChallengeListItemDto } from "../api/challengesApi";
import { useAuth } from "../auth/AuthContext";

const pageSize = 10;

type PublishFilter = "all" | "published" | "draft";
type TimeFilter = "all" | "upcoming" | "open" | "ended";

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

  function resetFilters(update: () => void) {
    update();
    setPage(1);
  }

  function stopActionPropagation(event: MouseEvent) {
    event.stopPropagation();
  }

  async function handleDeleteClick(event: MouseEvent, challenge: ChallengeListItemDto) {
    event.preventDefault();
    event.stopPropagation();

    if (!window.confirm(`确定删除挑战「${challenge.title}」吗？`)) {
      return;
    }

    try {
      setDeletingId(challenge.id);
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

  if (isLoading) {
    return <div className="state-line">正在加载挑战管理列表...</div>;
  }

  return (
    <section className="challenge-page admin-challenge-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">CHALLENGE ADMIN</p>
          <h1>挑战管理</h1>
          <p>维护大题目、棋盘任务、开放时间和发布状态。</p>
        </div>
        <Link className="button primary" to="/admin/challenges/new">
          创建挑战
        </Link>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-filter-bar">
        <label>
          搜索
          <input placeholder="挑战标题" value={keyword} onChange={(event) => resetFilters(() => setKeyword(event.target.value))} />
        </label>
        <label>
          发布状态
          <select value={publishFilter} onChange={(event) => resetFilters(() => setPublishFilter(event.target.value as PublishFilter))}>
            <option value="all">全部</option>
            <option value="published">已发布</option>
            <option value="draft">未发布</option>
          </select>
        </label>
        <label>
          时间状态
          <select value={timeFilter} onChange={(event) => resetFilters(() => setTimeFilter(event.target.value as TimeFilter))}>
            <option value="all">全部</option>
            <option value="upcoming">未开始</option>
            <option value="open">进行中</option>
            <option value="ended">已结束</option>
          </select>
        </label>
      </div>

      {filteredChallenges.length === 0 ? (
        <div className="empty-state">暂无匹配的挑战</div>
      ) : (
        <div className="admin-challenge-list">
          {pagedChallenges.map((challenge) => (
            <article className="admin-challenge-card" key={challenge.id}>
              <div>
                <span className="challenge-status">{challenge.isPublished ? "已发布" : "草稿"}</span>
                <h2>{challenge.title}</h2>
                <p>{challenge.description ? challenge.description.slice(0, 120) : "暂无描述"}</p>
                <div className="challenge-time">
                  <span>创建：{formatDate(challenge.createdAt)}</span>
                  <span>开始：{formatDate(challenge.startAt)}</span>
                  <span>截止：{formatDate(challenge.endAt)}</span>
                </div>
              </div>
              <div className="admin-challenge-card-actions">
                <strong>{challenge.totalTaskCount === 0 ? "暂无任务" : `${challenge.completedTaskCount} / ${challenge.totalTaskCount}`}</strong>
                <Link className="button" to={`/admin/challenges/${challenge.id}/edit`} onClick={stopActionPropagation}>
                  编辑
                </Link>
                <Link className="button" to={`/admin/challenges/${challenge.id}/tasks/new`} onClick={stopActionPropagation}>
                  新建任务
                </Link>
                <Link className="button" to={`/challenges/${challenge.id}/admin`} onClick={stopActionPropagation}>
                  管理统计
                </Link>
                <Link className="button" to={`/challenges/${challenge.id}`} onClick={stopActionPropagation}>
                  查看棋盘
                </Link>
                <button className="button" disabled={deletingId === challenge.id} type="button" onClick={(event) => handleDeleteClick(event, challenge)}>
                  {deletingId === challenge.id ? "删除中..." : "删除"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}

      <Pagination
        page={currentPage}
        pageSize={pageSize}
        totalCount={filteredChallenges.length}
        totalPages={totalPages}
        onPageChange={setPage}
      />
    </section>
  );
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
