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
  const [openMenuProblemId, setOpenMenuProblemId] = useState<string | null>(null);

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

  useEffect(() => {
    function closeMenu(event: globalThis.MouseEvent) {
      if (event.target instanceof Element && !event.target.closest(".management-actions")) {
        setOpenMenuProblemId(null);
      }
    }

    document.addEventListener("mousedown", closeMenu);
    return () => document.removeEventListener("mousedown", closeMenu);
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
  const filtersAreDefault = keyword.length === 0 && publishFilter === "all";

  function resetFilters(update: () => void) {
    update();
    setPage(1);
    setOpenMenuProblemId(null);
  }

  function resetAllFilters() {
    setKeyword("");
    setPublishFilter("all");
    setPage(1);
    setOpenMenuProblemId(null);
  }

  async function handleDeleteClick(event: MouseEvent, problem: ProblemListItemDto) {
    event.preventDefault();
    event.stopPropagation();

    if (!window.confirm(`确定删除题目「${problem.title}」吗？`)) {
      return;
    }

    try {
      setDeletingId(problem.id);
      setOpenMenuProblemId(null);
      setNotice(null);
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

  return (
    <section className="challenge-page management-v2-page admin-problem-v2-page">
      <div className="leaderboard-header management-header">
        <div>
          <p className="eyebrow">PROBLEM ADMIN</p>
          <h1>题目管理</h1>
          <p>维护算法题、题面、资源限制与测试用例。</p>
        </div>
        <div className="management-header-actions">
          <span className="management-total">共 {problems.length} 道题目</span>
          <Link className="button primary" to="/admin/problems/new">创建题目</Link>
        </div>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="management-toolbar problem-management-toolbar">
        <label className="management-search-field">
          <span>搜索</span>
          <input placeholder="搜索题目标题" value={keyword} onChange={(event) => resetFilters(() => setKeyword(event.target.value))} />
        </label>
        <label>
          <span>发布状态</span>
          <select value={publishFilter} onChange={(event) => resetFilters(() => setPublishFilter(event.target.value as PublishFilter))}>
            <option value="all">状态：全部</option>
            <option value="published">状态：已发布</option>
            <option value="draft">状态：未发布</option>
          </select>
        </label>
        <button className="button management-toolbar-reset" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置</button>
      </div>

      {isLoading ? (
        <div className="management-state-panel">正在加载题目...</div>
      ) : filteredProblems.length === 0 ? (
        <div className="management-state-panel management-empty-state">
          <strong>未找到匹配题目</strong>
          <p>调整搜索条件或重置筛选后重试。</p>
          <button className="button" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置筛选</button>
        </div>
      ) : (
        <div className="table-wrap management-table-wrap">
          <table className="management-table problem-management-table">
            <thead>
              <tr>
                <th>题目</th>
                <th>判题模式</th>
                <th>状态</th>
                <th>资源限制</th>
                <th>创建时间</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {pagedProblems.map((problem, index) => {
                const createdAt = formatDateTime(problem.createdAt);
                const isMenuOpen = openMenuProblemId === problem.id;
                const isDeleting = deletingId === problem.id;

                return (
                  <tr key={problem.id}>
                    <td>
                      <div className="management-identity-copy">
                        <Link className="management-title-link" to={`/problems/${problem.id}`} title={problem.title}>{problem.title}</Link>
                        <span>{problem.judgeMode === 2 ? "函数题" : "标准输入输出题"}</span>
                      </div>
                    </td>
                    <td><ProblemModeBadge judgeMode={problem.judgeMode} /></td>
                    <td><PublishBadge isPublished={problem.isPublished} /></td>
                    <td>
                      <div className="management-limit-stack">
                        <span>{problem.timeLimitMs} ms</span>
                        <span>{problem.memoryLimitMb} MB</span>
                      </div>
                    </td>
                    <td>
                      <time className="management-date-time" dateTime={problem.createdAt}>
                        <strong>{createdAt.date}</strong>
                        <span>{createdAt.time}</span>
                      </time>
                    </td>
                    <td>
                      <div className="management-row-actions">
                        <Link className="button management-view-link" to={`/problems/${problem.id}`}>查看</Link>
                        <div className="management-actions">
                          <button
                            className="button management-more-button"
                            type="button"
                            aria-haspopup="menu"
                            aria-expanded={isMenuOpen}
                            aria-label={`管理题目 ${problem.title}`}
                            disabled={isDeleting}
                            onClick={() => setOpenMenuProblemId(isMenuOpen ? null : problem.id)}
                          >
                            …
                          </button>
                          {isMenuOpen && (
                            <div className={index >= pagedProblems.length - 2 ? "management-action-menu management-action-menu-align-up" : "management-action-menu"} role="menu">
                              <Link to={`/admin/problems/${problem.id}/edit`} role="menuitem" onClick={() => setOpenMenuProblemId(null)}>编辑题目</Link>
                              <Link to={`/admin/problems/${problem.id}/test-cases`} role="menuitem" onClick={() => setOpenMenuProblemId(null)}>管理测试用例</Link>
                              <button className="management-danger-action" type="button" role="menuitem" disabled={isDeleting} onClick={(event) => void handleDeleteClick(event, problem)}>
                                {isDeleting ? "删除中..." : "删除题目"}
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

      <Pagination page={currentPage} pageSize={pageSize} totalCount={filteredProblems.length} totalPages={totalPages} onPageChange={setPage} />
    </section>
  );
}

function ProblemModeBadge({ judgeMode }: { judgeMode: ProblemListItemDto["judgeMode"] }) {
  return <span className={`management-badge management-mode-${judgeMode === 2 ? "function" : "standard"}`}>{judgeMode === 2 ? "函数题" : "标准输入输出"}</span>;
}

function PublishBadge({ isPublished }: { isPublished: boolean }) {
  return <span className={`management-badge management-status-${isPublished ? "published" : "draft"}`}>{isPublished ? "已发布" : "未发布"}</span>;
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

function getDeleteProblemErrorMessage(error: unknown) {
  const message = error instanceof Error ? error.message : "";

  if (message.includes("挑战任务引用")) {
    return "该题目已被挑战任务引用，请先移除相关挑战任务后再删除。";
  }

  return "删除失败，请稍后重试。";
}
