import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { getChallenge, type ChallengeDetailDto, type ChallengeTaskDto } from "../api/challengesApi";
import { MarkdownRenderer } from "../components/MarkdownRenderer";

const difficultyNames = {
  1: "Pawn",
  2: "Knight",
  3: "Bishop",
  4: "Rook",
  5: "Queen",
  6: "King"
} as const;

export function ChallengeTaskDetailPage() {
  const { challengeId, taskId } = useParams();
  const [challenge, setChallenge] = useState<ChallengeDetailDto | null>(null);
  const [task, setTask] = useState<ChallengeTaskDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!challengeId || !taskId) {
      return;
    }

    getChallenge(challengeId)
      .then((detail) => {
        setChallenge(detail);
        setTask(detail.tasks.find((item) => item.id === taskId) ?? null);
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "加载小题失败"));
  }, [challengeId, taskId]);

  if (error) {
    return <div className="alert error">{error}</div>;
  }

  if (!challenge || !task) {
    return <div className="state-line">正在加载小题...</div>;
  }

  return (
    <section className="challenge-page task-detail-layout ui-v2-page challenge-task-v2-page">
      <article className="task-statement">
        <Link className="subtle-link" to={`/challenges/${challenge.id}`}>
          返回棋盘
        </Link>
        <p className="eyebrow">{challenge.title}</p>
        <h1>{task.title}</h1>
        <MarkdownRenderer value={task.description} />
      </article>

      <aside className="task-meta-panel">
        <p className="eyebrow">TASK</p>
        <dl>
          <div>
            <dt>类型</dt>
            <dd>{task.taskType === 1 ? "算法题" : "文件题"}</dd>
          </div>
          <div>
            <dt>难度</dt>
            <dd>{difficultyNames[task.difficulty]}</dd>
          </div>
          <div>
            <dt>分数</dt>
            <dd>{task.score}</dd>
          </div>
          <div>
            <dt>棋盘位置</dt>
            <dd>{task.boardX}, {task.boardY}</dd>
          </div>
        </dl>

        {task.taskType === 1 && task.algorithmProblemId ? (
          <Link className="button primary" to={`/problems/${task.algorithmProblemId}`}>
            进入算法题
          </Link>
        ) : (
          <div className="quiet-note">文件题请从棋盘进入答题页</div>
        )}
      </aside>
    </section>
  );
}
