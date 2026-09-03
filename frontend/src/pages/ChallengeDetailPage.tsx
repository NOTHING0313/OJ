import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { getChallenge, getChallengePeerReview, joinChallenge, registerChallengeTeam, type ChallengeDetailDto, type ChallengePeerReviewWorkspace, type ChallengeTaskDto } from "../api/challengesApi";
import { getMyTeam, type TeamProjectDto } from "../api/teamsApi";
import { useAuth } from "../auth/AuthContext";
import { canManageContent } from "../auth/roles";
import { MarkdownRenderer } from "../components/MarkdownRenderer";

const difficultySymbols = {
  1: "♙",
  2: "♘",
  3: "♗",
  4: "♖",
  5: "♕",
  6: "♔"
} as const;

const difficultyNames = {
  1: "兵",
  2: "马",
  3: "象",
  4: "车",
  5: "皇后",
  6: "国王"
} as const;

const breakFragments = Array.from({ length: 10 }, (_, index) => index + 1);

export function ChallengeDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { currentUser } = useAuth();
  const [challenge, setChallenge] = useState<ChallengeDetailDto | null>(null);
  const [selectedTask, setSelectedTask] = useState<ChallengeTaskDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [joinWarning, setJoinWarning] = useState<string | null>(null);
  const [joinedChallengeId, setJoinedChallengeId] = useState<string | null>(null);
  const [breakingTaskId, setBreakingTaskId] = useState<string | null>(null);
  const [visualCompletedTaskIds, setVisualCompletedTaskIds] = useState<Set<string>>(() => new Set());
  const consumedAnimationNonceRef = useRef<string | number | null>(null);
  const animationTimerRef = useRef<number | null>(null);
  const pieceRefs = useRef<Record<string, HTMLButtonElement | null>>({});
  const [focusedTaskId, setFocusedTaskId] = useState<string | null>(null);
  const [teamProjects, setTeamProjects] = useState<TeamProjectDto[]>([]);
  const [selectedTeamProjectId, setSelectedTeamProjectId] = useState("");
  const [peerReviewWorkspace, setPeerReviewWorkspace] = useState<ChallengePeerReviewWorkspace | null>(null);

  useEffect(() => () => {
    if (animationTimerRef.current) {
      window.clearTimeout(animationTimerRef.current);
    }
  }, []);

  useEffect(() => {
    if (!id) {
      return;
    }

    getChallenge(id)
      .then((detail) => {
        setChallenge(detail);
        setSelectedTask((current) => {
          if (!current) {
            return detail.tasks[0] ?? null;
          }

          return detail.tasks.find((task) => idsEqual(task.id, current.id)) ?? detail.tasks[0] ?? null;
        });
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "加载挑战失败"));
  }, [id]);

  useEffect(() => {
    if (!currentUser || !challenge || joinedChallengeId === challenge.id) {
      return;
    }

    setJoinedChallengeId(challenge.id);
    if (challenge.participationMode === 2) return;
    joinChallenge(challenge.id).catch((err: unknown) => {
      const message = err instanceof Error ? err.message : "加入挑战失败";
      if (message.includes("Forbidden") || message.includes("blacklisted") || message.includes("黑名单")) {
        setJoinWarning("当前账号无法加入该挑战。");
      }
    });
  }, [challenge, currentUser, joinedChallengeId]);

  useEffect(() => {
    if (!currentUser || !challenge?.peerReviewEnabled || !challenge.teamParticipation?.canRegisterTeam) {
      return;
    }

    let ignore = false;
    getMyTeam()
      .then((team) => {
        if (ignore) return;
        const projects = team?.projects ?? [];
        setTeamProjects(projects);
        setSelectedTeamProjectId((current) => current || projects[0]?.id || "");
      })
      .catch((err: unknown) => {
        if (!ignore) setJoinWarning(err instanceof Error ? err.message : "战队项目加载失败");
      });
    return () => { ignore = true; };
  }, [challenge?.id, challenge?.peerReviewEnabled, challenge?.teamParticipation?.canRegisterTeam, currentUser]);

  useEffect(() => {
    if (!challenge?.peerReviewEnabled || !challenge.teamParticipation?.isRosterMember || new Date() < new Date(challenge.endAt)) {
      return;
    }
    let ignore = false;
    getChallengePeerReview(challenge.id)
      .then((workspace) => { if (!ignore) setPeerReviewWorkspace(workspace); })
      .catch((err: unknown) => { if (!ignore) setJoinWarning(err instanceof Error ? err.message : "互评任务加载失败"); });
    return () => { ignore = true; };
  }, [challenge?.endAt, challenge?.id, challenge?.peerReviewEnabled, challenge?.teamParticipation?.isRosterMember]);

  useEffect(() => {
    if (!challenge) {
      return;
    }

    const state = location.state as { completedTaskId?: string; playBreakAnimation?: boolean; animationNonce?: string | number } | null;
    if (!state?.playBreakAnimation || !state.completedTaskId) {
      return;
    }

    const completedTaskId = normalizeTaskId(state.completedTaskId);
    const animationNonce = state.animationNonce ?? location.key;
    if (consumedAnimationNonceRef.current === animationNonce) {
      return;
    }

    const completedTask = challenge.tasks.find((task) => idsEqual(task.id, completedTaskId));
    if (!completedTask) {
      navigate(location.pathname, { replace: true, state: null });
      return;
    }

    consumedAnimationNonceRef.current = animationNonce;
    const taskId = normalizeTaskId(completedTask.id);
    const prefersReducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;

    if (animationTimerRef.current) {
      window.clearTimeout(animationTimerRef.current);
    }

    setSelectedTask(completedTask);
    setBreakingTaskId(taskId);
    setFocusedTaskId(taskId);
    setVisualCompletedTaskIds((current) => {
      const next = new Set(current);
      next.add(taskId);
      return next;
    });

    window.requestAnimationFrame(() => {
      const element = pieceRefs.current[taskId];
      element?.focus({ preventScroll: true });
      element?.scrollIntoView({
        behavior: prefersReducedMotion ? "auto" : "smooth",
        block: "center",
        inline: "center"
      });
    });

    navigate(location.pathname, { replace: true, state: null });

    animationTimerRef.current = window.setTimeout(() => {
      setBreakingTaskId((current) => current && idsEqual(current, taskId) ? null : current);
      setVisualCompletedTaskIds((current) => {
        const next = new Set(current);
        next.add(taskId);
        return next;
      });

      if (id) {
        getChallenge(id)
          .then((detail) => {
            setChallenge(detail);
            setSelectedTask((current) => {
              if (!current) {
                return detail.tasks.find((task) => idsEqual(task.id, taskId)) ?? detail.tasks[0] ?? null;
              }

              return detail.tasks.find((task) => idsEqual(task.id, current.id)) ?? detail.tasks[0] ?? null;
            });
          })
          .catch(() => {
            // Keep the optimistic visual completion if a transient refresh fails.
          });
      }
    }, prefersReducedMotion ? 0 : 950);
  }, [challenge, id, location.key, location.pathname, location.state, navigate]);

  const taskMap = useMemo(() => {
    const map = new Map<string, ChallengeTaskDto>();
    challenge?.tasks.forEach((task) => map.set(`${task.boardX}:${task.boardY}`, task));
    return map;
  }, [challenge]);

  function handleTaskClick(task: ChallengeTaskDto) {
    if (task.taskType === 1 && task.algorithmProblemId) {
      navigate(`/problems/${task.algorithmProblemId}?challengeId=${task.challengeId}&taskId=${task.id}`);
      return;
    }

    if (task.taskType === 2) {
      navigate(`/challenges/${task.challengeId}/tasks/${task.id}/answer`);
    }
  }

  if (error) {
    const isMissingChallenge = error.toLowerCase().includes("not found") || error.includes("不存在") || error.includes("404");

    return (
      <section className="page-section narrow">
        <div className="alert error">{isMissingChallenge ? "挑战不存在或已被删除" : error}</div>
        <div className="button-row">
          <button className="button" type="button" onClick={() => navigate("/challenges")}>
            返回挑战列表
          </button>
          <button className="button" type="button" onClick={() => navigate("/admin/challenges")}>
            返回挑战管理
          </button>
        </div>
      </section>
    );
  }

  if (!challenge) {
    return <div className="state-line">正在加载挑战...</div>;
  }

  const canOpenAdminSummary = challenge.canManage;
  const canAuditPeerReviews = challenge.peerReviewEnabled && canManageContent(currentUser?.role);
  const challengeIdForRegistration = challenge.id;
  const peerReviewEnabledForRegistration = challenge.peerReviewEnabled;

  async function registerTeam() {
    try {
      const teamParticipation = await registerChallengeTeam(
        challengeIdForRegistration,
        peerReviewEnabledForRegistration ? selectedTeamProjectId : undefined
      );
      setChallenge((current) => current ? { ...current, teamParticipation } : current);
      setJoinWarning(null);
    } catch (err) {
      setJoinWarning(err instanceof Error ? err.message : "战队报名失败");
    }
  }

  return (
    <section className="challenge-page ui-v2-page challenge-detail-v2-page challenge-detail-v8-page">
      <header className="challenge-detail-header-v8">
        <div className="challenge-detail-title-v8">
          <p className="eyebrow">BOARD CHALLENGE</p>
          <h1>{challenge.title}</h1>
          {joinWarning && <div className="alert error">{joinWarning}</div>}
          <div className="challenge-description-v8">
            <MarkdownRenderer value={challenge.description} />
          </div>
          {challenge.participationMode === 2 && (
            <div className="quiet-note">
              <strong>战队挑战</strong>
              {challenge.teamParticipation?.isRosterMember
                ? (
                  <span>
                    已随「{challenge.teamParticipation.teamName}」报名 · 冻结阵容 {challenge.teamParticipation.rosterMemberCount} 人
                    {challenge.teamParticipation.projectName ? ` · 项目「${challenge.teamParticipation.projectName}」` : ""}
                  </span>
                )
                : challenge.teamParticipation?.id
                  ? <span>你的当前战队「{challenge.teamParticipation.teamName}」已报名，但你未被登记在本场挑战的冻结参赛阵容中。</span>
                : challenge.teamParticipation?.canRegisterTeam
                  ? challenge.peerReviewEnabled
                    ? (
                      <div className="form-stack">
                        {teamProjects.length > 0 ? (
                          <>
                            <label>
                              选择本场互评项目
                              <select value={selectedTeamProjectId} onChange={(event) => setSelectedTeamProjectId(event.target.value)}>
                                {teamProjects.map((project) => <option key={project.id} value={project.id}>{project.name}</option>)}
                              </select>
                            </label>
                            <button className="button primary" type="button" onClick={() => void registerTeam()}>绑定项目并报名</button>
                          </>
                        ) : <span>互评挑战报名需要先在战队页创建 Git 项目。</span>}
                      </div>
                    )
                    : <button className="button primary" type="button" onClick={() => void registerTeam()}>以我的战队报名</button>
                  : <span>等待队长报名；若你尚无战队，需要先加入或创建战队。报名阵容将被冻结。</span>}
            </div>
          )}
          {challenge.peerReviewEnabled && challenge.teamParticipation?.isRosterMember && new Date() >= new Date(challenge.endAt) && (
            <div className="quiet-note">
              <strong>赛后互评</strong>
              {!peerReviewWorkspace
                ? <span>正在加载互评任务...</span>
                : !peerReviewWorkspace.assignmentReady
                  ? <span>{peerReviewWorkspace.insufficientTeams ? "参赛战队不足，无法进行互评。" : "正在生成互评任务。"}</span>
                  : (
                    <>
                      <span>评审战队：{peerReviewWorkspace.targetTeamName} · 项目：{peerReviewWorkspace.targetProjectName}</span>
                      {peerReviewWorkspace.targetRepositoryUrl && <a href={peerReviewWorkspace.targetRepositoryUrl} target="_blank" rel="noreferrer noopener">查看仓库</a>}
                      <button className="button primary" type="button" onClick={() => navigate(`/challenges/${challenge.id}/peer-review`)}>开始评审</button>
                    </>
                  )}
              {challenge.peerReviewEndAt && <span className="muted">截止：{formatDate(challenge.peerReviewEndAt)}</span>}
            </div>
          )}
        </div>
        <div className="challenge-time-v8">
          <div>
            <span>开始时间</span>
            <strong>{formatDate(challenge.startAt)}</strong>
          </div>
          <div>
            <span>截止时间</span>
            <strong>{formatDate(challenge.endAt)}</strong>
          </div>
        </div>
      </header>

      <div className="challenge-detail-layout-v8">
        <div className="challenge-board-panel-v8">
          <div className="challenge-board-heading-v8">
            <div>
              <span>挑战棋盘</span>
              <strong>选择棋子进入对应任务</strong>
            </div>
            <span className="context-chip">8 × 8</span>
          </div>

          <div className="challenge-board" aria-label="Challenge board">
            {Array.from({ length: 64 }, (_, index) => {
              const x = index % 8;
              const y = 7 - Math.floor(index / 8);
              const task = taskMap.get(`${x}:${y}`);
              const isSelected = Boolean(task && selectedTask && idsEqual(task.id, selectedTask.id));
              const isBreaking = Boolean(task && breakingTaskId && idsEqual(task.id, breakingTaskId));
              const isVisuallyCompleted = Boolean(task && (task.isCompleted || hasTaskId(visualCompletedTaskIds, task.id)));
              const isFocusedTarget = Boolean(task && focusedTaskId && idsEqual(task.id, focusedTaskId));

              return (
                <button
                  className={`board-cell ${(x + y) % 2 === 0 ? "light" : "dark"} ${isSelected ? "selected" : ""} ${isVisuallyCompleted && !isBreaking ? "completed" : ""} ${isBreaking ? "breaking" : ""} ${isFocusedTarget ? "focus-target" : ""}`}
                  key={`${x}:${y}`}
                  type="button"
                  disabled={!task}
                  ref={(element) => {
                    if (task) {
                      pieceRefs.current[normalizeTaskId(task.id)] = element;
                    }
                  }}
                  onClick={() => task && handleTaskClick(task)}
                  onFocus={() => task && setSelectedTask(task)}
                  onMouseEnter={() => task && setSelectedTask(task)}
                >
                  {task && (
                    <span className={`task-piece ${getPieceTone(task, isVisuallyCompleted && !isBreaking)} ${isBreaking ? "challenge-piece--breaking" : ""} ${isVisuallyCompleted && !isBreaking ? "challenge-piece--ghost" : ""}`}>
                      <span className="piece-symbol">{difficultySymbols[task.difficulty]}</span>
                      {isBreaking && (
                        <>
                          {breakFragments.map((fragment) => (
                            <span className={`challenge-piece-fragment fragment-${fragment}`} key={fragment} />
                          ))}
                        </>
                      )}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        </div>

        <aside className="challenge-side-panel challenge-side-panel-v8">
          <section className="challenge-side-section-v8">
            <div className="challenge-side-heading-v8">
              <div>
                <p className="eyebrow">PROGRESS</p>
                <span>我的挑战进度</span>
              </div>
              <strong>
                {challenge.totalTaskCount === 0
                  ? "—"
                  : `${challenge.completedTaskCount} / ${challenge.totalTaskCount}`}
              </strong>
            </div>
            <div className="challenge-progress-track" aria-hidden="true">
              <span style={{ width: `${getProgressPercent(challenge.completedTaskCount, challenge.totalTaskCount)}%` }} />
            </div>
            <button
              className="button primary leaderboard-panel-link"
              type="button"
              onClick={() => navigate(`/challenges/${challenge.id}/leaderboard`)}
            >
              查看排行榜
            </button>
            {canOpenAdminSummary && (
              <div className="challenge-admin-actions challenge-admin-actions-v8">
                <button
                  className="button"
                  type="button"
                  onClick={() => navigate(`/admin/challenges/${challenge.id}/edit`)}
                >
                  编辑挑战
                </button>
                <button
                  className="button"
                  type="button"
                  onClick={() => navigate(`/admin/challenges/${challenge.id}/tasks/new`)}
                >
                  管理任务
                </button>
                <button
                  className="button"
                  type="button"
                  onClick={() => navigate(`/challenges/${challenge.id}/admin`)}
                >
                  管理统计
                </button>
              </div>
            )}
            {canAuditPeerReviews && (
              <button
                className="button"
                type="button"
                onClick={() => navigate(`/challenges/${challenge.id}/peer-review-audit`)}
              >
                互评审计
              </button>
            )}
          </section>

          <section className="challenge-side-section-v8 challenge-selected-section-v8">
            <p className="eyebrow">SELECTED TASK</p>
            {selectedTask ? (
              <div className="selected-task selected-task-v8">
                <div className="selected-task-title-v8">
                  <span className="selected-task-piece-v8">{difficultySymbols[selectedTask.difficulty]}</span>
                  <div>
                    <h2>{selectedTask.title}</h2>
                    <span>{difficultyNames[selectedTask.difficulty]} · {selectedTask.taskType === 1 ? "算法题" : "文件题"}</span>
                  </div>
                </div>
                {selectedTask.description.trim() && (
                  <div className="selected-task-description-v8">
                    <MarkdownRenderer value={selectedTask.description} />
                  </div>
                )}
                <div className="selected-task-facts selected-task-facts-v8">
                  <div><span>得分</span><strong>{selectedTask.earnedScore} / {selectedTask.score}</strong></div>
                  <div>
                    <span>状态</span>
                    <strong className={selectedTask.isCompleted || hasTaskId(visualCompletedTaskIds, selectedTask.id) ? "status-passed" : undefined}>
                      {selectedTask.isCompleted || hasTaskId(visualCompletedTaskIds, selectedTask.id) ? "已完成" : selectedTask.earnedScore > 0 ? "进行中" : "未完成"}
                    </strong>
                  </div>
                </div>
                {selectedTask.taskType === 1 && selectedTask.score > 0 && (
                  <div className="challenge-task-score-progress" aria-label={`当前得分 ${selectedTask.earnedScore} / ${selectedTask.score}`}>
                    <span style={{ width: `${Math.min(100, Math.max(0, Math.round((selectedTask.earnedScore / selectedTask.score) * 100)))}%` }} />
                  </div>
                )}
                <button className="button" type="button" onClick={() => handleTaskClick(selectedTask)}>
                  进入任务
                </button>
              </div>
            ) : (
              <p className="muted">将鼠标移到棋子上查看任务信息。</p>
            )}
          </section>
        </aside>
      </div>
    </section>
  );
}

function getProgressPercent(completed: number, total: number) {
  if (total <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((completed / total) * 100)));
}

function getPieceTone(task: ChallengeTaskDto, isVisuallyCompleted: boolean) {
  if (!task.isPublished) {
    return "locked";
  }

  if (isVisuallyCompleted) {
    return "completed";
  }

  return task.difficulty >= 5 ? "important" : "normal";
}

function idsEqual(left: unknown, right: unknown) {
  return normalizeTaskId(left) === normalizeTaskId(right);
}

function hasTaskId(ids: Set<string>, id: unknown) {
  return ids.has(normalizeTaskId(id));
}

function normalizeTaskId(id: unknown) {
  return String(id).toLowerCase();
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
