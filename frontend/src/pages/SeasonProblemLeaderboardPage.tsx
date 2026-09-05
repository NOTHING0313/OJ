import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getCurrentSeasonProblemLeaderboard, type SeasonProblemLeaderboard } from "../api/leaderboardsApi";

const LIVE_REFRESH_MS = 10_000;

export function SeasonProblemLeaderboardPage() {
  const { problemId } = useParams();
  const [leaderboard, setLeaderboard] = useState<SeasonProblemLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const load = useCallback(async () => {
    if (!problemId) return;
    try {
      setLeaderboard(await getCurrentSeasonProblemLeaderboard(problemId));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "单题排行榜加载失败");
    } finally {
      setIsLoading(false);
    }
  }, [problemId]);

  useEffect(() => {
    void load();
    const timer = window.setInterval(() => {
      if (document.visibilityState !== "hidden") void load();
    }, LIVE_REFRESH_MS);
    return () => window.clearInterval(timer);
  }, [load]);

  if (isLoading) return <div className="state-line">正在加载单题排行榜...</div>;
  if (error) return <section className="page-section narrow"><div className="alert error">{error}</div></section>;

  if (!leaderboard?.season || !leaderboard.problem) {
    return (
      <section className="challenge-page leaderboard-page leaderboard-v2-page">
        <div className="leaderboard-header leaderboard-v2-header">
          <div><h1>单题排行榜</h1></div>
          <Link className="button" to="/leaderboards/users">返回赛季榜</Link>
        </div>
        <div className="empty-state">该题当前不属于进行中或公示中的赛季。</div>
      </section>
    );
  }

  const { season, problem, entries } = leaderboard;
  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <h1>{problem.problemTitle}</h1>
          <p>{season.name} · 基础分 {problem.baseScore}</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-live-status"><i /> 实时更新 · 10 秒</span>
          <Link className="button" to="/leaderboards/users">返回赛季榜</Link>
          <Link className="button" to={`/problems/${problem.problemId}`}>查看题目</Link>
        </div>
      </div>

      {entries.length === 0 ? <div className="empty-state">当前暂无满分成绩</div> : (
        <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap">
          <table className="leaderboard-table leaderboard-v2-table season-problem-leaderboard-table">
            <thead><tr><th>排名</th><th>用户</th><th>题目得分</th><th>时间奖励</th><th>运行奖励</th><th>内存奖励</th><th>计分性能</th></tr></thead>
            <tbody>{entries.map((entry) => (
              <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={`${entry.rank}-${entry.alias}`}>
                <td><span className={`leaderboard-rank ${rankClass(entry.rank)}`}>{entry.rank}</span></td>
                <td><strong>{entry.displayName}</strong>{entry.isCurrentUser && <small className="leaderboard-you-badge">YOU</small>}</td>
                <td><strong className="leaderboard-score">{entry.totalProblemScore}</strong><small className="season-score-detail">基础 {entry.earnedBaseScore}</small></td>
                <td>+{entry.timeBonus}<small className="season-score-detail">{entry.timeRank ? `Top ${entry.timeRank}` : "Top10 外"}</small></td>
                <td>+{entry.runtimeBonus}<small className="season-score-detail">{entry.runtimeMs == null ? "无数据" : `${entry.runtimeMs} ms`}</small></td>
                <td>+{entry.memoryBonus}<small className="season-score-detail">{entry.memoryKb == null ? "无数据" : `${entry.memoryKb} KB`}</small></td>
                <td>{languageLabel(entry.performanceLanguage)}</td>
              </tr>
            ))}</tbody>
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

function languageLabel(language: number | null) {
  if (language === 1) return "C++17";
  if (language === 2) return "C11";
  if (language === 3) return "C#";
  return "—";
}
