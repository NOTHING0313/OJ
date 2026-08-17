import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { formatDate } from "../utils/labels";

export function ProblemListPage() {
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
    <section className="page-section">
      <div className="page-header">
        <div>
          <h1>题目列表</h1>
          <p>查看当前可用题目，进入详情后提交代码。</p>
        </div>
        <Link className="button primary" to="/admin/problems/new">
          创建题目
        </Link>
      </div>

      {isLoading && <div className="state-line">加载中...</div>}
      {error && <div className="alert error">{error}</div>}

      {!isLoading && !error && (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Title</th>
                <th>Time</th>
                <th>Memory</th>
                <th>Published</th>
                <th>Created</th>
              </tr>
            </thead>
            <tbody>
              {problems.map((problem) => (
                <tr key={problem.id}>
                  <td>
                    <Link className="table-link" to={`/problems/${problem.id}`}>
                      {problem.title}
                    </Link>
                  </td>
                  <td>{problem.timeLimitMs} ms</td>
                  <td>{problem.memoryLimitMb} MB</td>
                  <td>{problem.isPublished ? "Yes" : "No"}</td>
                  <td>{formatDate(problem.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {problems.length === 0 && <div className="empty-state">暂无题目</div>}
        </div>
      )}
    </section>
  );
}
