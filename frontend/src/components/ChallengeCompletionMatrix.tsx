import type { ChallengeLeaderboardProgress } from "../api/challengesApi";

export function ChallengeCompletionMatrix({ progress }: { progress: ChallengeLeaderboardProgress | null }) {
  return (
    <section className="challenge-progress-card">
      <div className="challenge-progress-header">
        <div>
          <p className="eyebrow">PARTICIPANT PROGRESS</p>
          <h2>参与者完成情况</h2>
          <p>展示挑战内每位参与者对各任务的完成状态，未上榜但已参与的用户也会保留。</p>
        </div>
        <span className="leaderboard-total">{progress ? `${progress.users.length} 名参与者 · ${progress.tasks.length} 个任务` : "数据暂不可用"}</span>
      </div>

      {!progress ? (
        <div className="rank-history-empty">完成情况接口暂不可用，当前排行榜仍可正常使用。</div>
      ) : progress.users.length === 0 ? (
        <div className="rank-history-empty">暂无参与者。</div>
      ) : (
        <div className="challenge-progress-table-wrap">
          <table className="challenge-progress-table">
            <thead>
              <tr>
                <th className="challenge-progress-user-column">用户</th>
                <th>名次</th>
                {progress.tasks.map((task, index) => (
                  <th className="challenge-progress-task-column" key={task.taskId} title={task.title}>
                    <span>T{index + 1}</span>
                    <small>{task.title}</small>
                    <em>{task.score} 分</em>
                  </th>
                ))}
                <th className="challenge-progress-summary-column">完成进度</th>
                <th>总分</th>
              </tr>
            </thead>
            <tbody>
              {progress.users.map((user) => {
                const completedTaskIds = new Set(user.completedTaskIds);
                const percent = progress.tasks.length === 0 ? 0 : Math.round((user.completedTaskCount / progress.tasks.length) * 100);
                return (
                  <tr className={user.isCurrentUser ? "leaderboard-current-user" : ""} key={user.userId}>
                    <td>
                      <div className="leaderboard-user challenge-progress-user">
                        {user.avatarUrl ? (
                          <img src={user.avatarUrl} alt={user.userName} />
                        ) : (
                          <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
                        )}
                        <span>
                          <strong>{user.userName}</strong>
                          <small>{user.isCurrentUser ? "当前用户" : formatDate(user.lastCompletedAt)}</small>
                        </span>
                      </div>
                    </td>
                    <td>{user.rank ? <span className={`leaderboard-rank ${getRankClass(user.rank)}`}>{user.rank}</span> : <span className="challenge-progress-unranked">—</span>}</td>
                    {progress.tasks.map((task) => {
                      const completed = completedTaskIds.has(task.taskId);
                      const earnedScore = user.taskScores?.[task.taskId] ?? 0;
                      const hasProgress = earnedScore > 0;
                      return (
                        <td className="challenge-progress-status-cell" key={task.taskId}>
                          <span className={`challenge-progress-status ${completed ? "is-completed" : hasProgress ? "is-partial" : "is-pending"}`} title={`${task.title}：${earnedScore} / ${task.score} 分${completed ? "，已完成" : ""}`}>
                            {completed ? "✓" : hasProgress ? `${earnedScore}` : "—"}
                          </span>
                        </td>
                      );
                    })}
                    <td>
                      <div className="challenge-progress-summary">
                        <span>
                          <strong>{user.completedTaskCount}</strong> / {progress.tasks.length}
                        </span>
                        <div className="challenge-progress-bar">
                          <i style={{ width: `${percent}%` }} />
                        </div>
                        <small>{percent}%</small>
                      </div>
                    </td>
                    <td><strong className="leaderboard-score">{user.totalScore}</strong></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
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
