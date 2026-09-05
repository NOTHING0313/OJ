import { useCallback, useEffect, useState } from "react";
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
  SubmissionTable
} from "./MySubmissionsPage";
import { parseLanguage, parseStatus } from "../utils/submissionFilters";

const pageSize = 20;

export function AdminSubmissionsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const problemId = searchParams.get("problemId") ?? undefined;
  const [items, setItems] = useState<SubmissionQueryItem[]>([]);
  const [userKeyword, setUserKeyword] = useState("");
  const [problemKeyword, setProblemKeyword] = useState("");
  const [status, setStatus] = useState<JudgeStatus | "">("");
  const [submissionKind, setSubmissionKind] = useState<1 | 2 | "">("");
  const [language, setLanguage] = useState<JudgeLanguage | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadSubmissions = useCallback(async (signal: AbortSignal) => {
    try {
      setIsLoading(true);
      const result = await querySubmissions({
        problemId,
        userKeyword,
        problemKeyword,
        submissionKind,
        status,
        language,
        from: toIsoString(from),
        to: toIsoString(to),
        page,
        pageSize
      }, signal);
      if (signal.aborted) return;
      setItems(result.items);
      setTotalCount(result.totalCount);
      setError(null);
    } catch (err) {
      if (signal.aborted) return;
      setError(err instanceof Error ? err.message : "提交管理列表加载失败");
    } finally {
      if (!signal.aborted) setIsLoading(false);
    }
  }, [submissionKind, problemId, userKeyword, problemKeyword, status, language, from, to, page]);

  useEffect(() => {
    const controller = new AbortController();
    const handle = window.setTimeout(() => {
      void loadSubmissions(controller.signal);
    }, 180);

    return () => { controller.abort(); window.clearTimeout(handle); };
  }, [loadSubmissions]);

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const currentPage = Math.min(page, totalPages);
  const filtersAreDefault = !problemId && submissionKind === "" && !userKeyword && !problemKeyword && status === "" && language === "" && !from && !to;

  function resetFilters(update: () => void) {
    update();
    setPage(1);
  }

  function resetAllFilters() {
    setSearchParams(current => { const next = new URLSearchParams(current); next.delete("problemId"); return next; });
    setSubmissionKind("");
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
          <h1>提交管理</h1>
        </div>
        <span className="submission-total">共 {totalCount} 条提交</span>
      </div>

      {error && <div className="alert error">{error}</div>}

      {problemId && <div className="quiet-note">已限定当前题目 <button className="button" type="button" onClick={() => { setSearchParams(current => { const next = new URLSearchParams(current); next.delete("problemId"); return next; }); setPage(1); }}>清除题目筛选</button></div>}

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
        <label>
          <span>题型</span>
          <select value={submissionKind} onChange={event => resetFilters(() => { const kind = event.target.value === "" ? "" : Number(event.target.value) as 1 | 2; setSubmissionKind(kind); if (kind === 2) setLanguage(""); })}>
            <option value="">全部题型</option><option value="1">编程题</option><option value="2">选择题</option>
          </select>
        </label>
        <label className="submission-filter-language">
          <span>语言</span>
          <select disabled={submissionKind === 2} value={language} onChange={(event) => resetFilters(() => setLanguage(parseLanguage(event.target.value)))}>
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
