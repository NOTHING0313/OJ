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
    <section className="challenge-page leaderboard-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARDS</p>
          <h1>挑战榜单</h1>
          <p>浏览所有已发布挑战的领先者与完整榜单入口。</p>
        </div>
        <Link className="button" to="/leaderboards">
          返回榜单中心
        </Link>
      </div>

      <div className="leaderboard-challenge-list">
        {index.challenges.map((challenge) => (
          <article className="leaderboard-challenge-card" key={challenge.challengeId}>
            <div className="leaderboard-challenge-main">
              <span className="challenge-status">已发布</span>
              <h2>{challenge.title}</h2>
              <p>{challenge.description ? challenge.description.slice(0, 160) : "暂无简介"}</p>
              <div className="challenge-time">
                <span>开始：{formatDate(challenge.startAt)}</span>
                <span>截止：{formatDate(challenge.endAt)}</span>
              </div>
              <div className="leaderboard-challenge-facts">
                <span>任务数：{challenge.totalTaskCount}</span>
                <span>参与人数：{challenge.participantCount}</span>
                <span>完成人数：{challenge.completedUserCount}</span>
              </div>
            </div>

            <aside className="leaderboard-top-panel">
              <p className="eyebrow">TOP 3</p>
              {challenge.topEntries.length === 0 ? (
                <p className="muted">暂无完成记录</p>
              ) : (
                <ol>
                  {challenge.topEntries.map((entry) => (
                    <li key={entry.userId}>
                      <span className="leaderboard-rank">{entry.rank}</span>
                      <div>
                        <strong>{entry.userName}</strong>
                        <span>
                          {entry.totalScore} 分 / {entry.completedTaskCount} 题
                        </span>
                      </div>
                    </li>
                  ))}
                </ol>
              )}
              <Link className="button" to={`/challenges/${challenge.challengeId}/leaderboard`}>
                查看完整榜单
              </Link>
            </aside>
          </article>
        ))}
      </div>
    </section>
  );
}

function formatDate(value: string | null) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
