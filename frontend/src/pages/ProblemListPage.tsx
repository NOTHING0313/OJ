import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { formatDate } from "../utils/labels";

export function ProblemListPage() {
  const { currentUser } = useAuth();
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [searchTerm, setSearchTerm] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const filteredProblems = useMemo(() => {
    const normalizedSearchTerm = searchTerm.trim().toLocaleLowerCase();
    return normalizedSearchTerm
      ? problems.filter((problem) => problem.title.toLocaleLowerCase().includes(normalizedSearchTerm))
      : problems;
  }, [problems, searchTerm]);
  const canEdit = canManageContent(currentUser?.role);

  useEffect(() => {
    let isMounted = true;

    getProblems()
      .then((items) => {
        if (isMounted) {
          setProblems(items);
        }
      })
      .catch((err: unknown) => {
        if (isMounted) {
          setError(err instanceof Error ? err.message : "加载题目失败");
        }
      })
      .finally(() => {
        if (isMounted) {
          setIsLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  return (
    <section className="page-section problem-list-page ui-v2-page problem-list-v2-page">
      <div className="page-header ui-v2-page-header">
        <div>
          <p className="eyebrow">PROBLEMS</p>
          <h1>题目列表</h1>
          <p>查看当前可用题目，进入详情后提交代码。</p>
        </div>
        {canEdit && (
          <Link className="button primary" to="/admin/problems/new">
            创建题目
          </Link>
        )}
      </div>

      {isLoading && <div className="state-line">加载中...</div>}
      {error && <div className="alert error">{error}</div>}

      {!isLoading && !error && (
        <>
          <div className="problem-list-toolbar">
            <label className="problem-search">
              <span aria-hidden="true">🔍</span>
              <input
                type="search"
                value={searchTerm}
                onChange={(event) => setSearchTerm(event.target.value)}
                placeholder="搜索题目标题..."
                aria-label="搜索题目标题"
              />
            </label>
          </div>

          {filteredProblems.length > 0 ? (
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
                  {filteredProblems.map((problem, index) => (
                    <tr key={problem.id}>
                      <td className="problem-list-index">{index + 1}</td>
                      <td>
                        <Link className="problem-list-title" to={`/problems/${problem.id}`}>
                          {problem.title}
                        </Link>
                      </td>
                      <td><span className="problem-meta-badge time">{problem.timeLimitMs} ms</span></td>
                      <td><span className="problem-meta-badge memory">{problem.memoryLimitMb} MB</span></td>
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
              {problems.length === 0 ? "暂无题目" : "未找到匹配的题目"}
            </div>
          )}
        </>
      )}
    </section>
  );
}
