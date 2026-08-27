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
    <section className="challenge-page ui-v2-page challenge-list-v2-page challenge-list-v8-page">
      <div className="challenge-hero challenge-list-header">
        <div>
          <p className="eyebrow">UNREALSTUDIO CHALLENGES</p>
          <h1>挑战</h1>
          <p>进入棋盘、完成任务并推进自己的挑战进度。</p>
        </div>
        <span className="context-chip">共 {challenges.length} 个挑战</span>
      </div>

      {challenges.length === 0 ? (
        <div className="empty-state">当前暂无开放挑战</div>
      ) : (
        <div className="challenge-list">
          {challenges.map((challenge) => {
            const progress = getProgress(challenge.completedTaskCount, challenge.totalTaskCount);

            return (
              <article className="challenge-card" key={challenge.id}>
                <Link className="challenge-card-main" to={`/challenges/${challenge.id}`}>
                  <div className="challenge-card-heading">
                    <span className={`challenge-status ${challenge.isPublished ? "open" : "draft"}`}>
                      {challenge.isPublished ? "开放" : "未发布"}
                    </span>
                    <span className="challenge-card-kicker">棋盘挑战</span>
                  </div>
                  <h2>{challenge.title}</h2>
                  <p>{toSummary(challenge.description)}</p>
                  <div className="challenge-card-time-grid">
                    <div>
                      <span>开始时间</span>
                      <strong>{formatDate(challenge.startAt)}</strong>
                    </div>
                    <div>
                      <span>截止时间</span>
                      <strong>{formatDate(challenge.endAt)}</strong>
                    </div>
                  </div>
                </Link>

                <div className="challenge-card-meta">
                  <div className="challenge-card-progress-head">
                    <div>
                      <span>任务进度</span>
                      <strong>
                        {challenge.totalTaskCount === 0
                          ? "暂无任务"
                          : `${challenge.completedTaskCount} / ${challenge.totalTaskCount}`}
                      </strong>
                    </div>
                    <span>{progress}%</span>
                  </div>
                  <div className="challenge-progress-track" aria-hidden="true">
                    <span style={{ width: `${progress}%` }} />
                  </div>

                  <div className="challenge-card-actions">
                    <Link className="button primary" to={`/challenges/${challenge.id}`}>
                      进入挑战
                    </Link>
                    <Link className="button" to={`/challenges/${challenge.id}/leaderboard`}>
                      排行榜
                    </Link>
                    {challenge.canManage && (
                      <Link className="button" to={`/challenges/${challenge.id}/admin`}>
                        管理统计
                      </Link>
                    )}
                  </div>
                </div>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}

function getProgress(completed: number, total: number) {
  if (total <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((completed / total) * 100)));
}

function toSummary(value: string) {
  const plain = value
    .replace(/[#*_`>|[\]()]/g, "")
    .replace(/\s+/g, " ")
    .trim();

  return plain.length > 140 ? `${plain.slice(0, 140)}...` : plain || "暂无描述";
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(value));
}
