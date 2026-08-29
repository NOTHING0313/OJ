import { FormEvent, useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import {
  createChallenge,
  deleteChallengeTask,
  getChallenge,
  updateChallenge,
  type ChallengeDetailDto,
  type SaveChallengeRequest
} from "../api/challengesApi";
import { MarkdownEditor } from "../components/MarkdownEditor";
import { getAdminLeaderboardSeasons, type LeaderboardSeason } from "../api/leaderboardsApi";

export function AdminChallengeEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const isEditMode = Boolean(id);
  const seasonId = isEditMode ? null : searchParams.get("seasonId");
  const startInputRef = useRef<HTMLInputElement | null>(null);
  const endInputRef = useRef<HTMLInputElement | null>(null);
  const peerReviewEndInputRef = useRef<HTMLInputElement | null>(null);
  const [challenge, setChallenge] = useState<ChallengeDetailDto | null>(null);
  const [form, setForm] = useState({
    title: "",
    description: "",
    startAt: toDateTimeLocalValue(new Date().toISOString()),
    endAt: toDateTimeLocalValue(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString()),
    isPublished: false,
    participationMode: 1 as 1 | 2,
    peerReviewEnabled: false,
    peerReviewEndAt: ""
  });
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(isEditMode);
  const [isSaving, setIsSaving] = useState(false);
  const [linkedSeason, setLinkedSeason] = useState<LeaderboardSeason | null>(null);
  const [deletingTaskId, setDeletingTaskId] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      return;
    }

    let ignore = false;
    setIsLoading(true);

    getChallenge(id)
      .then((detail) => {
        if (!ignore) {
          setChallenge(detail);
          setForm({
            title: detail.title,
            description: detail.description,
            startAt: toDateTimeLocalValue(detail.startAt),
            endAt: toDateTimeLocalValue(detail.endAt),
            isPublished: detail.isPublished,
            participationMode: detail.participationMode,
            peerReviewEnabled: detail.peerReviewEnabled,
            peerReviewEndAt: detail.peerReviewEndAt ? toDateTimeLocalValue(detail.peerReviewEndAt) : ""
          });
          setError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "挑战加载失败");
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
  }, [id]);

  useEffect(() => {
    if (!seasonId) return;
    let ignore = false;
    getAdminLeaderboardSeasons().then((items) => {
      const season = items.find((item) => item.id === seasonId && item.effectiveStatus === 1) ?? null;
      if (!ignore && season) {
        setLinkedSeason(season);
        setForm((current) => ({ ...current, startAt: toDateTimeLocalValue(season.startAt), endAt: toDateTimeLocalValue(season.freezeAt) }));
      }
    }).catch((err: unknown) => { if (!ignore) setError(err instanceof Error ? err.message : "赛季配置加载失败"); });
    return () => { ignore = true; };
  }, [seasonId]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setNotice(null);

    const validationError = validateTimeRange(form.startAt, form.endAt);
    if (validationError) {
      setError(validationError);
      setIsSaving(false);
      return;
    }
    if (linkedSeason && (new Date(form.startAt) < new Date(linkedSeason.startAt) || new Date(form.endAt) > new Date(linkedSeason.freezeAt))) {
      setError("关联挑战时间必须位于赛季开始与冻结时间之间");
      setIsSaving(false);
      return;
    }

    if (form.peerReviewEnabled && (!form.peerReviewEndAt || new Date(form.peerReviewEndAt).getTime() <= new Date(form.endAt).getTime())) {
      setError("互评截止时间必须晚于挑战截止时间");
      setIsSaving(false);
      return;
    }

    const peerReviewEnabled = challenge?.peerReviewConfigurationLocked ? challenge.peerReviewEnabled : form.peerReviewEnabled;
    const peerReviewEndAt = challenge?.peerReviewConfigurationLocked
      ? challenge.peerReviewEndAt
      : form.peerReviewEnabled ? toIsoString(form.peerReviewEndAt) : null;
    const payload: SaveChallengeRequest = {
      title: form.title.trim(),
      description: form.description,
      startAt: toIsoString(form.startAt),
      endAt: toIsoString(form.endAt),
      isPublished: form.isPublished,
      participationMode: form.participationMode,
      peerReviewEnabled,
      peerReviewEndAt,
      seasonId
    };

    try {
      if (id) {
        const updated = await updateChallenge(id, payload);
        setChallenge(updated);
        setNotice("挑战已保存。");
      } else {
        const created = await createChallenge(payload);
        navigate(`/admin/challenges/${created.id}/edit`);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : "保存挑战失败";
      setError(message.includes("Challenge has ended") ? "挑战已结束，非 Root 不可修改。" : message);
    } finally {
      setIsSaving(false);
    }
  }

  async function handleDeleteTask(taskId: string) {
    if (!id || !window.confirm("确定删除这个小题吗？")) {
      return;
    }

    try {
      setDeletingTaskId(taskId);
      await deleteChallengeTask(id, taskId);
      setChallenge((current) => current ? { ...current, tasks: current.tasks.filter((task) => task.id !== taskId) } : current);
      setNotice("小题已删除。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "删除小题失败");
    } finally {
      setDeletingTaskId(null);
    }
  }

  function openDateTimePicker(input: HTMLInputElement | null) {
    const picker = input as (HTMLInputElement & { showPicker?: () => void }) | null;
    picker?.showPicker?.();
  }

  function setStartOffset(days: number) {
    setForm((current) => ({ ...current, startAt: toDateTimeLocalValue(new Date(Date.now() + days * 24 * 60 * 60 * 1000).toISOString()) }));
  }

  function setEndAfterStart(days: number) {
    const start = form.startAt ? new Date(form.startAt) : new Date();
    setForm((current) => ({ ...current, endAt: toDateTimeLocalValue(new Date(start.getTime() + days * 24 * 60 * 60 * 1000).toISOString()) }));
  }

  if (isLoading) {
    return <div className="state-line">正在加载挑战...</div>;
  }

  if (error && isEditMode && !challenge) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to="/admin/challenges">
          返回挑战管理
        </Link>
      </section>
    );
  }

  return (
    <section className="challenge-page admin-editor-page ui-v2-page editor-v2-page challenge-editor-v2-page">
      <div className="leaderboard-header ui-v2-page-header">
        <div>
          <p className="eyebrow">CHALLENGE EDITOR</p>
          <h1>{isEditMode ? "编辑挑战" : "创建挑战"}</h1>
          <p>配置大题目名称、说明、开放时间和发布状态。</p>
        </div>
        <div className="button-row">
          <Link className="button" to="/admin/challenges">
            返回管理列表
          </Link>
          {challenge && (
            <Link className="button" to={`/challenges/${challenge.id}`}>
              查看棋盘
            </Link>
          )}
        </div>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <form className="form-stack" onSubmit={handleSubmit}>
        {linkedSeason && <div className="quiet-note">将关联到榜单赛季：{linkedSeason.name}。挑战时间不得超出赛季范围。</div>}
        <label>
          标题
          <input required value={form.title} onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))} />
        </label>
        <MarkdownEditor required label="描述" value={form.description} onChange={(description) => setForm((current) => ({ ...current, description }))} />
        <div className="form-row">
          <label>
            开始时间
            <input
              ref={startInputRef}
              required
              type="datetime-local"
              value={form.startAt}
              onClick={() => openDateTimePicker(startInputRef.current)}
              onFocus={() => openDateTimePicker(startInputRef.current)}
              onChange={(event) => setForm((current) => ({ ...current, startAt: event.target.value }))}
            />
          </label>
          <label>
            截止时间
            <input
              ref={endInputRef}
              required
              type="datetime-local"
              value={form.endAt}
              onClick={() => openDateTimePicker(endInputRef.current)}
              onFocus={() => openDateTimePicker(endInputRef.current)}
              onChange={(event) => setForm((current) => ({ ...current, endAt: event.target.value }))}
            />
          </label>
        </div>
        <div className="button-row">
          <button className="button" type="button" onClick={() => setStartOffset(0)}>
            今天开始
          </button>
          <button className="button" type="button" onClick={() => setStartOffset(1)}>
            明天开始
          </button>
          <button className="button" type="button" onClick={() => setEndAfterStart(7)}>
            7 天后截止
          </button>
          <button className="button" type="button" onClick={() => setEndAfterStart(30)}>
            30 天后截止
          </button>
        </div>
        <label className="checkbox-line">
          <input type="checkbox" checked={form.isPublished} onChange={(event) => setForm((current) => ({ ...current, isPublished: event.target.checked }))} />
          发布挑战
        </label>
        <label>
          参与模式
          <select
            value={form.participationMode}
            disabled={challenge?.participationModeLocked}
            onChange={(event) => {
              const participationMode = Number(event.target.value) as 1 | 2;
              setForm((current) => ({
                ...current,
                participationMode,
                peerReviewEnabled: participationMode === 2 && current.peerReviewEnabled
              }));
            }}
          >
            <option value={1}>个人挑战</option>
            <option value={2}>战队挑战（仅算法题）</option>
          </select>
          {challenge?.participationModeLocked && <small>挑战发布、开始或产生参与/提交后，参与模式不可更改。</small>}
        </label>
        {form.participationMode === 2 && (
          <div className="admin-panel">
            <label className="checkbox-line">
              <input
                type="checkbox"
                checked={form.peerReviewEnabled}
                disabled={challenge?.peerReviewConfigurationLocked}
                onChange={(event) => setForm((current) => ({ ...current, peerReviewEnabled: event.target.checked }))}
              />
              启用战队项目互评
            </label>
            {form.peerReviewEnabled && (
              <label>
                互评截止时间
                <input
                  ref={peerReviewEndInputRef}
                  required
                  type="datetime-local"
                  value={form.peerReviewEndAt}
                  disabled={challenge?.peerReviewConfigurationLocked}
                  onClick={() => openDateTimePicker(peerReviewEndInputRef.current)}
                  onFocus={() => openDateTimePicker(peerReviewEndInputRef.current)}
                  onChange={(event) => setForm((current) => ({ ...current, peerReviewEndAt: event.target.value }))}
                />
              </label>
            )}
            <small>互评仅适用于战队算法挑战；发布、开始或有战队报名后配置被冻结。</small>
          </div>
        )}
        <div className="button-row">
          <button className="button primary" disabled={isSaving} type="submit">
            {isSaving ? "保存中..." : "保存"}
          </button>
        </div>
      </form>

      {challenge && (
        <section className="admin-panel">
          <div className="admin-panel-header">
            <p className="eyebrow">TASKS</p>
            <h2>小题管理</h2>
            <Link className="button" to={`/admin/challenges/${challenge.id}/tasks/new`}>
              新建小题
            </Link>
          </div>
          {challenge.tasks.length === 0 ? (
            <div className="empty-state">暂无小题</div>
          ) : (
            <div className="table-wrap leaderboard-table-wrap">
              <table className="leaderboard-table">
                <thead>
                  <tr>
                    <th>题目</th>
                    <th>类型</th>
                    <th>难度</th>
                    <th>位置</th>
                    <th>分数</th>
                    <th>发布</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {challenge.tasks.map((task) => (
                    <tr key={task.id}>
                      <td>{task.title}</td>
                      <td>{task.taskType === 1 ? "算法题" : "文件题"}</td>
                      <td>{difficultyNames[task.difficulty]}</td>
                      <td>{task.boardX}, {task.boardY}</td>
                      <td>{task.score}</td>
                      <td>{task.isPublished ? "是" : "否"}</td>
                      <td>
                        <div className="table-actions">
                          <Link className="button" to={`/admin/challenges/${challenge.id}/tasks/${task.id}/edit`}>
                            编辑
                          </Link>
                          <button className="button" disabled={deletingTaskId === task.id} type="button" onClick={() => handleDeleteTask(task.id)}>
                            {deletingTaskId === task.id ? "删除中..." : "删除"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      )}
    </section>
  );
}

const difficultyNames = {
  1: "兵",
  2: "马",
  3: "象",
  4: "车",
  5: "皇后",
  6: "国王"
} as const;

function validateTimeRange(startAt: string, endAt: string) {
  if (!startAt) {
    return "开始时间不能为空";
  }

  if (!endAt) {
    return "截止时间不能为空";
  }

  if (new Date(endAt).getTime() <= new Date(startAt).getTime()) {
    return "截止时间必须晚于开始时间";
  }

  return null;
}

function toDateTimeLocalValue(value: string) {
  const date = new Date(value);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function toIsoString(value: string) {
  return new Date(value).toISOString();
}
