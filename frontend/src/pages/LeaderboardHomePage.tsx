import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  getChallengeLeaderboardIndex,
  getCurrentSeasonLeaderboard,
  type ChallengeLeaderboardIndex,
  type SeasonLeaderboard
} from "../api/leaderboardsApi";

export function LeaderboardHomePage() {
  const [globalLeaderboard, setGlobalLeaderboard] = useState<SeasonLeaderboard | null>(null);
  const [challengeIndex, setChallengeIndex] = useState<ChallengeLeaderboardIndex | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;

    Promise.all([getCurrentSeasonLeaderboard(), getChallengeLeaderboardIndex()])
      .then(([globalData, challengeData]) => {
        if (!ignore) {
          setGlobalLeaderboard(globalData);
          setChallengeIndex(challengeData);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "榜单概览加载失败");
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

  const entries = globalLeaderboard?.entries ?? [];
  const season = globalLeaderboard?.season;
  const challenges = challengeIndex?.challenges ?? [];
  const topUser = entries[0];
  const participantCount = challenges.reduce((sum, challenge) => sum + challenge.participantCount, 0);

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">LEADERBOARDS</p>
          <h1>榜单中心</h1>
          <p>从全局积分到单个挑战，快速查看平台上的排名、参与和完成情况。</p>
        </div>
        <span className="leaderboard-live-badge">实时榜单</span>
        <div className="leaderboard-header-actions"><Link className="button" to="/leaderboards/history">历史赛季</Link><Link className="button" to="/account/competition">我的赛季战绩</Link></div>
      </div>

      {error && <div className="leaderboard-inline-note">榜单概览暂不可用：{error}</div>}

      <div className="leaderboard-overview-grid">
        <article className="leaderboard-overview-card">
          <span>上榜用户</span>
          <strong>{isLoading ? "—" : entries.length}</strong>
          <small>进入当前赛季有效榜单的答题人</small>
        </article>
        <article className="leaderboard-overview-card">
          <span>已发布挑战</span>
          <strong>{isLoading ? "—" : challenges.length}</strong>
          <small>当前可参与并计入榜单的挑战</small>
        </article>
        <article className="leaderboard-overview-card">
          <span>挑战参与人次</span>
          <strong>{isLoading ? "—" : participantCount}</strong>
          <small>所有已发布挑战的参与人数汇总</small>
        </article>
        <article className="leaderboard-overview-card leaderboard-overview-card-accent">
          <span>当前最高总分</span>
          <strong>{isLoading ? "—" : topUser?.totalScore ?? 0}</strong>
          <small>{topUser ? `${topUser.displayName} 暂居赛季第一` : "暂无赛季排名记录"}</small>
        </article>
      </div>

      <div className="leaderboard-v2-hub-grid">
        <article className="leaderboard-v2-feature-card">
          <div className="leaderboard-v2-feature-header">
            <div>
              <p className="eyebrow">SEASON</p>
              <h2>{season?.name ?? "赛季用户榜单"}</h2>
              <p>{season ? "按赛季基础分和完成题数查看当前排名。" : "当前暂无进行中的排行榜赛季。"}</p>
            </div>
            <Link className="button leaderboard-v2-primary-link" to="/leaderboards/users">
              查看完整榜单
            </Link>
          </div>

          <div className="leaderboard-preview-list">
            {isLoading ? (
              <div className="leaderboard-preview-empty">正在加载领先用户...</div>
            ) : entries.length === 0 ? (
              <div className="leaderboard-preview-empty">当前赛季暂无有效成绩</div>
            ) : (
              entries.slice(0, 3).map((entry) => (
                <div className="leaderboard-preview-row" key={`${entry.rank}-${entry.alias}`}>
                  <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                  <span className="leaderboard-preview-user">
                    <span className="leaderboard-avatar-placeholder">{entry.displayName.slice(0, 1).toUpperCase()}</span>
                    <span>
                      <strong>{entry.displayName}</strong>
                      <small>{entry.solvedCount} 题</small>
                    </span>
                  </span>
                  <span className="leaderboard-preview-score">
                    <strong>{entry.totalScore}</strong>
                    <small>总分</small>
                  </span>
                </div>
              ))
            )}
          </div>
        </article>

        <article className="leaderboard-v2-feature-card">
          <div className="leaderboard-v2-feature-header">
            <div>
              <p className="eyebrow">CHALLENGES</p>
              <h2>挑战榜单</h2>
              <p>浏览已发布挑战的参与情况、完成进度和领先用户。</p>
            </div>
            <Link className="button leaderboard-v2-primary-link" to="/leaderboards/challenges">
              浏览挑战榜单
            </Link>
          </div>

          <div className="leaderboard-challenge-preview-list">
            {isLoading ? (
              <div className="leaderboard-preview-empty">正在加载挑战...</div>
            ) : challenges.length === 0 ? (
              <div className="leaderboard-preview-empty">暂无已发布挑战</div>
            ) : (
              challenges.slice(0, 3).map((challenge) => (
                <Link className="leaderboard-challenge-preview-row" to={`/challenges/${challenge.challengeId}/leaderboard`} key={challenge.challengeId}>
                  <span className="leaderboard-challenge-preview-main">
                    <strong>{challenge.title}</strong>
                    <small>
                      {challenge.totalTaskCount} 个任务 · {challenge.participantCount} 人参与
                    </small>
                  </span>
                  <span className="leaderboard-challenge-preview-meta">
                    <strong>{challenge.completedUserCount}</strong>
                    <small>已完成</small>
                  </span>
                </Link>
              ))
            )}
          </div>
        </article>
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
