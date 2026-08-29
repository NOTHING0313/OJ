import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getCurrentSeasonPersonal, type LeaderboardSeasonPersonal } from "../api/leaderboardsApi";

export function AccountCompetitionPage() {
  const [current, setCurrent] = useState<LeaderboardSeasonPersonal | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    getCurrentSeasonPersonal().then(setCurrent)
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "赛季战绩加载失败"));
  }, []);
  const points = useMemo(() => chartPoints(current?.rankHistory ?? []), [current]);
  if (error) return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  return <section className="challenge-page leaderboard-page leaderboard-v2-page season-operations-page">
    <div className="leaderboard-header leaderboard-v2-header"><div><p className="eyebrow">MY COMPETITION</p><h1>赛季战绩</h1><p>当前排名趋势与历史赛季最终成绩。</p></div><Link className="button" to="/account/settings">账号设置</Link></div>
    {!current ? <div className="state-line">正在加载赛季战绩...</div> : !current.season ? <div className="empty-state">当前暂无赛季</div> : <>
      <div className="leaderboard-overview-grid"><Metric label="当前排名" value={current.currentRank ? `#${current.currentRank}` : "—"}/><Metric label="总分" value={current.totalScore}/><Metric label="完成题目" value={`${current.solvedCount}/${current.seasonProblemCount}`}/><Metric label="Top10 / 第一" value={`${current.top10ProblemCount} / ${current.firstPlaceProblemCount}`}/></div>
      <article className="leaderboard-v2-feature-card season-rank-chart"><div><h2>排名趋势</h2><p>最佳排名 #{current.bestRank ?? "—"} · 本期变化 {formatChange(current.rankChange)}</p></div>{points ? <svg viewBox="0 0 600 180" role="img" aria-label="赛季排名变化折线图"><polyline points={points} /></svg> : <div className="empty-state">等待首次排名采样</div>}</article>
      <div className="leaderboard-v2-table-wrap"><table className="leaderboard-table leaderboard-v2-table"><thead><tr><th>题目</th><th>得分</th><th>时间排名</th><th>时间奖励</th><th>性能奖励</th></tr></thead><tbody>{current.problems.map(problem => <tr key={problem.problemId}><td>{problem.title}</td><td>{problem.score}</td><td>{problem.timeRank ? `#${problem.timeRank}` : "—"}</td><td>+{problem.timeBonus}</td><td>+{problem.performanceBonus}</td></tr>)}</tbody></table></div>
    </>}
  </section>;
}

function Metric({ label, value }: { label: string; value: string | number }) { return <article className="leaderboard-overview-card"><span>{label}</span><strong>{value}</strong></article>; }
function formatChange(value: number | null) { return value === null || value === 0 ? "—" : value > 0 ? `↑${value}` : `↓${Math.abs(value)}`; }
function chartPoints(history: LeaderboardSeasonPersonal["rankHistory"]) {
  if (history.length < 2) return null;
  const maxRank = Math.max(...history.map(point => point.rank), 1);
  return history.map((point, index) => `${20 + index * 560 / (history.length - 1)},${20 + (point.rank - 1) * 140 / maxRank}`).join(" ");
}
