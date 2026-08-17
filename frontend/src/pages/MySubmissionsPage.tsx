import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  querySubmissions,
  type JudgeLanguage,
  type JudgeStatus,
  type SubmissionQueryItem
} from "../api/submissionsApi";
import { formatDate, languageLabel, statusLabel } from "../utils/labels";

const pageSize = 20;

export function MySubmissionsPage() {
  const [searchParams] = useSearchParams();
  const problemId = searchParams.get("problemId") ?? undefined;
  const [items, setItems] = useState<SubmissionQueryItem[]>([]);
  const [problemKeyword, setProblemKeyword] = useState("");
  const [status, setStatus] = useState<JudgeStatus | "">("");
  const [language, setLanguage] = useState<JudgeLanguage | "">("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void loadSubmissions();
    }, 180);

    return () => window.clearTimeout(handle);
  }, [problemId, problemKeyword, status, language, page]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);

  async function loadSubmissions() {
    try {
      setIsLoading(true);
      const result = await querySubmissions({
        mine: true,
        problemId,
        problemKeyword,
        status,
        language,
        page,
        pageSize
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "提交记录加载失败");
    } finally {
      setIsLoading(false);
    }
  }

  function resetFilters(update: () => void) {
    update();
    setPage(1);
  }

  return (
    <section className="challenge-page submissions-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">SUBMISSIONS</p>
          <h1>我的提交</h1>
          <p>查看自己的判题记录、筛选状态和语言，并进入提交详情。</p>
        </div>
      </div>

      {error && <div className="alert error">{error}</div>}

      <div className="admin-filter-bar">
        <label>
          题目
          <input
            disabled={Boolean(problemId)}
            placeholder={problemId ? "已按题目筛选" : "题目关键字"}
            value={problemKeyword}
            onChange={(event) => resetFilters(() => setProblemKeyword(event.target.value))}
          />
        </label>
        <label>
          状态
          <select value={status} onChange={(event) => resetFilters(() => setStatus(parseStatus(event.target.value)))}>
            <option value="">全部</option>
            <StatusOptions />
          </select>
        </label>
        <label>
          语言
          <select value={language} onChange={(event) => resetFilters(() => setLanguage(parseLanguage(event.target.value)))}>
            <option value="">全部</option>
            <LanguageOptions />
          </select>
        </label>
      </div>

      {isLoading ? (
        <div className="state-line">正在加载提交记录...</div>
      ) : items.length === 0 ? (
        <div className="empty-state">暂无提交记录</div>
      ) : (
        <SubmissionTable items={items} showUser={false} />
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

export function SubmissionTable({ items, showUser }: { items: SubmissionQueryItem[]; showUser: boolean }) {
  return (
    <div className="table-wrap leaderboard-table-wrap">
      <table className="leaderboard-table">
        <thead>
          <tr>
            <th>提交时间</th>
            <th>题目</th>
            {showUser && <th>用户</th>}
            <th>语言</th>
            <th>状态</th>
            <th>耗时</th>
            <th>内存</th>
            <th>完成时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id}>
              <td>{formatDate(item.createdAt)}</td>
              <td>
                <Link to={`/problems/${item.problemId}`}>{item.problemTitle}</Link>
              </td>
              {showUser && <td>{item.userName}</td>}
              <td>{languageLabel(item.language)}</td>
              <td>
                <span className={getStatusClassName(item.status)}>{statusLabel(item.status)}</span>
              </td>
              <td>{item.timeUsedMs ?? "-"} ms</td>
              <td>{item.memoryUsedKb ?? "-"} KB</td>
              <td>{formatDate(item.finishedAt)}</td>
              <td>
                <Link className="button" to={`/submissions/${item.id}`}>
                  查看详情
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function getStatusClassName(status: JudgeStatus) {
  return status === 3 ? "status-accepted" : undefined;
}

export function LanguageOptions() {
  return (
    <>
      <option value={1}>C++17</option>
      <option value={2}>C11</option>
      <option value={3}>C#</option>
    </>
  );
}

export function StatusOptions() {
  return (
    <>
      <option value={1}>等待中</option>
      <option value={2}>判题中</option>
      <option value={3}>通过</option>
      <option value={4}>答案错误</option>
      <option value={5}>超出时间限制</option>
      <option value={6}>超出内存限制</option>
      <option value={7}>运行错误</option>
      <option value={8}>编译错误</option>
      <option value={9}>系统错误</option>
    </>
  );
}

export function parseLanguage(value: string): JudgeLanguage | "" {
  return value ? (Number(value) as JudgeLanguage) : "";
}

export function parseStatus(value: string): JudgeStatus | "" {
  return value ? (Number(value) as JudgeStatus) : "";
}

export function Pagination({
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
