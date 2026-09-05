import { Link } from "react-router-dom";
import type {
  ChallengeLeaderboardSummary,
  LeaderboardSeasonPublicSummary,
  SeasonLeaderboard
} from "../../api/leaderboardsApi";

interface LeaderboardHomeViewProps {
  globalLeaderboard: SeasonLeaderboard | null;
  summary: LeaderboardSeasonPublicSummary | null | undefined;
  challenges: ChallengeLeaderboardSummary[];
  isLoading: boolean;
  error: string | null;
  canManage: boolean;
  showPersonalRecord: boolean;
}

export function LeaderboardHomeView({ globalLeaderboard, summary, challenges, isLoading, error, canManage, showPersonalRecord }: LeaderboardHomeViewProps) {
  const entries = globalLeaderboard?.entries ?? [];
  const season = globalLeaderboard?.season;
  const boards = summary?.boards ?? [];
  const hasGlobalBoard = canManage && boards.some((board) => board.boardType === 1);
  const hasChallengeBoards = boards.some((board) => board.boardType === 2);
  const enabledChallengeIds = new Set(boards.flatMap((board) => board.boardType === 2 && board.challengeId ? [board.challengeId] : []));
  const enabledChallenges = challenges.filter((challenge) => enabledChallengeIds.has(challenge.challengeId));
  const currentEntry = entries.find((entry) => entry.isCurrentUser);

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page">
      <div className="leaderboard-header leaderboard-v2-header" data-surface="decoration.pageHeader">
        <div>
          <h1>榜单中心</h1>
        </div>
        <div className="leaderboard-header-actions">
          {canManage && <Link className="button" to="/admin/leaderboard-seasons">榜单管理</Link>}
          {showPersonalRecord && <Link className="button" to="/account/competition">我的赛季战绩</Link>}
        </div>
      </div>

      {error && <div className="leaderboard-inline-note">榜单概览暂不可用：{error}</div>}

      <div className="leaderboard-v2-hub-grid season-board-card-grid">
        {hasGlobalBoard && <article className="leaderboard-v2-feature-card" data-surface="panel.primary">
          <div className="leaderboard-v2-feature-header" data-surface="panel.header">
            <div>
              <h2>{summary?.name ?? season?.name ?? "当前赛季"} · 全局榜</h2>
              <p>{entries.length} 人上榜{currentEntry ? ` · 我的排名 #${currentEntry.rank}` : ""}</p>
            </div>
            <Link className="button leaderboard-v2-primary-link" to="/leaderboards/users">查看完整榜单</Link>
          </div>
          <LeaderboardEntries entries={entries.map((entry) => ({ rank: entry.rank, name: entry.displayName, detail: `${entry.solvedCount} 题`, score: entry.totalScore, key: `${entry.rank}-${entry.alias}` }))} isLoading={isLoading} />
        </article>}

        {hasChallengeBoards && enabledChallenges.map((challenge) => <article className="leaderboard-v2-feature-card challenge-board-card" data-surface="panel.primary" key={challenge.challengeId}>
          <div className="leaderboard-v2-feature-header" data-surface="panel.header"><div><h2>{challenge.title}</h2><p>{challenge.participationMode === 2 ? "战队模式" : "个人模式"} · {challenge.participationMode === 2 ? `${challenge.teamCount} 支战队` : `${challenge.participantCount} 人参与`} · {challenge.completedUserCount} 个完成者</p></div><Link className="button leaderboard-v2-primary-link" to={`/challenges/${challenge.challengeId}/leaderboard`}>查看完整榜单</Link></div>
          <LeaderboardEntries entries={challenge.topEntries.slice(0, 3).map((entry) => ({ rank: entry.rank, name: entry.userName, detail: `${entry.completedTaskCount} 个任务`, score: entry.totalScore, key: `${challenge.challengeId}-${entry.rank}` }))} />
        </article>)}
        {hasChallengeBoards && !isLoading && enabledChallenges.length === 0 && <div className="compact-empty" data-surface="decoration.emptyState">已启用的挑战榜暂不可用。</div>}
        {!isLoading && boards.length === 0 && <div className="empty-state" data-surface="decoration.emptyState">当前赛季尚未启用公开榜单。</div>}
      </div>
    </section>
  );
}

function LeaderboardEntries({ entries, isLoading = false }: { entries: Array<{ rank: number; name: string; detail: string; score: number; key: string }>; isLoading?: boolean }) {
  return <div className="leaderboard-preview-list">{isLoading ? (
    <div className="leaderboard-preview-empty">正在加载领先用户...</div>
  ) : entries.length === 0 ? (
    <div className="leaderboard-preview-empty compact">暂无成绩</div>
  ) : entries.slice(0, 3).map((entry) => <div className="leaderboard-preview-row" key={entry.key}><span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span><span className="leaderboard-preview-user"><span className="leaderboard-avatar-placeholder">{entry.name.slice(0, 1).toUpperCase()}</span><span><strong>{entry.name}</strong><small>{entry.detail}</small></span></span><span className="leaderboard-preview-score"><strong>{entry.score}</strong><small>总分</small></span></div>)}</div>;
}

function getRankClass(rank: number) {
  if (rank === 1) return "top-one";
  if (rank === 2) return "top-two";
  if (rank === 3) return "top-three";
  return "";
}
