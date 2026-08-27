import { Fragment, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  downloadChallengeAdminTasksCsv,
  downloadChallengeAdminUsersCsv,
  downloadChallengeFileSubmission,
  getChallengeAdminSummary,
  reviewChallengeFileSubmission,
  type ChallengeAdminSummary,
  type ChallengeAdminUserProgress,
  type ChallengeAdminUserTaskStatus
} from "../api/challengesApi";

const difficultyNames = {
  1: "兵",
  2: "马",
  3: "象",
  4: "车",
  5: "皇后",
  6: "国王"
} as const;

const taskTypeNames = {
  1: "算法题",
  2: "文件题"
} as const;

export function ChallengeAdminSummaryPage() {
  const { challengeId } = useParams();
  const [summary, setSummary] = useState<ChallengeAdminSummary | null>(null);
  const [selectedUser, setSelectedUser] = useState<ChallengeAdminUserProgress | null>(null);
  const [fatalError, setFatalError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [exportingCsv, setExportingCsv] = useState<"users" | "tasks" | null>(null);
  const [reviewTarget, setReviewTarget] = useState<ChallengeAdminUserTaskStatus | null>(null);
  const [reviewScore, setReviewScore] = useState("");
  const [reviewComment, setReviewComment] = useState("");
  const [isReviewing, setIsReviewing] = useState(false);

  useEffect(() => {
    if (!challengeId) {
      return;
    }

    let ignore = false;
    setIsLoading(true);

    getChallengeAdminSummary(challengeId)
      .then((data) => {
        if (!ignore) {
          setSummary(data);
          setSelectedUser((current) => data.users.find((user) => user.userId === current?.userId) ?? data.users[0] ?? null);
          setFatalError(null);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setFatalError(err instanceof Error ? err.message : "管理统计加载失败");
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

  async function refreshSummary(preferredUserId?: string) {
    if (!challengeId) {
      return;
    }

    const data = await getChallengeAdminSummary(challengeId);
    setSummary(data);
    setSelectedUser(data.users.find((user) => user.userId === preferredUserId) ?? data.users[0] ?? null);
  }

  async function handleDownload(status: ChallengeAdminUserTaskStatus) {
    if (!challengeId || !status.fileSubmissionId) {
      return;
    }

    try {
      setDownloadingId(status.fileSubmissionId);
      await downloadChallengeFileSubmission(challengeId, status.fileSubmissionId, status.originalFileName ?? undefined);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "文件下载失败");
    } finally {
      setDownloadingId(null);
    }
  }

  async function handleExportCsv(type: "users" | "tasks") {
    if (!challengeId) {
      return;
    }

    try {
      setExportingCsv(type);
      if (type === "users") {
        await downloadChallengeAdminUsersCsv(challengeId);
      } else {
        await downloadChallengeAdminTasksCsv(challengeId);
      }
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "CSV 导出失败");
    } finally {
      setExportingCsv(null);
    }
  }

  function openReviewForm(status: ChallengeAdminUserTaskStatus) {
    setReviewTarget(status);
    setReviewScore(String(status.reviewScore ?? status.earnedScore ?? status.completedScore ?? 0));
    setReviewComment(status.reviewComment ?? "");
    setNotice(null);
    setActionError(null);
  }

  async function handleSubmitReview() {
    if (!challengeId || !reviewTarget?.fileSubmissionId) {
      return;
    }

    const score = Number(reviewScore);
    if (!Number.isInteger(score) || score < 0 || score > reviewTarget.score) {
      setActionError(`评分必须是 0 到 ${reviewTarget.score} 之间的整数。`);
      return;
    }

    try {
      setIsReviewing(true);
      await reviewChallengeFileSubmission(challengeId, reviewTarget.fileSubmissionId, {
        score,
        comment: reviewComment.trim() || undefined
      });
      setNotice("评分已保存。");
      setReviewTarget(null);
      await refreshSummary(selectedUser?.userId);
      setActionError(null);
    } catch (err) {
      setActionError(err instanceof Error ? err.message : "评分保存失败");
    } finally {
      setIsReviewing(false);
    }
  }

  if (isLoading) {
    return <div className="state-line">正在加载管理统计...</div>;
  }

  if (fatalError) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{fatalError}</div>
        {challengeId && (
          <Link className="button" to={`/challenges/${challengeId}`}>
            返回棋盘
          </Link>
        )}
      </section>
    );
  }

  if (!summary) {
    return <div className="empty-state">暂无管理统计数据</div>;
  }

  const overallCompletionRate = getCompletionRate(summary.totalCompletionCount, summary.participantCount * summary.totalTaskCount);

  return (
    <section className="challenge-page admin-summary-page ui-v2-page analytics-v2-page challenge-admin-summary-v2-page challenge-admin-summary-v8-page">
      <div className="leaderboard-header ui-v2-page-header challenge-admin-header-v8">
        <div>
          <p className="eyebrow">CHALLENGE ADMIN</p>
          <h1>{summary.challengeTitle}</h1>
          <p>查看参与者进度、逐题完成情况与文件题评分状态。</p>
        </div>
        <div className="button-row challenge-admin-toolbar-v8">
          <button
            className="button"
            disabled={exportingCsv !== null}
            type="button"
            onClick={() => handleExportCsv("users")}
          >
            {exportingCsv === "users" ? "导出中..." : "导出用户 CSV"}
          </button>
          <button
            className="button"
            disabled={exportingCsv !== null}
            type="button"
            onClick={() => handleExportCsv("tasks")}
          >
            {exportingCsv === "tasks" ? "导出中..." : "导出逐题 CSV"}
          </button>
          <Link className="button" to={`/challenges/${summary.challengeId}`}>
            返回棋盘
          </Link>
        </div>
      </div>

      <div className="admin-metrics challenge-admin-metrics-v8">
        <Metric label="总任务数" value={summary.totalTaskCount} />
        <Metric label="参与人数" value={summary.participantCount} />
        <Metric label="总完成次数" value={summary.totalCompletionCount} />
        <Metric label="总体完成率" value={`${overallCompletionRate}%`} />
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {actionError && <div className="alert error">{actionError}</div>}

      <section className="admin-panel challenge-admin-panel-v8">
        <div className="admin-panel-header challenge-admin-panel-header-v8">
          <div>
            <p className="eyebrow">USERS</p>
            <h2>用户进度</h2>
            <p>选择用户后可在下方查看完整逐题状态。</p>
          </div>
          <span className="context-chip">{summary.users.length} 名参与者</span>
        </div>

        {summary.users.length === 0 ? (
          <div className="empty-state">暂无参与者</div>
        ) : (
          <div className="table-wrap leaderboard-table-wrap challenge-admin-table-wrap-v8">
            <table className="leaderboard-table challenge-admin-table-v8">
              <thead>
                <tr>
                  <th>用户</th>
                  <th>完成进度</th>
                  <th>总分</th>
                  <th>最后完成</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {summary.users.map((user) => {
                  const progress = getCompletionRate(user.completedTaskCount, summary.totalTaskCount);

                  return (
                    <tr className={selectedUser?.userId === user.userId ? "admin-selected-row" : ""} key={user.userId}>
                      <td>
                        <div className="leaderboard-user challenge-admin-user-v8">
                          {user.avatarUrl ? (
                            <img src={user.avatarUrl} alt={user.userName} />
                          ) : (
                            <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
                          )}
                          <div>
                            <strong>{user.userName}</strong>
                            <span>{user.completedTaskCount} / {summary.totalTaskCount} 题</span>
                          </div>
                        </div>
                      </td>
                      <td>
                        <div className="challenge-admin-progress-v8">
                          <div className="challenge-progress-track"><span style={{ width: `${progress}%` }} /></div>
                          <span>{progress}%</span>
                        </div>
                      </td>
                      <td><strong className="challenge-admin-score-v8">{user.totalScore}</strong></td>
                      <td>{formatDate(user.lastCompletedAt)}</td>
                      <td>
                        <button className="button" type="button" onClick={() => setSelectedUser(user)}>
                          查看
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="admin-panel challenge-admin-panel-v8">
        <div className="admin-panel-header challenge-admin-panel-header-v8">
          <div>
            <p className="eyebrow">TASKS</p>
            <h2>题目完成统计</h2>
            <p>汇总每道题的类型、难度、分数与参与者完成情况。</p>
          </div>
          <span className="context-chip">{summary.tasks.length} 道任务</span>
        </div>
        <div className="table-wrap leaderboard-table-wrap challenge-admin-table-wrap-v8">
          <table className="leaderboard-table challenge-admin-table-v8 challenge-task-stat-table-v8">
            <thead>
              <tr>
                <th>题目</th>
                <th>类型</th>
                <th>难度</th>
                <th>分数</th>
                <th>完成情况</th>
                <th>操作</th>
              </tr>
            </thead>
            <tbody>
              {summary.tasks.map((task) => {
                const progress = getCompletionRate(task.completedUserCount, summary.participantCount);

                return (
                  <tr key={task.taskId}>
                    <td><strong className="challenge-admin-task-title-v8">{task.title}</strong></td>
                    <td><span className="challenge-admin-badge-v8">{taskTypeNames[task.taskType]}</span></td>
                    <td><span className="challenge-admin-badge-v8 subtle">{difficultyNames[task.difficulty]}</span></td>
                    <td>{task.score}</td>
                    <td>
                      <div className="challenge-admin-progress-v8">
                        <div className="challenge-progress-track"><span style={{ width: `${progress}%` }} /></div>
                        <span>{task.completedUserCount} / {summary.participantCount || 0}</span>
                      </div>
                    </td>
                    <td>
                      <Link className="button" to={`/challenges/${summary.challengeId}/admin/tasks/${task.taskId}`}>
                        查看
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      <section className="admin-panel challenge-admin-panel-v8 challenge-user-detail-v8">
        <div className="admin-panel-header challenge-admin-panel-header-v8">
          <div>
            <p className="eyebrow">DETAIL</p>
            <h2>{selectedUser ? `${selectedUser.userName} 的逐题状态` : "用户逐题状态"}</h2>
            <p>查看每道任务的完成状态、得分与提交记录。</p>
          </div>
        </div>

        {selectedUser ? (
          <>
            <div className="challenge-selected-user-summary-v8">
              <div className="leaderboard-user challenge-admin-user-v8">
                {selectedUser.avatarUrl ? (
                  <img src={selectedUser.avatarUrl} alt={selectedUser.userName} />
                ) : (
                  <span className="leaderboard-avatar-placeholder">{selectedUser.userName.slice(0, 1).toUpperCase()}</span>
                )}
                <div>
                  <strong>{selectedUser.userName}</strong>
                  <span>当前选择用户</span>
                </div>
              </div>
              <div><span>完成题数</span><strong>{selectedUser.completedTaskCount} / {summary.totalTaskCount}</strong></div>
              <div><span>累计得分</span><strong>{selectedUser.totalScore}</strong></div>
              <div><span>最后完成</span><strong>{formatDate(selectedUser.lastCompletedAt)}</strong></div>
            </div>

            <div className="table-wrap leaderboard-table-wrap challenge-admin-table-wrap-v8">
              <table className="leaderboard-table challenge-admin-table-v8 challenge-user-task-table-v8">
                <thead>
                  <tr>
                    <th>题目</th>
                    <th>类型</th>
                    <th>难度</th>
                    <th>状态</th>
                    <th>得分</th>
                    <th>评分</th>
                    <th>完成时间</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedUser.taskStatuses.map((status) => (
                    <Fragment key={status.taskId}>
                      <tr key={status.taskId}>
                        <td><strong className="challenge-admin-task-title-v8">{status.taskTitle}</strong></td>
                        <td><span className="challenge-admin-badge-v8">{taskTypeNames[status.taskType]}</span></td>
                        <td><span className="challenge-admin-badge-v8 subtle">{difficultyNames[status.difficulty]}</span></td>
                        <td>
                          <span className={`challenge-admin-status-v8 ${status.isCompleted ? "completed" : "pending"}`}>
                            {status.isCompleted ? "已完成" : "未完成"}
                          </span>
                        </td>
                        <td>{status.earnedScore > 0 ? status.earnedScore : status.completedScore ?? "—"}</td>
                        <td>
                          {status.taskType === 2 && status.fileSubmissionId ? (
                            <ReviewSummary status={status} />
                          ) : (
                            <span className="muted">—</span>
                          )}
                        </td>
                        <td>{formatDate(status.completedAt)}</td>
                        <td>
                          <div className="challenge-admin-row-actions-v8">
                            {status.submissionId && (
                              <Link className="button" to={`/submissions/${status.submissionId}`}>
                                提交
                              </Link>
                            )}
                            {status.fileSubmissionId && (
                              <button
                                className="button"
                                disabled={downloadingId === status.fileSubmissionId}
                                type="button"
                                onClick={() => handleDownload(status)}
                              >
                                {downloadingId === status.fileSubmissionId ? "下载中..." : "下载"}
                              </button>
                            )}
                            {status.taskType === 2 && status.fileSubmissionId && (
                              <button className="button" type="button" onClick={() => openReviewForm(status)}>
                                评分
                              </button>
                            )}
                            {!status.submissionId && !status.fileSubmissionId && <span className="muted">—</span>}
                          </div>
                        </td>
                      </tr>
                      {reviewTarget?.fileSubmissionId === status.fileSubmissionId && (
                        <tr className="review-row">
                          <td colSpan={8}>
                            <div className="review-form">
                              <label>
                                分数（0 - {status.score}）
                                <input
                                  max={status.score}
                                  min={0}
                                  type="number"
                                  value={reviewScore}
                                  onChange={(event) => setReviewScore(event.target.value)}
                                />
                              </label>
                              <label>
                                评语
                                <textarea
                                  maxLength={2000}
                                  value={reviewComment}
                                  onChange={(event) => setReviewComment(event.target.value)}
                                />
                              </label>
                              <div className="button-row">
                                <button className="button" disabled={isReviewing} type="button" onClick={() => setReviewTarget(null)}>
                                  取消
                                </button>
                                <button className="button primary" disabled={isReviewing} type="button" onClick={handleSubmitReview}>
                                  {isReviewing ? "保存中..." : "保存评分"}
                                </button>
                              </div>
                            </div>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : (
          <div className="empty-state">选择用户查看逐题状态</div>
        )}
      </section>
    </section>
  );
}

function getCompletionRate(completed: number, total: number) {
  if (total <= 0) {
    return 0;
  }

  return Math.min(100, Math.max(0, Math.round((completed / total) * 100)));
}

function ReviewSummary({ status }: { status: ChallengeAdminUserTaskStatus }) {
  if (!status.isReviewed) {
    return <span className="muted">未评分</span>;
  }

  return (
    <div className="review-summary">
      <strong>
        {status.reviewScore} / {status.score}
      </strong>
      <span>{status.reviewComment || "无评语"}</span>
      <span>
        {status.reviewedByUserName ? `评分人：${status.reviewedByUserName}` : "评分人：-"}
        {status.reviewedAt ? ` · ${formatDate(status.reviewedAt)}` : ""}
      </span>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="admin-metric">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
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
