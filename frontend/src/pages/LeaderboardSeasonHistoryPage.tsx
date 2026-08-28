import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getLeaderboardSeasonHistory, type LeaderboardSeasonHistorySummary } from "../api/leaderboardsApi";

export function LeaderboardSeasonHistoryPage() {
  const [seasons, setSeasons] = useState<LeaderboardSeasonHistorySummary[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getLeaderboardSeasonHistory().then(setSeasons).catch((err: unknown) =>
      setError(err instanceof Error ? err.message : "历史赛季加载失败"));
  }, []);

  return <section className="challenge-page leaderboard-page leaderboard-v2-page season-operations-page">
    <div className="leaderboard-header leaderboard-v2-header">
      <div><p className="eyebrow">SEASON HISTORY</p><h1>历史赛季</h1><p>查看已经归档并永久保留的最终排行榜。</p></div>
      <Link className="button" to="/leaderboards/users">当前赛季</Link>
    </div>
    {error ? <div className="alert error">{error}</div> : seasons.length === 0 ? <div className="empty-state">暂无已归档赛季</div> :
      <div className="season-history-grid">{seasons.map(season => <Link className="leaderboard-v2-feature-card season-history-card" to={`/leaderboards/history/${season.seasonId}`} key={season.seasonId}>
        <div><small>{formatDate(season.startAt)} — {formatDate(season.freezeAt)}</small><h2>{season.name}</h2></div>
        <div className="season-history-facts"><span>参与人数<strong>{season.participantCount}</strong></span><span>冠军<strong>{season.top3[0]?.displayName ?? "—"}</strong></span><span>最高分<strong>{season.top3[0]?.finalScore ?? 0}</strong></span></div>
        <ol>{season.top3.map(entry => <li key={entry.rank}>#{entry.rank} {entry.displayName} · {entry.finalScore} 分</li>)}</ol>
      </Link>)}</div>}
  </section>;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", { year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date(value));
}
