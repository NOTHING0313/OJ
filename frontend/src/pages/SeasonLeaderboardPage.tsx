import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getCurrentSeasonLeaderboard, type SeasonLeaderboard } from "../api/leaderboardsApi";

const LIVE_REFRESH_MS = 10_000;

export function SeasonLeaderboardPage() {
  const [leaderboard, setLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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

  if (isLoading) return <div className="state-line">正在加载赛季排行榜...</div>;

  if (error) {
    return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  }

  if (!leaderboard?.season) {
    return (
      <section className="challenge-page leaderboard-page leaderboard-v2-page">
        <div className="leaderboard-header leaderboard-v2-header">
          <div><p className="eyebrow">SEASON LEADERBOARD</p><h1>赛季排行榜</h1></div>
          <Link className="button" to="/leaderboards">返回榜单中心</Link>
        </div>
        <div className="empty-state">当前暂无进行中的排行榜赛季。</div>
      </section>
    );
  }

  const { season, entries } = leaderboard;
  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">SEASON LEADERBOARD</p>
          <h1>{season.name}</h1>
          <p>赛季时间：{formatDate(season.startAt)} 至 {formatDate(season.freezeAt)}</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-live-status"><i /> 实时更新 · 10 秒</span>
          <span className="leaderboard-total">共 {entries.length} 名答题人</span>
          <Link className="button" to="/leaderboards">返回榜单中心</Link>
        </div>
      </div>

      {entries.length === 0 ? (
        <div className="empty-state">当前赛季暂无有效成绩</div>
      ) : (
        <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap">
          <table className="leaderboard-table leaderboard-v2-table">
            <thead><tr><th>排名</th><th>用户</th><th>完成题目</th><th>基础分</th><th>总分</th></tr></thead>
            <tbody>
              {entries.map((entry) => (
                <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={`${entry.rank}-${entry.alias}`}>
                  <td><span className={`leaderboard-rank ${rankClass(entry.rank)}`}>{entry.rank}</span></td>
                  <td><strong>{entry.displayName}</strong>{entry.isCurrentUser && <small className="leaderboard-you-badge">YOU</small>}</td>
                  <td>{entry.solvedCount}</td>
                  <td>{entry.baseScore}</td>
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
