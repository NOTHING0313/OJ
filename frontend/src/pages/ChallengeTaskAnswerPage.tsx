import { ChangeEvent, FormEvent, useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  downloadChallengeFileSubmission,
  getChallenge,
  getMyChallengeFileSubmission,
  submitChallengeTaskFileAnswer,
  withdrawMyChallengeFileSubmission,
  type ChallengeDetailDto,
  type ChallengeTaskFileSubmissionDto,
  type ChallengeTaskDto
} from "../api/challengesApi";
import { useAuth } from "../auth/AuthContext";
import { MarkdownRenderer } from "../components/MarkdownRenderer";

const difficultyNames = {
  1: "兵",
  2: "马",
  3: "象",
  4: "车",
  5: "皇后",
  6: "国王"
} as const;

export function ChallengeTaskAnswerPage() {
  const { challengeId, taskId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [challenge, setChallenge] = useState<ChallengeDetailDto | null>(null);
  const [task, setTask] = useState<ChallengeTaskDto | null>(null);
  const [mySubmission, setMySubmission] = useState<ChallengeTaskFileSubmissionDto | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isLoadingSubmission, setIsLoadingSubmission] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [isWithdrawing, setIsWithdrawing] = useState(false);

  useEffect(() => {
    if (!challengeId || !taskId) {
      return;
    }

    getChallenge(challengeId)
      .then((detail) => {
        setChallenge(detail);
        setTask(detail.tasks.find((item) => item.id === taskId) ?? null);
      })
      .catch((err: unknown) => setError(err instanceof Error ? err.message : "加载文件题失败"));
  }, [challengeId, taskId]);

  useEffect(() => {
    if (!challengeId || !taskId || !isAuthenticated) {
      setMySubmission(null);
      return;
    }

    let ignore = false;
    setIsLoadingSubmission(true);

    getMyChallengeFileSubmission(challengeId, taskId)
      .then((submission) => {
        if (!ignore) {
          setMySubmission(submission);
        }
      })
      .catch((err: unknown) => {
        if (!ignore) {
          setError(err instanceof Error ? err.message : "加载我的提交失败");
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoadingSubmission(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [challengeId, taskId, isAuthenticated]);

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    setFile(event.target.files?.[0] ?? null);
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    if (!challengeId || !taskId || !file) {
      setError("请选择 .zip 文件");
      return;
    }

    if (!isAuthenticated) {
      setError("请先登录");
      navigate(`/login?returnTo=${encodeURIComponent(`${location.pathname}${location.search}`)}`);
      return;
    }

    if (!file.name.toLowerCase().endsWith(".zip")) {
      setError("只能提交 .zip 文件");
      return;
    }

    setIsSubmitting(true);
    setError(null);
    setMessage(null);

    try {
      await submitChallengeTaskFileAnswer(challengeId, taskId, file);
      setMessage("文件已提交，任务完成");
      setTask((current) => current ? { ...current, isCompleted: true } : current);
      setFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
      navigate(`/challenges/${challengeId}`, {
        state: {
          completedTaskId: String(taskId),
          playBreakAnimation: true,
          animationNonce: Date.now()
        }
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "提交失败");
    } finally {
      setIsSubmitting(false);
    }
  }

  async function handleWithdraw() {
    if (!challengeId || !taskId || !mySubmission?.canWithdrawSubmission) {
      return;
    }

    if (!window.confirm("确认撤回这次文件提交吗？撤回后可重新上传。")) {
      return;
    }

    setIsWithdrawing(true);
    setError(null);
    setMessage(null);

    try {
      await withdrawMyChallengeFileSubmission(challengeId, taskId);
      setMySubmission(null);
      setTask((current) => current
        ? { ...current, isCompleted: false, completedAt: null, completedScore: null }
        : current);
      setFile(null);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
      setMessage("已撤回最近一次文件提交。");
    } catch (err) {
      setError(err instanceof Error ? err.message : "撤回提交失败");
    } finally {
      setIsWithdrawing(false);
    }
  }

  async function handleDownloadMine() {
    if (!challengeId || !mySubmission) {
      return;
    }

    try {
      setIsDownloading(true);
      await downloadChallengeFileSubmission(challengeId, mySubmission.id, mySubmission.originalFileName);
    } catch (err) {
      setError(err instanceof Error ? err.message : "下载失败");
    } finally {
      setIsDownloading(false);
    }
  }

  if (error && !task) {
    return <div className="alert error">{error}</div>;
  }

  if (!challenge || !task) {
    return <div className="state-line">正在加载文件题...</div>;
  }

  return (
    <section className="challenge-page task-detail-layout file-task-layout">
      <article className="task-statement file-task-main">
        <Link className="subtle-link" to={`/challenges/${challenge.id}`}>
          返回棋盘
        </Link>
        <p className="eyebrow">{challenge.title}</p>
        <h1>{task.title}</h1>
        <MarkdownRenderer value={task.description} />
      </article>

      <aside className="file-task-sidebar">
        <section className="file-task-panel file-task-info-panel">
          <p className="eyebrow">ZIP FILE TASK</p>
          <dl>
            <div>
              <dt>类型</dt>
              <dd>文件题</dd>
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
        </section>

        <section className="file-task-panel my-file-submission">
          <p className="eyebrow">MY SUBMISSION</p>
          {!isAuthenticated ? (
            <div className="quiet-note">
              <p>请先登录后提交文件。</p>
              <Link className="button" to={`/login?returnTo=${encodeURIComponent(`${location.pathname}${location.search}`)}`}>
                去登录
              </Link>
            </div>
          ) : isLoadingSubmission ? (
            <div className="state-line">正在加载我的提交...</div>
          ) : mySubmission ? (
            <div className="submission-status-card">
              <strong>已提交</strong>
              <dl>
                <div>
                  <dt>文件名</dt>
                  <dd>{mySubmission.originalFileName}</dd>
                </div>
                <div>
                  <dt>文件大小</dt>
                  <dd>{formatFileSize(mySubmission.fileSizeBytes)}</dd>
                </div>
                <div>
                  <dt>首次提交</dt>
                  <dd>{formatDate(mySubmission.createdAt)}</dd>
                </div>
                <div>
                  <dt>最后更新</dt>
                  <dd>{formatDate(mySubmission.updatedAt)}</dd>
                </div>
              </dl>
              <ReviewResult submission={mySubmission} maxScore={task.score} />
              <div className="file-task-actions">
                <button className="button" disabled={isDownloading} type="button" onClick={handleDownloadMine}>
                  {isDownloading ? "下载中..." : "下载我的提交"}
                </button>
                {mySubmission.canWithdrawSubmission && (
                  <button
                    className="button file-task-withdraw-button"
                    disabled={isWithdrawing}
                    type="button"
                    onClick={handleWithdraw}
                  >
                    {isWithdrawing ? "撤回中..." : "撤回提交"}
                  </button>
                )}
              </div>
            </div>
          ) : (
            <div className="quiet-note">你还没有提交文件。</div>
          )}
        </section>

        <form className="file-task-panel answer-form file-answer-form file-task-upload-card" onSubmit={handleSubmit}>
          <div className="file-task-upload-head">
            <div>
              <p className="eyebrow">UPLOAD</p>
              <h2>ZIP 文件</h2>
              <p>请选择 .zip 文件后提交。</p>
            </div>
            <input
              ref={fileInputRef}
              className="visually-hidden-file"
              type="file"
              accept=".zip,application/zip,application/x-zip-compressed"
              onChange={handleFileChange}
            />
          </div>
          <div className="file-task-upload-picker">
            <button className="button file-task-upload-button" type="button" onClick={() => fileInputRef.current?.click()}>
              选择文件
            </button>
            <div
              className={`file-task-upload-filename ${file ? "" : "empty"}`}
              title={file?.name ?? "尚未选择文件"}
            >
              {file?.name ?? "尚未选择文件"}
            </div>
          </div>
          {file && (
            <div className="file-task-upload-meta">
              <span>{formatFileSize(file.size)}</span>
              <span>{file.name.toLowerCase().endsWith(".zip") ? "ZIP 文件" : "文件类型需为 .zip"}</span>
            </div>
          )}
          {message && <div className="quiet-note success">{message}</div>}
          {error && <div className="alert error">{error}</div>}
          <button className="button primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? "提交中..." : "提交文件"}
          </button>
          <Link className="button" to={`/challenges/${challenge.id}`}>
            返回棋盘
          </Link>
        </form>
      </aside>
    </section>
  );
}

function ReviewResult({ submission, maxScore }: { submission: ChallengeTaskFileSubmissionDto; maxScore: number }) {
  const isReviewed = submission.reviewScore !== null || submission.reviewedAt !== null;

  if (!isReviewed) {
    return <div className="quiet-note">已提交，等待评分。</div>;
  }

  return (
    <div className="quiet-note success">
      <strong>
        得分：{submission.reviewScore ?? 0} / {maxScore}
      </strong>
      <p>评语：{submission.reviewComment || "暂无评语"}</p>
      {submission.reviewedByUserName && <p>评分人：{submission.reviewedByUserName}</p>}
      {submission.reviewedAt && <p>评分时间：{formatDate(submission.reviewedAt)}</p>}
    </div>
  );
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

function formatFileSize(value: number) {
  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / 1024 / 1024).toFixed(2)} MB`;
}
