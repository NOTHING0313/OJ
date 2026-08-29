import { useCallback, useEffect, useState } from "react";
import { Link, Navigate } from "react-router-dom";
import { getCurrentSeasonLeaderboard, type SeasonLeaderboard } from "../api/leaderboardsApi";

const LIVE_REFRESH_MS = 10_000;

export function SeasonLeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [now, setNow] = useState(() => Date.now());

  const load = useCallback(async () => {
    try {
      setLeaderboard(await getCurrentSeasonLeaderboard());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "赛季排行榜加载失败");
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => {
      if (document.visibilityState !== "hidden") void load();
    }, LIVE_REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1_000);
    return () => window.clearInterval(timer);
  }, []);

  if (isLoading) return <div className="state-line">正在加载赛季排行榜...</div>;

  if (error) {
    return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  }

  if (!leaderboard?.season) {
    return <Navigate to="/problems" replace />;
  }

  const { season, entries } = leaderboard;
  const state = getSeasonState(season.effectiveStatus, season.startAt, season.freezeAt, season.publicUntil, now);
  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">SEASON LEADERBOARD</p>
          <h1>{season.name}</h1>
          <p>开始：{formatDate(season.startAt)} · 结榜：{formatDate(season.freezeAt)}</p>
          <div className="season-lifecycle-line"><span className={`season-status status-${season.effectiveStatus}`}>{state.label}</span><strong>{state.countdownLabel} {state.countdown}</strong></div>
          <p className="season-status-note">{state.note}</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-total">共 {entries.length} 名答题人</span>
          <Link className="button" to="/leaderboards">返回榜单中心</Link>
        </div>
      </div>

      <nav className="season-problem-links" aria-label="赛季题目排行榜">
        <span>题目排行</span>
        {season.problems.map((problem) => (
          <Link key={problem.problemId} to={`/leaderboards/users/problems/${problem.problemId}`}>{problem.problemTitle}</Link>
        ))}
      </nav>

      {entries.length === 0 ? (
        <div className="empty-state">当前赛季暂无有效成绩</div>
      ) : (
        <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap">
          <table className="leaderboard-table leaderboard-v2-table">
            <thead><tr><th>排名</th><th>用户</th><th>完成题目</th><th>基础分</th><th>时间奖励</th><th>性能奖励</th><th>总分</th></tr></thead>
            <tbody>
              {entries.map((entry) => (
                <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={`${entry.rank}-${entry.alias}`}>
                  <td><span className={`leaderboard-rank ${rankClass(entry.rank)}`}>{entry.rank}</span></td>
                  <td><strong>{entry.displayName}</strong>{entry.isCurrentUser && <small className="leaderboard-you-badge">YOU</small>}</td>
                  <td>{entry.solvedCount}</td>
                  <td>{entry.baseScore}</td>
                  <td>+{entry.timeBonus}</td>
                  <td>+{entry.runtimeBonus + entry.memoryBonus}</td>
                  <td><strong className="leaderboard-score">{entry.totalScore}</strong></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function rankClass(rank: number) {
  if (rank === 1) return "top-one";
  if (rank === 2) return "top-two";
  if (rank === 3) return "top-three";
  return "";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit"
  }).format(new Date(value));
}

function getSeasonState(status: number, startAt: string, freezeAt: string, publicUntil: string, now: number) {
  if (status === 1) return { label: "未开始", countdownLabel: "距离开榜", countdown: countdown(startAt, now), note: "赛季尚未开始，当前提交不会获得赛季积分。" };
  if (status === 2) return { label: "进行中", countdownLabel: "距离结榜", countdown: countdown(freezeAt, now), note: "赛季进行中，提交可以获得积分与奖励。" };
  if (status === 4) return { label: "公示中", countdownLabel: "距离归档", countdown: countdown(publicUntil, now), note: "赛季已结榜，当前为公示期。" };
  return { label: "待定榜", countdownLabel: "", countdown: "", note: "系统正在生成最终排名；失败时会自动重试。" };
}

function countdown(value: string, now: number) {
  const total = Math.max(0, Math.floor((new Date(value).getTime() - now) / 1_000));
  const days = Math.floor(total / 86_400);
  const hours = Math.floor(total % 86_400 / 3_600);
  const minutes = Math.floor(total % 3_600 / 60);
  const seconds = total % 60;
  return [days, hours, minutes, seconds].map(number => String(number).padStart(2, "0")).join(" : ");
}
