import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { RankHistoryChart } from "../components/RankHistoryChart";
import { useRankMovementAnimation } from "../components/useRankMovementAnimation";
import {
  getGlobalUserLeaderboard,
  getGlobalUserRankHistory,
  type GlobalUserLeaderboard,
  type RankHistory
} from "../api/leaderboardsApi";
import { mergeCurrentRankHistory } from "../utils/rankHistory";

const LIVE_REFRESH_MS = 10_000;

export function GlobalUserLeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<GlobalUserLeaderboard | null>(null);
  const [history, setHistory] = useState<RankHistory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const rowKeys = leaderboard?.entries.map(entryKey) ?? [];
  const { capturePositions, setRowNode } = useRankMovementAnimation(rowKeys);

  useEffect(() => {
    let ignore = false;

    Promise.all([getGlobalUserLeaderboard(), getGlobalUserRankHistory(10).catch(() => null)])
      .then(([data, historyData]) => {
        if (!ignore) {
          setLeaderboard(data);
          setHistory(historyData);
          setLastUpdatedAt(new Date());
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "全局榜单加载失败");
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

  const refreshLeaderboard = useCallback(async () => {
    if (document.visibilityState === "hidden") {
      return;
    }

    try {
      const data = await getGlobalUserLeaderboard();
      capturePositions();
      setLeaderboard(data);
      setHistory((current) => mergeCurrentRankHistory(current, data.entries));
      setLastUpdatedAt(new Date());
    } catch {
      // 保留最后一次成功数据，避免短暂网络波动让榜单闪空。
    }
  }, [capturePositions]);

  useEffect(() => {
    const timerId = window.setInterval(() => void refreshLeaderboard(), LIVE_REFRESH_MS);
    return () => window.clearInterval(timerId);
  }, [refreshLeaderboard]);

  if (isLoading) {
    return <div className="state-line">正在加载全局榜单...</div>;
  }

  if (error) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to="/leaderboards">
          返回榜单中心
        </Link>
      </section>
    );
  }

  if (!leaderboard || leaderboard.entries.length === 0) {
    return <div className="empty-state">暂无榜单数据</div>;
  }

  const topEntry = leaderboard.entries[0];
  const completedTasks = leaderboard.entries.reduce((sum, entry) => sum + entry.completedTaskCount, 0);
  const completedChallenges = leaderboard.entries.reduce((sum, entry) => sum + entry.completedChallengeCount, 0);

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">GLOBAL LEADERBOARD</p>
          <h1>全局用户榜单</h1>
          <p>只统计已发布挑战，按总分、完成题数和完成挑战数展示当前排名。</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-live-status">
            <i /> 实时更新 · 10 秒
            {lastUpdatedAt && <small>{formatUpdatedTime(lastUpdatedAt)}</small>}
          </span>
          <span className="leaderboard-total">共 {leaderboard.entries.length} 名用户</span>
          <Link className="button" to="/leaderboards">
            返回榜单中心
          </Link>
        </div>
      </div>

      <div className="leaderboard-mini-metrics">
        <div>
          <span>当前第一</span>
          <strong>{topEntry.userName}</strong>
        </div>
        <div>
          <span>最高总分</span>
          <strong>{topEntry.totalScore}</strong>
        </div>
        <div>
          <span>累计完成题目</span>
          <strong>{completedTasks}</strong>
        </div>
        <div>
          <span>累计完成挑战</span>
          <strong>{completedChallenges}</strong>
        </div>
      </div>

      <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap">
        <table className="leaderboard-table leaderboard-v2-table">
          <thead>
            <tr>
              <th>排名</th>
              <th>用户</th>
              <th>完成挑战</th>
              <th>完成题目</th>
              <th>总分</th>
              <th>最后完成时间</th>
            </tr>
          </thead>
          <tbody>
            {leaderboard.entries.map((entry) => (
              <tr
                className={entry.isCurrentUser ? "leaderboard-current-user" : ""}
                key={entryKey(entry)}
                ref={(node) => setRowNode(entryKey(entry), node)}
              >
                <td>
                  <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                </td>
                <td>
                  <div className="leaderboard-user leaderboard-user-link">
                    {entry.avatarUrl ? (
                      <img src={entry.avatarUrl} alt={entry.userName} />
                    ) : (
                      <span className="leaderboard-avatar-placeholder">{entry.userName.slice(0, 1).toUpperCase()}</span>
                    )}
                    <span>
                      <strong>{entry.userName}</strong>
                      {entry.isCurrentUser && <small>当前用户</small>}
                    </span>
                  </div>
                </td>
                <td>{entry.completedChallengeCount}</td>
                <td>{entry.completedTaskCount}</td>
                <td>
                  <strong className="leaderboard-score">{entry.totalScore}</strong>
                </td>
                <td>{formatDate(entry.lastCompletedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <RankHistoryChart
        history={history}
        currentEntries={leaderboard.entries}
        description="每天记录一次排名轨迹；当前日名次随实时榜单同步变化。"
      />
    </section>
  );
}

function getRankClass(rank: number) {
  if (rank === 1) return "top-one";
  if (rank === 2) return "top-two";
  if (rank === 3) return "top-three";
  return "";
}

function formatDate(value: string | null) {
  if (!value) return "—";

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatUpdatedTime(value: Date) {
  return new Intl.DateTimeFormat("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }).format(value);
}

function entryKey(entry: { userId: string | null; userName: string }) {
  return entry.userId ?? `anonymous:${entry.userName}`;
}
