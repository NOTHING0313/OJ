import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getChallengeLeaderboardIndex, type ChallengeLeaderboardIndex } from "../api/leaderboardsApi";

export function ChallengeLeaderboardIndexPage() {
  const [index, setIndex] = useState<ChallengeLeaderboardIndex | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;

    getChallengeLeaderboardIndex()
      .then((data) => {
        if (!ignore) {
          setIndex(data);
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "挑战榜单加载失败");
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
    return <div className="state-line">正在加载挑战榜单...</div>;
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

  if (!index || index.challenges.length === 0) {
    return <div className="empty-state">暂无已发布挑战</div>;
  }

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARDS</p>
          <h1>挑战榜单</h1>
          <p>浏览所有已发布挑战的领先者、参与情况和完整排行榜入口。</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-total">共 {index.challenges.length} 个挑战</span>
          <Link className="button" to="/leaderboards">
            返回榜单中心
          </Link>
        </div>
      </div>

      <div className="leaderboard-challenge-list leaderboard-v2-challenge-list">
        {index.challenges.map((challenge) => (
          <article className="leaderboard-challenge-card leaderboard-v2-challenge-card" key={challenge.challengeId}>
            <div className="leaderboard-challenge-main">
              <div className="leaderboard-challenge-title-row">
                <span className="management-badge management-status-published">已发布</span>
                <span className="leaderboard-challenge-task-count">{challenge.totalTaskCount} 个任务</span>
              </div>
              <h2>{challenge.title}</h2>
              <p>{challenge.description ? challenge.description.slice(0, 160) : "暂无简介"}</p>

              <div className="leaderboard-challenge-stats">
                <div>
                  <span>参与人数</span>
                  <strong>{challenge.participantCount}</strong>
                </div>
                <div>
                  <span>完成人数</span>
                  <strong>{challenge.completedUserCount}</strong>
                </div>
                <div>
                  <span>完成率</span>
                  <strong>{formatPercent(challenge.completedUserCount, challenge.participantCount)}</strong>
                </div>
              </div>

              <div className="challenge-time leaderboard-v2-time-row">
                <span>开始：{formatDate(challenge.startAt)}</span>
                <span>截止：{formatDate(challenge.endAt)}</span>
              </div>
            </div>

            <aside className="leaderboard-top-panel leaderboard-v2-top-panel">
              <div className="leaderboard-v2-top-header">
                <div>
                  <p className="eyebrow">TOP 3</p>
                  <strong>领先用户</strong>
                </div>
                <Link className="admin-user-view-link" to={`/challenges/${challenge.challengeId}/leaderboard`}>
                  完整榜单
                </Link>
              </div>

              {challenge.topEntries.length === 0 ? (
                <div className="leaderboard-preview-empty leaderboard-preview-empty-compact">暂无完成记录</div>
              ) : (
                <ol>
                  {challenge.topEntries.map((entry) => (
                    <li key={entry.userId}>
                      <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                      <div>
                        <strong>{entry.userName}</strong>
                        <span>
                          {entry.totalScore} 分 · {entry.completedTaskCount} 题
                        </span>
                      </div>
                    </li>
                  ))}
                </ol>
              )}
            </aside>
          </article>
        ))}
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
    return "—";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}

function formatPercent(completed: number, total: number) {
  if (total <= 0) {
    return "0%";
  }

  return `${Math.round((completed / total) * 100)}%`;
}
