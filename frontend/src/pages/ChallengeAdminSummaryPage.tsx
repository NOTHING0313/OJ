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
    setReviewScore(String(status.reviewScore ?? status.completedScore ?? 0));
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

  return (
    <section className="challenge-page admin-summary-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">CHALLENGE ADMIN</p>
          <h1>{summary.challengeTitle}</h1>
          <p>参与人数统计进入过该挑战或已有完成记录的用户。</p>
        </div>
        <div className="button-row">
          <button
            className="button"
            disabled={exportingCsv !== null}
            type="button"
            onClick={() => handleExportCsv("users")}
          >
            {exportingCsv === "users" ? "导出中..." : "导出用户总览 CSV"}
          </button>
          <button
            className="button"
            disabled={exportingCsv !== null}
            type="button"
            onClick={() => handleExportCsv("tasks")}
          >
            {exportingCsv === "tasks" ? "导出中..." : "导出逐题明细 CSV"}
          </button>
          <Link className="button" to={`/challenges/${summary.challengeId}`}>
            返回棋盘
          </Link>
        </div>
      </div>

      <div className="admin-metrics">
        <Metric label="总任务数" value={summary.totalTaskCount} />
        <Metric label="参与人数" value={summary.participantCount} />
        <Metric label="总完成次数" value={summary.totalCompletionCount} />
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {actionError && <div className="alert error">{actionError}</div>}

      <div className="admin-summary-grid">
        <section className="admin-panel">
          <div className="admin-panel-header">
            <p className="eyebrow">USERS</p>
            <h2>用户进度</h2>
          </div>

          {summary.users.length === 0 ? (
            <div className="empty-state">暂无参与者</div>
          ) : (
            <div className="table-wrap leaderboard-table-wrap">
              <table className="leaderboard-table">
                <thead>
                  <tr>
                    <th>用户</th>
                    <th>完成题数</th>
                    <th>总分</th>
                    <th>最后完成</th>
                    <th>操作</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.users.map((user) => (
                    <tr className={selectedUser?.userId === user.userId ? "admin-selected-row" : ""} key={user.userId}>
                      <td>
                        <div className="leaderboard-user">
                          {user.avatarUrl ? (
                            <img src={user.avatarUrl} alt={user.userName} />
                          ) : (
                            <span className="leaderboard-avatar-placeholder">{user.userName.slice(0, 1).toUpperCase()}</span>
                          )}
                          <span>{user.userName}</span>
                        </div>
                      </td>
                      <td>
                        {user.completedTaskCount} / {summary.totalTaskCount}
                      </td>
                      <td>{user.totalScore}</td>
                      <td>{formatDate(user.lastCompletedAt)}</td>
                      <td>
                        <button className="button" type="button" onClick={() => setSelectedUser(user)}>
                          查看详情
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="admin-panel">
          <div className="admin-panel-header">
            <p className="eyebrow">TASKS</p>
            <h2>题目完成统计</h2>
          </div>
          <div className="table-wrap leaderboard-table-wrap">
            <table className="leaderboard-table">
              <thead>
                <tr>
                  <th>题目</th>
                  <th>类型</th>
                  <th>难度</th>
                  <th>分数</th>
                  <th>完成人数</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {summary.tasks.map((task) => (
                  <tr key={task.taskId}>
                    <td>{task.title}</td>
                    <td>{taskTypeNames[task.taskType]}</td>
                    <td>{difficultyNames[task.difficulty]}</td>
                    <td>{task.score}</td>
                    <td>{task.completedUserCount}</td>
                    <td>
                      <Link className="button" to={`/challenges/${summary.challengeId}/admin/tasks/${task.taskId}`}>
                        查看详情
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="admin-panel">
        <div className="admin-panel-header">
          <p className="eyebrow">DETAIL</p>
          <h2>{selectedUser ? `${selectedUser.userName} 的逐题状态` : "用户逐题状态"}</h2>
        </div>

        {selectedUser ? (
          <div className="table-wrap leaderboard-table-wrap">
            <table className="leaderboard-table">
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
                      <td>{status.taskTitle}</td>
                      <td>{taskTypeNames[status.taskType]}</td>
                      <td>{difficultyNames[status.difficulty]}</td>
                      <td>{status.isCompleted ? "已完成" : "未完成"}</td>
                      <td>{status.completedScore ?? "-"}</td>
                      <td>
                        {status.taskType === 2 && status.fileSubmissionId ? (
                          <ReviewSummary status={status} />
                        ) : (
                          <span className="muted">-</span>
                        )}
                      </td>
                      <td>{formatDate(status.completedAt)}</td>
                      <td>
                        {status.submissionId && (
                          <Link className="button" to={`/submissions/${status.submissionId}`}>
                            查看提交
                          </Link>
                        )}
                        {status.fileSubmissionId && (
                          <button
                            className="button"
                            disabled={downloadingId === status.fileSubmissionId}
                            type="button"
                            onClick={() => handleDownload(status)}
                          >
                            {downloadingId === status.fileSubmissionId ? "下载中..." : "下载文件"}
                          </button>
                        )}
                        {status.taskType === 2 && status.fileSubmissionId && (
                          <button className="button" type="button" onClick={() => openReviewForm(status)}>
                            评分
                          </button>
                        )}
                        {!status.submissionId && !status.fileSubmissionId && <span className="muted">-</span>}
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
        ) : (
          <div className="empty-state">选择用户查看逐题状态</div>
        )}
      </section>
    </section>
  );
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

function Metric({ label, value }: { label: string; value: number }) {
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
