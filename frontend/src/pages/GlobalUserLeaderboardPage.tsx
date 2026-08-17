import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getGlobalUserLeaderboard, type GlobalUserLeaderboard } from "../api/leaderboardsApi";

export function GlobalUserLeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<GlobalUserLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;

    getGlobalUserLeaderboard()
      .then((data) => {
        if (!ignore) {
          setLeaderboard(data);
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

  return (
    <section className="challenge-page leaderboard-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">GLOBAL LEADERBOARD</p>
          <h1>全局用户榜单</h1>
          <p>只统计已发布挑战，未完成用户不计入榜单。</p>
        </div>
        <Link className="button" to="/leaderboards">
          返回榜单中心
        </Link>
      </div>

      <div className="table-wrap leaderboard-table-wrap">
        <table className="leaderboard-table">
          <thead>
            <tr>
              <th>排名</th>
              <th>用户</th>
              <th>完成挑战数</th>
              <th>完成题数</th>
              <th>总分</th>
              <th>最后完成时间</th>
            </tr>
          </thead>
          <tbody>
            {leaderboard.entries.map((entry) => (
              <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={entry.userId}>
                <td>
                  <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                </td>
                <td>
                  <div className="leaderboard-user">
                    {entry.avatarUrl ? (
                      <img src={entry.avatarUrl} alt={entry.userName} />
                    ) : (
                      <span className="leaderboard-avatar-placeholder">{entry.userName.slice(0, 1).toUpperCase()}</span>
                    )}
                    <span>{entry.userName}</span>
                  </div>
                </td>
                <td>{entry.completedChallengeCount}</td>
                <td>{entry.completedTaskCount}</td>
                <td>{entry.totalScore}</td>
                <td>{formatDate(entry.lastCompletedAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function getRankClass(rank: number) {
  if (rank === 1) {
    return "top-one";
  }

  if (rank === 2) {
    return "top-two";
  }

  if (rank === 3) {
    return "top-three";
  }

  return "";
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
