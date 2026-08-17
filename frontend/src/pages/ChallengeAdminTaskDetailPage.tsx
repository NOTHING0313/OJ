import { Fragment, useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  downloadChallengeFileSubmission,
  getChallengeAdminSummary,
  reviewChallengeFileSubmission,
  type ChallengeAdminSummary,
  type ChallengeAdminTaskProgress,
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

interface UserTaskRow {
  user: ChallengeAdminUserProgress;
  status: ChallengeAdminUserTaskStatus | null;
}

export function ChallengeAdminTaskDetailPage() {
  const { challengeId, taskId } = useParams();
  const [summary, setSummary] = useState<ChallengeAdminSummary | null>(null);
  const [task, setTask] = useState<ChallengeAdminTaskProgress | null>(null);
  const [rows, setRows] = useState<UserTaskRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [reviewTarget, setReviewTarget] = useState<ChallengeAdminUserTaskStatus | null>(null);
  const [reviewScore, setReviewScore] = useState("");
  const [reviewComment, setReviewComment] = useState("");
  const [isReviewing, setIsReviewing] = useState(false);

  useEffect(() => {
    if (!challengeId || !taskId) {
      return;
    }

    let ignore = false;
    setIsLoading(true);

    getChallengeAdminSummary(challengeId)
      .then((data) => {
        if (ignore) {
          return;
        }

        const matchedTask = data.tasks.find((item) => item.taskId === taskId) ?? null;
        if (!matchedTask) {
          setSummary(data);
          setTask(null);
          setRows([]);
          setError("题目不存在。");
          return;
        }

        setSummary(data);
        setTask(matchedTask);
        setRows(buildRows(data, matchedTask.taskId));
        setError(null);
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "单题统计加载失败");
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

  const statistics = useMemo(() => {
    const participantCount = rows.length;
    const completedCount = rows.filter((row) => row.status?.isCompleted).length;
    const scores = rows.map((row) => row.status?.completedScore ?? 0);
    const totalScore = scores.reduce((sum, value) => sum + value, 0);

    return {
      participantCount,
      completedCount,
      pendingCount: participantCount - completedCount,
      completionRate: participantCount > 0 ? `${((completedCount / participantCount) * 100).toFixed(1)}%` : "0.0%",
      averageScore: participantCount > 0 ? (totalScore / participantCount).toFixed(1) : "0.0",
      maxScore: scores.length > 0 ? Math.max(...scores) : 0,
      minScore: scores.length > 0 ? Math.min(...scores) : 0
    };
  }, [rows]);

  async function refreshSummary() {
    if (!challengeId || !taskId) {
      return;
    }

    const data = await getChallengeAdminSummary(challengeId);
    const matchedTask = data.tasks.find((item) => item.taskId === taskId) ?? null;
    setSummary(data);
    setTask(matchedTask);
    setRows(matchedTask ? buildRows(data, matchedTask.taskId) : []);
  }

  async function handleDownload(status: ChallengeAdminUserTaskStatus) {
    if (!challengeId || !status.fileSubmissionId) {
      return;
    }

    try {
      setDownloadingId(status.fileSubmissionId);
      await downloadChallengeFileSubmission(challengeId, status.fileSubmissionId, status.originalFileName ?? undefined);
    } catch (err) {
      setError(err instanceof Error ? err.message : "文件下载失败");
    } finally {
      setDownloadingId(null);
    }
  }

  function openReviewForm(status: ChallengeAdminUserTaskStatus) {
    setReviewTarget(status);
    setReviewScore(String(status.reviewScore ?? status.completedScore ?? 0));
    setReviewComment(status.reviewComment ?? "");
    setNotice(null);
    setError(null);
  }

  async function handleSubmitReview() {
    if (!challengeId || !reviewTarget?.fileSubmissionId) {
      return;
    }

    const score = Number(reviewScore);
    if (!Number.isInteger(score) || score < 0 || score > reviewTarget.score) {
      setError(`评分必须是 0 到 ${reviewTarget.score} 之间的整数。`);
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
      await refreshSummary();
    } catch (err) {
      setError(err instanceof Error ? err.message : "评分保存失败");
    } finally {
      setIsReviewing(false);
    }
  }

  if (isLoading) {
    return <div className="state-line">正在加载单题统计...</div>;
  }

  if (!challengeId) {
    return <div className="alert error">Challenge 参数无效。</div>;
  }

  if (error && !summary) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error}</div>
        <Link className="button" to="/challenges">
          返回挑战列表
        </Link>
      </section>
    );
  }

  if (!summary || !task) {
    return (
      <section className="page-section narrow">
        <div className="alert error">{error ?? "题目不存在。"}</div>
        <Link className="button" to={`/challenges/${challengeId}/admin`}>
          返回管理统计
        </Link>
      </section>
    );
  }

  return (
    <section className="challenge-page admin-task-detail-page">
      <div className="leaderboard-header">
        <div>
          <p className="eyebrow">TASK DETAIL</p>
          <h1>{task.title}</h1>
          <p>{summary.challengeTitle}</p>
        </div>
        <Link className="button" to={`/challenges/${challengeId}/admin`}>
          返回管理统计
        </Link>
      </div>

      <div className="task-overview-panel">
        <span>{taskTypeNames[task.taskType]}</span>
        <span>难度：{difficultyNames[task.difficulty]}</span>
        <span>满分：{task.score}</span>
        <span>平均分按全部参与者计算，未完成按 0 分。</span>
      </div>

      {notice && <div className="quiet-note success">{notice}</div>}
      {error && <div className="alert error">{error}</div>}

      <div className="admin-metrics task-metrics">
        <Metric label="参与人数" value={statistics.participantCount} />
        <Metric label="完成人数" value={statistics.completedCount} />
        <Metric label="未完成人数" value={statistics.pendingCount} />
        <Metric label="完成率" value={statistics.completionRate} />
        <Metric label="平均分" value={statistics.averageScore} />
        <Metric label="最高分" value={statistics.maxScore} />
        <Metric label="最低分" value={statistics.minScore} />
      </div>

      <section className="admin-panel">
        <div className="admin-panel-header">
          <p className="eyebrow">SUBMISSIONS</p>
          <h2>用户作答情况</h2>
        </div>

        {rows.length === 0 ? (
          <div className="empty-state">暂无参与者</div>
        ) : (
          <div className="table-wrap leaderboard-table-wrap">
            <table className="leaderboard-table">
              <thead>
                <tr>
                  <th>用户</th>
                  <th>状态</th>
                  <th>得分</th>
                  <th>完成时间</th>
                  <th>提交类型</th>
                  <th>操作</th>
                </tr>
              </thead>
              <tbody>
                {rows.map(({ user, status }) => (
                  <Fragment key={user.userId}>
                    <tr>
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
                      <td>{formatTaskStatus(status)}</td>
                      <td>{formatScore(status, task.score)}</td>
                      <td>{formatDate(status?.completedAt ?? null)}</td>
                      <td>{formatSubmissionType(status)}</td>
                      <td>
                        {status?.submissionId && (
                          <Link className="button" to={`/submissions/${status.submissionId}`}>
                            查看提交
                          </Link>
                        )}
                        {status?.fileSubmissionId && (
                          <button
                            className="button"
                            disabled={downloadingId === status.fileSubmissionId}
                            type="button"
                            onClick={() => handleDownload(status)}
                          >
                            {downloadingId === status.fileSubmissionId ? "下载中..." : "下载文件"}
                          </button>
                        )}
                        {status?.taskType === 2 && status.fileSubmissionId && (
                          <button className="button" type="button" onClick={() => openReviewForm(status)}>
                            {status.isReviewed ? "修改评分" : "评分"}
                          </button>
                        )}
                        {!status?.submissionId && !status?.fileSubmissionId && <span className="muted">-</span>}
                      </td>
                    </tr>
                    {reviewTarget?.fileSubmissionId && reviewTarget.fileSubmissionId === status?.fileSubmissionId && (
                      <tr className="review-row">
                        <td colSpan={6}>
                          <div className="review-form">
                            <label>
                              分数（0 - {reviewTarget.score}）
                              <input
                                max={reviewTarget.score}
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
        )}
      </section>
    </section>
  );
}

function buildRows(summary: ChallengeAdminSummary, taskId: string): UserTaskRow[] {
  return summary.users.map((user) => ({
    user,
    status: user.taskStatuses.find((status) => status.taskId === taskId) ?? null
  }));
}

function formatTaskStatus(status: ChallengeAdminUserTaskStatus | null) {
  if (!status?.isCompleted) {
    return "未完成";
  }

  if (status.taskType === 2 && status.fileSubmissionId && !status.isReviewed) {
    return "已提交待评分";
  }

  return "已完成";
}

function formatScore(status: ChallengeAdminUserTaskStatus | null, maxScore: number) {
  if (!status?.isCompleted) {
    return "-";
  }

  return `${status.completedScore ?? 0} / ${maxScore}`;
}

function formatSubmissionType(status: ChallengeAdminUserTaskStatus | null) {
  if (!status?.isCompleted) {
    return "未提交";
  }

  if (status.fileSubmissionId) {
    return "文件题";
  }

  if (status.submissionId) {
    return "算法题";
  }

  return "-";
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
