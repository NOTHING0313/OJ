import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { queryProblems, type ProblemListItemDto } from "../api/problemsApi";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { Pagination } from "./MySubmissionsPage";
import { formatDate } from "../utils/labels";

export function ProblemListPage() {
  const { currentUser } = useAuth();
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [resultPage, setResultPage] = useState(1);
  const difficultyNames = ["未分级", "简单", "中等", "困难"];
  const canEdit = canManageContent(currentUser?.role);

  useEffect(() => {
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      setIsLoading(true);
      queryProblems(searchTerm, page, controller.signal).then(result => {
        if (controller.signal.aborted) return;
        setProblems(result.items);
        setTotalCount(result.totalCount);
        setResultPage(result.page);
        setError(null);
      }).catch((err: unknown) => {
        if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "加载题目失败");
      }).finally(() => { if (!controller.signal.aborted) setIsLoading(false); });
    }, 180);
    return () => { controller.abort(); window.clearTimeout(timer); };
  }, [searchTerm, page]);

  return (
    <section className="page-section problem-list-page ui-v2-page problem-list-v2-page">
      <div className="page-header ui-v2-page-header">
        <div>
          <h1>题目列表</h1>

        </div>
        {canEdit && (
          <Link className="button primary" to="/admin/problems/new">
            创建题目
          </Link>
        )}
      </div>

      {isLoading && <div className="state-line">加载中...</div>}
      {error && <div className="alert error">{error}</div>}

      <>
          <div className="problem-list-toolbar">
            <label className="problem-search">
              <svg className="problem-search-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" aria-hidden="true" focusable="false">
                <circle cx="10.5" cy="10.5" r="6.5" />
                <path d="m15.5 15.5 4.5 4.5" />
              </svg>
              <input
                type="search"
                value={searchTerm}
                onChange={(event) => { setSearchTerm(event.target.value); setPage(1); }}
                placeholder="搜索题目标题..."
                aria-label="搜索题目标题"
              />
            </label>
          </div>

          {!isLoading && !error && (problems.length > 0 ? (
            <div className="table-wrap problem-list-table-wrap">
              <table className="problem-list-table">
                <thead>
                  <tr>
                    <th>#</th>
                    <th>题目标题</th>
                    <th>时间限制</th>
                    <th>内存限制</th>
                    <th>公开状态</th>
                    <th>创建时间</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {problems.map((problem, index) => (
                    <tr key={problem.id}>
                      <td className="problem-list-index">{(resultPage - 1) * 20 + index + 1}</td>
                      <td>
                        <Link className="problem-list-title" data-difficulty={problem.difficulty || undefined} title={problem.difficulty ? `难度：${difficultyNames[problem.difficulty]}` : undefined} aria-label={problem.difficulty ? `${problem.title}，${difficultyNames[problem.difficulty]}` : undefined} to={`/problems/${problem.id}`}>
                          {problem.title}
                        </Link>
                      </td>
                      <td><span className="problem-meta-badge time">{problem.problemKind === 2 ? "—" : `${problem.timeLimitMs} ms`}</span></td>
                      <td><span className="problem-meta-badge memory">{problem.problemKind === 2 ? "—" : `${problem.memoryLimitMb} MB`}</span></td>
                      <td>
                        <span className={`problem-meta-badge ${problem.isPublished ? "published" : "unpublished"}`}>
                          {problem.isPublished ? "公开" : "未公开"}
                        </span>
                      </td>
                      <td className="problem-list-created">{formatDate(problem.createdAt)}</td>
                      <td>
                        <div className="table-actions problem-list-actions">
                          <Link to={`/problems/${problem.id}`}>查看</Link>
                          {canEdit && <Link to={`/admin/problems/${problem.id}/edit`}>编辑</Link>}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="empty-state problem-list-empty">
              {searchTerm ? "未找到匹配的题目" : "暂无题目"}
            </div>
          ))}
          {!error && <Pagination page={resultPage} pageSize={20} totalCount={totalCount} totalPages={Math.max(1, Math.ceil(totalCount / 20))} onPageChange={setPage} />}
      </>
    </section>
  );
}
