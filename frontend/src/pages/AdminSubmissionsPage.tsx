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
  const filtersAreDefault = !userKeyword && !problemKeyword && status === "" && language === "" && !from && !to;

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

  function resetAllFilters() {
    setUserKeyword("");
    setProblemKeyword("");
    setStatus("");
    setLanguage("");
    setFrom("");
    setTo("");
    setPage(1);
  }

  return (
    <section className="challenge-page submissions-page submission-v2-page admin-submissions-v2-page">
      <div className="leaderboard-header submission-header">
        <div>
          <p className="eyebrow">ROOT ADMIN</p>
          <h1>提交管理</h1>
          <p>查看全站提交记录，并按用户、题目、状态、语言和时间范围筛选。</p>
        </div>
        <span className="submission-total">共 {totalCount} 条提交</span>
      </div>

      {error && <div className="alert error">{error}</div>}

      <div className="submission-toolbar submission-toolbar-admin">
        <label className="submission-filter-user">
          <span>用户</span>
          <input placeholder="搜索用户名" value={userKeyword} onChange={(event) => resetFilters(() => setUserKeyword(event.target.value))} />
        </label>
        <label className="submission-filter-problem">
          <span>题目</span>
          <input
            disabled={Boolean(problemId)}
            placeholder={problemId ? "已按题目筛选" : "搜索题目标题"}
            value={problemKeyword}
            onChange={(event) => resetFilters(() => setProblemKeyword(event.target.value))}
          />
        </label>
        <label className="submission-filter-status">
          <span>状态</span>
          <select value={status} onChange={(event) => resetFilters(() => setStatus(parseStatus(event.target.value)))}>
            <option value="">状态：全部</option>
            <StatusOptions />
          </select>
        </label>
        <label className="submission-filter-language">
          <span>语言</span>
          <select value={language} onChange={(event) => resetFilters(() => setLanguage(parseLanguage(event.target.value)))}>
            <option value="">语言：全部</option>
            <LanguageOptions />
          </select>
        </label>
        <button className="button submission-toolbar-reset submission-filter-reset" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>
          重置
        </button>
        <label className="submission-filter-from">
          <span>开始时间</span>
          <input type="datetime-local" value={from} onChange={(event) => resetFilters(() => setFrom(event.target.value))} />
        </label>
        <label className="submission-filter-to">
          <span>结束时间</span>
          <input type="datetime-local" value={to} onChange={(event) => resetFilters(() => setTo(event.target.value))} />
        </label>
      </div>

      {isLoading ? (
        <div className="submission-state-panel">正在加载提交管理列表...</div>
      ) : items.length === 0 ? (
        <div className="submission-state-panel submission-empty-state">
          <strong>未找到匹配的提交</strong>
          <p>{problemId ? "当前题目下暂无符合条件的提交记录。" : "调整筛选条件或重置筛选后重试。"}</p>
          <button className="button" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置筛选</button>
        </div>
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
