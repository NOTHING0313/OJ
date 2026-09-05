import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getLeaderboardSeasonHistoryDetail, type LeaderboardSeasonArchive } from "../api/leaderboardsApi";

export function LeaderboardSeasonHistoryDetailPage() {
  const { seasonId } = useParams();
  const [archive, setArchive] = useState<LeaderboardSeasonArchive | null>(null);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => {
    if (seasonId) getLeaderboardSeasonHistoryDetail(seasonId).then(setArchive).catch((err: unknown) =>
      setError(err instanceof Error ? err.message : "历史排行榜加载失败"));
  }, [seasonId]);

  if (error) return <section className="page-section narrow"><div className="alert error">{error}</div></section>;
  if (!archive) return <div className="state-line">正在加载历史排行榜...</div>;
  return <section className="challenge-page leaderboard-page leaderboard-v2-page season-operations-page">
    <div className="leaderboard-header leaderboard-v2-header"><div><h1>{archive.seasonName}</h1></div><Link className="button" to="/leaderboards/history">返回历史赛季</Link></div>
    <div className="leaderboard-v2-table-wrap"><table className="leaderboard-table leaderboard-v2-table"><thead><tr><th>排名</th><th>用户</th><th>完成题目</th><th>基础分</th><th>时间奖励</th><th>性能奖励</th><th>总分</th><th>逐题明细</th></tr></thead><tbody>
      {archive.entries.map(entry => <tr key={`${entry.finalRank}-${entry.alias}`}><td><span className="leaderboard-rank">{entry.finalRank}</span></td><td>{entry.userId ? <Link to={`/admin/leaderboard-seasons/users/${entry.userId}`}>{entry.displayNameSnapshot}</Link> : <strong>{entry.displayNameSnapshot}</strong>}</td><td>{entry.solvedCount}</td><td>{entry.finalBaseScore}</td><td>+{entry.finalTimeBonus}</td><td>+{entry.finalRuntimeBonus + entry.finalMemoryBonus}</td><td><strong className="leaderboard-score">{entry.finalScore}</strong></td><td><details className="season-score-breakdown"><summary>查看</summary>{entry.problemScores.map(problem => <div key={problem.problemId}><strong>{problem.problemTitleSnapshot}</strong><span>基础 {problem.earnedBaseScore}/{problem.baseScore}</span><span>时间 #{problem.timeRank ?? "—"} / +{problem.timeBonus}</span><span>运行 {problem.runtimeMs ?? "—"} ms / +{problem.runtimeBonus}</span><span>内存 {problem.memoryKb ?? "—"} KB / +{problem.memoryBonus}</span><span>最终 {problem.finalProblemScore}</span></div>)}</details></td></tr>)}
    </tbody></table></div>
  </section>;
}
