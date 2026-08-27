import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  createChallengeTask,
  getChallenge,
  updateChallengeTask,
  type ChallengeDetailDto,
  type ChallengeTaskDifficulty,
  type ChallengeTaskDto,
  type ChallengeTaskType
} from "../api/challengesApi";
import { getProblems, type ProblemListItemDto } from "../api/problemsApi";
import { BoardPositionPicker } from "../components/BoardPositionPicker";
import { MarkdownEditor } from "../components/MarkdownEditor";

export function AdminChallengeTaskEditorPage() {
  const { id, challengeId: challengeIdParam, taskId } = useParams();
  const challengeId = challengeIdParam ?? id;
  const navigate = useNavigate();
  const isEditMode = Boolean(taskId);
  const [challenge, setChallenge] = useState<ChallengeDetailDto | null>(null);
  const [problems, setProblems] = useState<ProblemListItemDto[]>([]);
  const [editingTask, setEditingTask] = useState<ChallengeTaskDto | null>(null);
  const [form, setForm] = useState({
    title: "",
    description: "",
    taskType: 1 as ChallengeTaskType,
    difficulty: 1 as ChallengeTaskDifficulty,
    boardX: 0,
    boardY: 0,
    algorithmProblemId: "",
    score: 0,
    isPublished: false
  });
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!challengeId) {
      return;
    }

    let ignore = false;
    setIsLoading(true);

    Promise.all([getChallenge(challengeId), getProblems()])
      .then(([challengeDetail, problemItems]) => {
        if (ignore) {
          return;
        }

        const task = taskId ? challengeDetail.tasks.find((item) => item.id === taskId) ?? null : null;
        if (taskId && !task) {
          setError("小题不存在。");
        }

        setChallenge(challengeDetail);
        setProblems(problemItems);
        setEditingTask(task);

        if (task) {
          setForm({
            title: task.title,
            description: task.description,
            taskType: task.taskType,
            difficulty: task.difficulty,
            boardX: task.boardX,
            boardY: task.boardY,
            algorithmProblemId: task.algorithmProblemId ?? "",
            score: task.score,
            isPublished: task.isPublished
          });
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "小题编辑数据加载失败");
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
  }, [challengeId, taskId]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!challengeId) {
      setError("Challenge 参数无效。");
      return;
    }

    if (form.boardX < 0 || form.boardX > 7 || form.boardY < 0 || form.boardY > 7) {
      setError("棋盘位置必须在 0 到 7 之间。");
      return;
    }

    if (form.score < 0) {
      setError("分数不能小于 0。");
      return;
    }

    if (form.taskType === 1 && !form.algorithmProblemId) {
      setError("算法题必须选择绑定的 OJ 题目。");
      return;
    }

    setIsSaving(true);
    setError(null);

    try {
      if (taskId) {
        await updateChallengeTask(challengeId, taskId, {
          title: form.title.trim(),
          description: form.description,
          difficulty: form.difficulty,
          boardX: form.boardX,
          boardY: form.boardY,
          algorithmProblemId: form.taskType === 1 ? form.algorithmProblemId : null,
          score: form.score,
          isPublished: form.isPublished
        });
      } else {
        await createChallengeTask(challengeId, {
          title: form.title.trim(),
          description: form.description,
          taskType: form.taskType,
          difficulty: form.difficulty,
          boardX: form.boardX,
          boardY: form.boardY,
          algorithmProblemId: form.taskType === 1 ? form.algorithmProblemId : null,
          score: form.score,
          isPublished: form.isPublished
        });
      }

      navigate(`/admin/challenges/${challengeId}/edit`);
    } catch (err) {
      const message = err instanceof Error ? err.message : "保存小题失败";
      setError(message.includes("Challenge has ended") ? "挑战已结束，非 Root 不可修改。" : message);
    } finally {
      setIsSaving(false);
    }
  }

  if (isLoading) {
    return <div className="state-line">正在加载小题编辑器...</div>;
  }

  if (!challenge || (isEditMode && !editingTask)) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error ?? "小题不存在。"}</div>
        {challengeId && (
          <Link className="button" to={`/admin/challenges/${challengeId}/edit`}>
            返回挑战编辑
          </Link>
        )}
      </section>
    );
  }

  return (
    <section className="challenge-page admin-editor-page ui-v2-page editor-v2-page task-editor-v2-page">
      <div className="leaderboard-header ui-v2-page-header">
        <div>
          <p className="eyebrow">TASK EDITOR</p>
          <h1>{isEditMode ? "编辑小题" : "创建小题"}</h1>
          <p>{challenge.title}</p>
        </div>
        <Link className="button" to={`/admin/challenges/${challenge.id}/edit`}>
          返回挑战编辑
        </Link>
      </div>

      {error && <div className="alert error">{error}</div>}

      <form className="form-stack" onSubmit={handleSubmit}>
        <label>
          标题
          <input
            required
            value={form.title}
            onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
          />
        </label>

        <MarkdownEditor
          required
          label="描述"
          value={form.description}
          onChange={(description) => setForm((current) => ({ ...current, description }))}
        />

        <div className="form-row">
          <label>
            类型
            <select
              disabled={isEditMode}
              value={form.taskType}
              onChange={(event) => setForm((current) => ({
                ...current,
                taskType: Number(event.target.value) as ChallengeTaskType,
                algorithmProblemId: Number(event.target.value) === 2 ? "" : current.algorithmProblemId
              }))}
            >
              <option value={1}>算法题</option>
              <option value={2}>文件题</option>
            </select>
          </label>
          <label>
            难度
            <select
              value={form.difficulty}
              onChange={(event) => setForm((current) => ({ ...current, difficulty: Number(event.target.value) as ChallengeTaskDifficulty }))}
            >
              <option value={1}>兵</option>
              <option value={2}>马</option>
              <option value={3}>象</option>
              <option value={4}>车</option>
              <option value={5}>皇后</option>
              <option value={6}>国王</option>
            </select>
          </label>
        </div>

        {form.taskType === 1 && (
          <label>
            绑定算法题
            <select
              required
              value={form.algorithmProblemId}
              onChange={(event) => setForm((current) => ({ ...current, algorithmProblemId: event.target.value }))}
            >
              <option value="">请选择题目</option>
              {problems.map((problem) => (
                <option key={problem.id} value={problem.id}>
                  {problem.title}
                </option>
              ))}
            </select>
          </label>
        )}

        <div className="form-row">
          <label>
            分数
            <input
              min={0}
              required
              type="number"
              value={form.score}
              onChange={(event) => setForm((current) => ({ ...current, score: Number(event.target.value) }))}
            />
          </label>
          <label className="checkbox-line">
            <input
              type="checkbox"
              checked={form.isPublished}
              onChange={(event) => setForm((current) => ({ ...current, isPublished: event.target.checked }))}
            />
            发布小题
          </label>
        </div>

        <section className="board-picker-section">
          <div>
            <p className="eyebrow">BOARD POSITION</p>
            <h2>棋盘位置</h2>
            <p className="muted">当前选择：{form.boardX}, {form.boardY}</p>
          </div>
          <BoardPositionPicker
            editingTaskId={taskId}
            tasks={challenge.tasks}
            value={{ boardX: form.boardX, boardY: form.boardY }}
            onChange={(position) => setForm((current) => ({ ...current, ...position }))}
          />
        </section>

        <div className="button-row">
          <button className="button primary" disabled={isSaving} type="submit">
            {isSaving ? "保存中..." : "保存小题"}
          </button>
        </div>
      </form>
    </section>
  );
}
