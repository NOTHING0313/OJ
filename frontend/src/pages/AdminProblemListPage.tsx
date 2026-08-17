import { type MouseEvent, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { deleteProblem, getProblems, type ProblemListItemDto } from "../api/problemsApi";

const pageSize = 10;

type PublishFilter = "all" | "published" | "draft";

export function AdminProblemListPage() {
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [keyword, setKeyword] = useState("");
  const [publishFilter, setPublishFilter] = useState<PublishFilter>("all");
  const [page, setPage] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  useEffect(() => {
    let ignore = false;

    getProblems()
      .then((data) => {
        if (!ignore) {
          setProblems(data);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "题目列表加载失败");
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

  const filteredProblems = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();

    return problems.filter((problem) => {
      if (normalizedKeyword && !problem.title.toLowerCase().includes(normalizedKeyword)) {
        return false;
      }

      if (publishFilter === "published" && !problem.isPublished) {
        return false;
      }

      if (publishFilter === "draft" && problem.isPublished) {
        return false;
      }

      return true;
    });
  }, [keyword, problems, publishFilter]);

  const totalPages = Math.max(1, Math.ceil(filteredProblems.length / pageSize));
  const currentPage = Math.min(page, totalPages);
  const pagedProblems = filteredProblems.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  function resetFilters(update: () => void) {
    update();
    setPage(1);
  }

  async function handleDeleteClick(event: MouseEvent, problem: ProblemListItemDto) {
    event.preventDefault();
    event.stopPropagation();

    if (!window.confirm(`确定删除题目「${problem.title}」吗？`)) {
      return;
    }

    try {
      setDeletingId(problem.id);
      await deleteProblem(problem.id);
      setProblems((current) => current.filter((item) => item.id !== problem.id));
      setNotice("题目已删除。");
      setError(null);
    } catch (err) {
      setError(getDeleteProblemErrorMessage(err));
    } finally {
      setDeletingId(null);
    }
  }

  function stopActionPropagation(event: MouseEvent) {
    event.stopPropagation();
  }

  if (isLoading) {
    return <div className="state-line">正在加载题目管理列表...</div>;
  }

  return (
    <section className="challenge-page admin-challenge-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">PROBLEM ADMIN</p>
          <h1>题目管理</h1>
          <p>维护算法题、题面、限制和测试用例。</p>
        </div>
        <Link className="button primary" to="/admin/problems/new">
          创建题目
        </Link>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-filter-bar">
        <label>
          搜索
          <input placeholder="题目标题" value={keyword} onChange={(event) => resetFilters(() => setKeyword(event.target.value))} />
        </label>
        <label>
          发布状态
          <select value={publishFilter} onChange={(event) => resetFilters(() => setPublishFilter(event.target.value as PublishFilter))}>
            <option value="all">全部</option>
            <option value="published">已发布</option>
            <option value="draft">未发布</option>
          </select>
        </label>
      </div>

      {filteredProblems.length === 0 ? (
        <div className="empty-state">暂无匹配的题目</div>
      ) : (
        <div className="admin-challenge-list">
          {pagedProblems.map((problem) => (
            <article className="admin-challenge-card" key={problem.id}>
              <div>
                <span className="challenge-status">{problem.isPublished ? "已发布" : "草稿"}</span>
                <h2>{problem.title}</h2>
                <div className="challenge-time">
                  <span>创建：{formatDate(problem.createdAt)}</span>
                  <span>时间限制：{problem.timeLimitMs} ms</span>
                  <span>内存限制：{problem.memoryLimitMb} MB</span>
                </div>
              </div>
              <div className="admin-challenge-card-actions">
                <Link className="button" to={`/admin/problems/${problem.id}/edit`} onClick={stopActionPropagation}>
                  编辑
                </Link>
                <Link className="button" to={`/admin/problems/${problem.id}/test-cases`} onClick={stopActionPropagation}>
                  测试用例
                </Link>
                <Link className="button" to={`/problems/${problem.id}`} onClick={stopActionPropagation}>
                  查看题目
                </Link>
                <button className="button" disabled={deletingId === problem.id} type="button" onClick={(event) => handleDeleteClick(event, problem)}>
                  {deletingId === problem.id ? "删除中..." : "删除"}
                </button>
              </div>
            </article>
          ))}
        </div>
      )}

      <Pagination
        page={currentPage}
        pageSize={pageSize}
        totalCount={filteredProblems.length}
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

function getDeleteProblemErrorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : "";

  if (message.includes("挑战任务引用")) {
    return "该题目已被挑战任务引用，请先移除相关挑战任务后再删除。";
  }

  return "删除失败，请稍后重试。";
}
