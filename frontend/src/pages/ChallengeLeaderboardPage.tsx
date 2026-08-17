import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getChallengeLeaderboard, type ChallengeLeaderboard } from "../api/challengesApi";

export function ChallengeLeaderboardPage() {
  const { challengeId } = useParams();
  const [leaderboard, setLeaderboard] = useState<ChallengeLeaderboard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!challengeId) {
      return;
    }

    let ignore = false;

    getChallengeLeaderboard(challengeId)
      .then((data) => {
        if (!ignore) {
          setLeaderboard(data);
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

  return (
    <section className="challenge-page leaderboard-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARD</p>
          <h1>{leaderboard.challengeTitle}</h1>
          <p>
            总任务数：{leaderboard.totalTaskCount} / 上榜人数：{leaderboard.entries.length}
          </p>
        </div>
        <Link className="button" to={`/challenges/${leaderboard.challengeId}`}>
          返回棋盘
        </Link>
      </div>

      {leaderboard.entries.length === 0 ? (
        <div className="empty-state">暂无完成记录</div>
      ) : (
        <div className="table-wrap leaderboard-table-wrap">
          <table className="leaderboard-table">
            <thead>
              <tr>
                <th>排名</th>
                <th>用户</th>
                <th>完成题数</th>
                <th>总分</th>
                <th>最后完成时间</th>
              </tr>
            </thead>
            <tbody>
              {leaderboard.entries.map((entry) => (
                <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={entry.userId}>
                  <td>
                    <span className={`leaderboard-rank ${getRankClass(entry.rank)}`}>{entry.rank}</span>
                  </td>
                  <td>
                    <div className="leaderboard-user">
                      {entry.avatarUrl ? (
                        <img src={entry.avatarUrl} alt={entry.userName} />
                      ) : (
                        <span className="leaderboard-avatar-placeholder">{entry.userName.slice(0, 1).toUpperCase()}</span>
                      )}
                      <span>{entry.userName}</span>
                    </div>
                  </td>
                  <td>
                    {entry.completedTaskCount} / {leaderboard.totalTaskCount}
                  </td>
                  <td>{entry.totalScore}</td>
                  <td>{formatDate(entry.lastCompletedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
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
