import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { getCurrentSeasonPersonal, getSeasonPersonalHistory, getUserSeasonPersonal, getUserSeasonHistory, type LeaderboardSeasonPersonalHistory, type LeaderboardSeasonPersonal } from "../api/leaderboardsApi";

export function AccountCompetitionPage() {
  const { userId } = useParams();
  const { currentUser } = useAuth();
  return <SeasonRecordContent key={`${currentUser?.id}:${userId ?? "me"}`} userId={userId} />;
}

function SeasonRecordContent({ userId }: { userId?: string }) {
  const [current, setCurrent] = useState<LeaderboardSeasonPersonal | null>(null);
  const [history, setHistory] = useState<LeaderboardSeasonPersonalHistory[]>([]);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    let ignore = false;
    Promise.all([
      userId ? getUserSeasonPersonal(userId) : getCurrentSeasonPersonal(),
      userId ? getUserSeasonHistory(userId) : getSeasonPersonalHistory()
    ]).then(([record, past]) => {
      if (!ignore) { setCurrent(record); setHistory(past); }
    }).catch(() => { if (!ignore) setError("赛季战绩加载失败，请刷新重试；若持续失败，请确认账号权限。"); });
    return () => { ignore = true; };
  }, [userId]);
  const points = useMemo(() => chartPoints(current?.rankHistory ?? []), [current]);
  if (error) return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  return <section className="challenge-page leaderboard-page leaderboard-v2-page season-operations-page">
    <div className="leaderboard-header leaderboard-v2-header"><div><h1>{userId ? "用户赛季战绩" : "我的赛季战绩"}</h1>{userId && <p>{current?.userName ?? "正在加载用户信息…"}</p>}</div><Link className="button" to={userId ? "/admin/leaderboard-seasons" : "/profile/me"}>{userId ? "返回榜单管理" : "返回个人中心"}</Link></div>
    {!current ? <div className="state-line">正在加载赛季战绩...</div> : !current.season ? <div className="empty-state">当前暂无赛季</div> : <>
      <h2>{current.season.name}</h2>
      <div className="leaderboard-overview-grid"><Metric label="当前排名" value={current.currentRank ? `#${current.currentRank}` : "—"}/><Metric label="总分" value={current.totalScore}/><Metric label="完成题目" value={`${current.solvedCount}/${current.seasonProblemCount}`}/><Metric label="Top10 / 第一" value={`${current.top10ProblemCount} / ${current.firstPlaceProblemCount}`}/></div>
      <article className="leaderboard-v2-feature-card season-rank-chart"><div><h2>排名趋势</h2><p>最佳排名 #{current.bestRank ?? "—"} · 本期变化 {formatChange(current.rankChange)}</p></div>{points ? <svg viewBox="0 0 600 180" role="img" aria-label="赛季排名变化折线图"><polyline points={points} /></svg> : <div className="empty-state">等待首次排名采样</div>}</article>
      <div className="leaderboard-v2-table-wrap"><table className="leaderboard-table leaderboard-v2-table"><thead><tr><th>题目</th><th>得分</th><th>时间排名</th><th>时间奖励</th><th>性能奖励</th></tr></thead><tbody>{current.problems.map(problem => <tr key={problem.problemId}><td>{problem.title}</td><td>{problem.score}</td><td>{problem.timeRank ? `#${problem.timeRank}` : "—"}</td><td>+{problem.timeBonus}</td><td>+{problem.performanceBonus}</td></tr>)}</tbody></table></div>
    </>}
    {current && <section className="content-block">
      <h2>{userId ? "历史赛季成绩" : "我的历史赛季"}</h2>
      {history.length === 0 ? <div className="empty-state">暂无历史赛季成绩</div> : history.map(season => <details className="content-block" key={season.seasonId}>
        <summary>{season.seasonName} · 第 {season.finalRank} 名 · {season.finalScore} 分 · 完成 {season.solvedCount} 题</summary>
        <div className="table-wrap"><table><thead><tr><th>题目</th><th>基础分</th><th>时间奖励</th><th>性能奖励</th><th>最终得分</th></tr></thead>
          <tbody>{season.problems.map(problem => <tr key={problem.problemId}><td>{problem.problemTitleSnapshot}</td><td>{problem.earnedBaseScore}/{problem.baseScore}</td><td>{problem.timeBonus}</td><td>{problem.runtimeBonus + problem.memoryBonus}</td><td>{problem.finalProblemScore}</td></tr>)}</tbody>
        </table></div>
      </details>)}
    </section>}
  </section>;
}

function Metric({ label, value }: { label: string; value: string | number }) { return <article className="leaderboard-overview-card"><span>{label}</span><strong>{value}</strong></article>; }
function formatChange(value: number | null) { return value === null || value === 0 ? "—" : value > 0 ? `↑${value}` : `↓${Math.abs(value)}`; }
function chartPoints(history: LeaderboardSeasonPersonal["rankHistory"]) {
  if (history.length < 2) return null;
  const maxRank = Math.max(...history.map(point => point.rank), 1);
  return history.map((point, index) => `${20 + index * 560 / (history.length - 1)},${20 + (point.rank - 1) * 140 / maxRank}`).join(" ");
}
