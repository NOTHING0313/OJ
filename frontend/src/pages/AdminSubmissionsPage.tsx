import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  querySubmissions,
  type JudgeLanguage,
  type JudgeStatus,
  type SubmissionQueryItem
} from "../api/submissionsApi";
import {
  LanguageOptions,
  Pagination,
  StatusOptions,
  SubmissionTable,
  parseLanguage,
  parseStatus
} from "./MySubmissionsPage";

const pageSize = 20;

export function AdminSubmissionsPage() {
  const [searchParams] = useSearchParams();
  const problemId = searchParams.get("problemId") ?? undefined;
  const [items, setItems] = useState<SubmissionQueryItem[]>([]);
  const [userKeyword, setUserKeyword] = useState("");
  const [problemKeyword, setProblemKeyword] = useState("");
  const [status, setStatus] = useState<JudgeStatus | "">("");
  const [language, setLanguage] = useState<JudgeLanguage | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void loadSubmissions();
    }, 180);

    return () => window.clearTimeout(handle);
  }, [problemId, userKeyword, problemKeyword, status, language, from, to, page]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);

  async function loadSubmissions() {
    try {
      setIsLoading(true);
      const result = await querySubmissions({
        problemId,
        userKeyword,
        problemKeyword,
        status,
        language,
        from: toIsoString(from),
        to: toIsoString(to),
        page,
        pageSize
      });
      setItems(result.items);
      setTotalCount(result.totalCount);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "提交管理列表加载失败");
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
          <p className="eyebrow">ROOT SUBMISSIONS</p>
          <h1>提交管理</h1>
          <p>Root 可查看全站提交，并按用户、题目、状态、语言和时间筛选。</p>
        </div>
      </div>

      {error && <div className="alert error">{error}</div>}

      <div className="admin-filter-bar">
        <label>
          用户
          <input placeholder="用户名关键字" value={userKeyword} onChange={(event) => resetFilters(() => setUserKeyword(event.target.value))} />
        </label>
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
        <label>
          开始时间
          <input type="datetime-local" value={from} onChange={(event) => resetFilters(() => setFrom(event.target.value))} />
        </label>
        <label>
          结束时间
          <input type="datetime-local" value={to} onChange={(event) => resetFilters(() => setTo(event.target.value))} />
        </label>
      </div>

      {isLoading ? (
        <div className="state-line">正在加载提交管理列表...</div>
      ) : items.length === 0 ? (
        <div className="empty-state">暂无匹配的提交记录</div>
      ) : (
        <SubmissionTable items={items} showUser />
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

function toIsoString(value: string) {
  if (!value) {
    return undefined;
  }

  return new Date(value).toISOString();
}
