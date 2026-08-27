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

  const topEntry = leaderboard.entries[0];
  const completedTaskTotal = leaderboard.entries.reduce((sum, entry) => sum + entry.completedTaskCount, 0);

  return (
    <section className="challenge-page leaderboard-page leaderboard-v2-page">
      <div className="leaderboard-header leaderboard-v2-header">
        <div>
          <p className="eyebrow">CHALLENGE LEADERBOARD</p>
          <h1>{leaderboard.challengeTitle}</h1>
          <p>查看当前挑战中所有完成用户的排名和得分情况。</p>
        </div>
        <div className="leaderboard-header-actions">
          <span className="leaderboard-total">共 {leaderboard.entries.length} 名用户</span>
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
        <div className="leaderboard-v2-table-wrap">
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
                <tr className={entry.isCurrentUser ? "leaderboard-current-user" : ""} key={entry.userId}>
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
