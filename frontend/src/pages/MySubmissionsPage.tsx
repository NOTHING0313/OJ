import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  querySubmissions,
  type JudgeLanguage,
  type JudgeStatus,
  type SubmissionQueryItem
} from "../api/submissionsApi";
import { languageLabel, statusLabel } from "../utils/labels";

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
  const filtersAreDefault = !problemKeyword && status === "" && language === "";

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

  function resetAllFilters() {
    setProblemKeyword("");
    setStatus("");
    setLanguage("");
    setPage(1);
  }

  return (
    <section className="challenge-page submissions-page submission-v2-page">
      <div className="leaderboard-header submission-header">
        <div>
          <p className="eyebrow">SUBMISSIONS</p>
          <h1>我的提交</h1>
          <p>查看自己的判题记录、筛选状态和语言，并进入提交详情。</p>
        </div>
        <span className="submission-total">共 {totalCount} 条提交</span>
      </div>

      {error && <div className="alert error">{error}</div>}

      <div className="submission-toolbar submission-toolbar-my">
        <label className="submission-search-field">
          <span>题目</span>
          <input
            disabled={Boolean(problemId)}
            placeholder={problemId ? "已按题目筛选" : "搜索题目标题"}
            value={problemKeyword}
            onChange={(event) => resetFilters(() => setProblemKeyword(event.target.value))}
          />
        </label>
        <label>
          <span>状态</span>
          <select value={status} onChange={(event) => resetFilters(() => setStatus(parseStatus(event.target.value)))}>
            <option value="">状态：全部</option>
            <StatusOptions />
          </select>
        </label>
        <label>
          <span>语言</span>
          <select value={language} onChange={(event) => resetFilters(() => setLanguage(parseLanguage(event.target.value)))}>
            <option value="">语言：全部</option>
            <LanguageOptions />
          </select>
        </label>
        <button className="button submission-toolbar-reset" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>
          重置
        </button>
      </div>

      {isLoading ? (
        <div className="submission-state-panel">正在加载提交记录...</div>
      ) : items.length === 0 ? (
        <div className="submission-state-panel submission-empty-state">
          <strong>未找到匹配的提交</strong>
          <p>{problemId ? "当前题目下暂无符合条件的提交记录。" : "调整筛选条件或重置筛选后重试。"}</p>
          <button className="button" type="button" disabled={filtersAreDefault} onClick={resetAllFilters}>重置筛选</button>
        </div>
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
    <div className="table-wrap submission-table-wrap">
      <table className={`submission-table ${showUser ? "submission-table-admin" : "submission-table-mine"}`}>
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
              <td><SubmissionDateTime value={item.createdAt} /></td>
              <td>
                <Link className="submission-problem-link" title={item.problemTitle} to={`/problems/${item.problemId}`}>
                  {item.problemTitle}
                </Link>
              </td>
              {showUser && (
                <td>
                  <div className="submission-user-cell">
                    <span className="submission-user-avatar-placeholder">{item.userName.slice(0, 1).toUpperCase()}</span>
                    <span title={item.userName}>{item.userName}</span>
                  </div>
                </td>
              )}
              <td><span className="submission-language-badge">{languageLabel(item.language)}</span></td>
              <td><SubmissionStatusBadge status={item.status} /></td>
              <td><span className="submission-metric">{formatMetric(item.timeUsedMs, "ms")}</span></td>
              <td><span className="submission-metric">{formatMetric(item.memoryUsedKb, "KB")}</span></td>
              <td><SubmissionDateTime value={item.finishedAt} /></td>
              <td>
                <Link className="button submission-view-link" to={`/submissions/${item.id}`}>查看</Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SubmissionStatusBadge({ status }: { status: JudgeStatus }) {
  return <span className={`submission-status-badge submission-status-${statusTone(status)}`}>{statusLabel(status)}</span>;
}

function SubmissionDateTime({ value }: { value: string | null }) {
  const dateTime = formatSubmissionDateTime(value);
  if (!dateTime) {
    return <span className="submission-empty-value">—</span>;
  }

  return (
    <time className="submission-date-time" dateTime={value ?? undefined}>
      <strong>{dateTime.date}</strong>
      <span>{dateTime.time}</span>
    </time>
  );
}

function statusTone(status: JudgeStatus) {
  switch (status) {
    case 1:
      return "pending";
    case 2:
      return "judging";
    case 3:
      return "accepted";
    case 4:
      return "wrong-answer";
    case 5:
      return "limit";
    case 6:
      return "limit";
    case 7:
      return "runtime-error";
    case 8:
      return "compile-error";
    case 9:
      return "system-error";
  }
}

function formatMetric(value: number | null, unit: string) {
  return value === null ? "—" : `${value} ${unit}`;
}

function formatSubmissionDateTime(value: string | null) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return null;
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
    <div className="pagination-row submission-pagination">
      <span>共 {totalCount} 条 · 每页 {pageSize} 条 · 第 {page} / {totalPages} 页</span>
      <div className="button-row">
        <button className="button" disabled={page <= 1} type="button" onClick={() => onPageChange(page - 1)}>上一页</button>
        <button className="button" disabled={page >= totalPages} type="button" onClick={() => onPageChange(page + 1)}>下一页</button>
      </div>
    </div>
  );
}
