import { useCallback, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  getChallengeLeaderboard,
  getChallengeLeaderboardHistory,
  getChallengeLeaderboardProgress,
  type ChallengeLeaderboard,
  type ChallengeLeaderboardProgress
} from "../api/challengesApi";
import type { RankHistory } from "../api/leaderboardsApi";
import { ChallengeCompletionMatrix } from "../components/ChallengeCompletionMatrix";
import { RankHistoryChart } from "../components/RankHistoryChart";
import { useRankMovementAnimation } from "../components/useRankMovementAnimation";
import { mergeCurrentRankHistory } from "../utils/rankHistory";

const LIVE_REFRESH_MS = 10_000;

export function ChallengeLeaderboardPage() {
  const { challengeId } = useParams();
  const [leaderboard, setLeaderboard] = useState<ChallengeLeaderboard | null>(null);
  const [progress, setProgress] = useState<ChallengeLeaderboardProgress | null>(null);
  const [history, setHistory] = useState<RankHistory | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [lastUpdatedAt, setLastUpdatedAt] = useState<Date | null>(null);
  const rowKeys = leaderboard?.participationMode === 2
    ? leaderboard.teamEntries.map((entry) => entry.teamParticipantId)
    : leaderboard?.entries.map(entryKey) ?? [];
  const { capturePositions, setRowNode } = useRankMovementAnimation(rowKeys);

  useEffect(() => {
    if (!challengeId) {
      return;
    }

    let ignore = false;

    Promise.all([
      getChallengeLeaderboard(challengeId),
      getChallengeLeaderboardProgress(challengeId).catch(() => null),
      getChallengeLeaderboardHistory(challengeId, 10).catch(() => null)
    ])
      .then(([data, progressData, historyData]) => {
        if (!ignore) {
          setLeaderboard(data);
          setProgress(progressData);
          setHistory(historyData);
          setLastUpdatedAt(new Date());
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "排行榜加载失败");
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
  }, [challengeId]);

  const refreshLeaderboard = useCallback(async () => {
    if (!challengeId || document.visibilityState === "hidden") {
      return;
    }

    try {
      const [data, progressData] = await Promise.all([
        getChallengeLeaderboard(challengeId),
        getChallengeLeaderboardProgress(challengeId).catch(() => null)
      ]);
      capturePositions();
      setLeaderboard(data);
      if (progressData) {
        setProgress(progressData);
      }
      setHistory((current) => mergeCurrentRankHistory(current, data.entries));
      setLastUpdatedAt(new Date());
    } catch {
      // 保留最后一次成功数据，短暂网络波动不会让榜单闪空。
    }
  }, [capturePositions, challengeId]);

  useEffect(() => {
    const timerId = window.setInterval(() => void refreshLeaderboard(), LIVE_REFRESH_MS);
    return () => window.clearInterval(timerId);
  }, [refreshLeaderboard]);

  if (isLoading) {
    return <div className="state-line">正在加载排行榜...</div>;
  }

  if (error) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        {challengeId && (
          <Link className="button" to={`/challenges/${challengeId}`}>
            返回棋盘
          </Link>
        )}
      </section>
    );
  }

  if (!leaderboard) {
    return <div className="empty-state">暂无排行榜数据</div>;
  }

  if (leaderboard.participationMode === 2) {
    const completedTaskTotal = leaderboard.teamEntries.reduce((sum, entry) => sum + entry.completedTaskCount, 0);
    return (
      <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
        <div className="leaderboard-header leaderboard-v2-header">
          <div><p className="eyebrow">TEAM CHALLENGE LEADERBOARD</p><h1>{leaderboard.challengeTitle}</h1><p>仅展示冻结报名战队的汇总成绩；成员与贡献者信息仅供管理员审计。</p></div>
          <div className="leaderboard-header-actions"><span className="leaderboard-total">共 {leaderboard.teamEntries.length} 支战队</span><Link className="button" to={`/challenges/${leaderboard.challengeId}`}>返回棋盘</Link></div>
        </div>
        <div className="leaderboard-mini-metrics"><div><span>挑战任务</span><strong>{leaderboard.totalTaskCount}</strong></div><div><span>报名战队</span><strong>{leaderboard.teamEntries.length}</strong></div><div><span>当前第一</span><strong>{leaderboard.teamEntries[0]?.teamName ?? "—"}</strong></div><div><span>累计完成题目</span><strong>{completedTaskTotal}</strong></div></div>
        {leaderboard.teamEntries.length === 0 ? <div className="management-state-panel management-empty-state"><strong>暂无战队成绩</strong><p>报名战队完成挑战任务后会出现在这里。</p></div> : (
          <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap"><table className="leaderboard-table leaderboard-v2-table"><thead><tr><th>排名</th><th>战队</th><th>完成题目</th><th>总分</th><th>最后提升时间</th></tr></thead><tbody>{leaderboard.teamEntries.map((entry) => <tr key={entry.teamParticipantId} ref={(node) => setRowNode(entry.teamParticipantId, node)}><td><span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span></td><td><strong>{entry.teamName}</strong></td><td><span className="leaderboard-progress-copy"><strong>{entry.completedTaskCount}</strong><small>/ {leaderboard.totalTaskCount}</small></span></td><td><strong className="leaderboard-score">{entry.totalScore}</strong></td><td>{formatDate(entry.lastImprovedAt)}</td></tr>)}</tbody></table></div>
        )}
      </section>
    );
  }

  const topEntry = leaderboard.entries[0];
  const completedTaskTotal = leaderboard.entries.reduce((sum, entry) => sum + entry.completedTaskCount, 0);

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page leaderboard-live-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARD</p>
          <h1>{leaderboard.challengeTitle}</h1>
          <p>查看当前挑战的实时排名、参与者任务完成情况和近十天名次变化。</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-live-status">
            <i /> 实时更新 · 10 秒
            {lastUpdatedAt && <small>{formatUpdatedTime(lastUpdatedAt)}</small>}
          </span>
          <span className="leaderboard-total">共 {progress?.users.length ?? leaderboard.entries.length} 名参与者</span>
          <Link className="button" to={`/challenges/${leaderboard.challengeId}`}>
            返回棋盘
          </Link>
        </div>
      </div>

      <div className="leaderboard-mini-metrics">
        <div>
          <span>挑战任务</span>
          <strong>{leaderboard.totalTaskCount}</strong>
        </div>
        <div>
          <span>上榜人数</span>
          <strong>{leaderboard.entries.length}</strong>
        </div>
        <div>
          <span>当前第一</span>
          <strong>{topEntry?.userName ?? "—"}</strong>
        </div>
        <div>
          <span>累计完成题目</span>
          <strong>{completedTaskTotal}</strong>
        </div>
      </div>

      {leaderboard.entries.length === 0 ? (
        <div className="management-state-panel management-empty-state">
          <strong>暂无完成记录</strong>
          <p>完成挑战任务的用户会出现在这里。</p>
        </div>
      ) : (
        <div className="leaderboard-v2-table-wrap leaderboard-live-table-wrap">
          <table className="leaderboard-table leaderboard-v2-table leaderboard-v2-challenge-table">
            <thead>
              <tr>
                <th>排名</th>
                <th>用户</th>
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
                  <td>
                    <span className="leaderboard-progress-copy">
                      <strong>{entry.completedTaskCount}</strong>
                      <small>/ {leaderboard.totalTaskCount}</small>
                    </span>
                  </td>
                  <td>
                    <strong className="leaderboard-score">{entry.totalScore}</strong>
                  </td>
                  <td>{formatDate(entry.lastCompletedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <ChallengeCompletionMatrix progress={progress} />

      <RankHistoryChart
        history={history}
        currentEntries={leaderboard.entries}
        title="挑战近 10 天名次变化"
        description="按挑战内积分和完成题数生成每日排名；今天的数据随实时榜单同步变化。"
      />
    </section>
  );
}

function entryKey(entry: { userId: string | null; userName: string }) {
  return entry.userId ?? `anonymous:${entry.userName}`;
}

function getRankClass(rank: number) {
  if (rank === 1) return "top-one";
  if (rank === 2) return "top-two";
  if (rank === 3) return "top-three";
  return "";
}

function formatDate(value: string | null) {
  if (!value) return "尚未完成";

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
