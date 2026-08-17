import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { getChallenges, type ChallengeListItemDto } from "../api/challengesApi";

export function ChallengeListPage() {
  const [challenges, setChallenges] = useState<ChallengeListItemDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let ignore = false;

    getChallenges()
      .then((items) => {
        if (!ignore) {
          setChallenges(items);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "加载挑战失败");
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
    return <div className="state-line">正在加载挑战...</div>;
  }

  if (error) {
    return <div className="alert error">{error}</div>;
  }

  return (
    <section className="challenge-page">
      <div className="challenge-hero">
        <p className="eyebrow">UNREALSTUDIO CHALLENGES</p>
        <h1>挑战</h1>
        <p>安静地进入棋盘。选择一个格子，解开它背后的题目。</p>
      </div>

      <div className="challenge-list">
        {challenges.map((challenge) => (
          <article className="challenge-card" key={challenge.id}>
            <Link className="challenge-card-main" to={`/challenges/${challenge.id}`}>
              <span className="challenge-status">{challenge.isPublished ? "开放" : "未发布"}</span>
              <h2>{challenge.title}</h2>
              <p>{toSummary(challenge.description)}</p>
            </Link>
            <div className="challenge-card-meta">
              <span>{formatDate(challenge.startAt)}</span>
              <span>{formatDate(challenge.endAt)}</span>
              <strong>
                {challenge.totalTaskCount === 0
                  ? "暂无任务"
                  : `${challenge.completedTaskCount} / ${challenge.totalTaskCount}`}
              </strong>
              <Link className="subtle-link leaderboard-card-link" to={`/challenges/${challenge.id}/leaderboard`}>
                排行榜
              </Link>
              {challenge.canManage && (
                <Link className="subtle-link leaderboard-card-link" to={`/challenges/${challenge.id}/admin`}>
                  管理统计
                </Link>
              )}
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}

function toSummary(value: string) {
  const plain = value
    .replace(/[#*_`>|[\]()]/g, "")
    .replace(/\s+/g, " ")
    .trim();

  return plain.length > 120 ? `${plain.slice(0, 120)}...` : plain || "暂无描述";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
