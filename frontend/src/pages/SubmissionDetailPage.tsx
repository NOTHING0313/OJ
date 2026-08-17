import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getSubmission, type SubmissionDto } from "../api/submissionsApi";
import { formatDate, languageLabel, statusLabel } from "../utils/labels";

const acceptedStatus = 3;

export function SubmissionDetailPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const challengeId = searchParams.get("challengeId");
  const redirectedToChallengeRef = useRef(false);
  const [submission, setSubmission] = useState<SubmissionDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copyNotice, setCopyNotice] = useState<string | null>(null);

  useEffect(() => {
    if (!id) {
      return;
    }

    let isMounted = true;
    let timerId: number | undefined;

    async function load() {
      try {
        const item = await getSubmission(id!);
        if (!isMounted) {
          return;
        }
        setSubmission(item);
        setError(null);

        if (
          challengeId
          && item.challengeTaskId
          && item.status === acceptedStatus
          && !redirectedToChallengeRef.current
        ) {
          redirectedToChallengeRef.current = true;
          navigate(`/challenges/${challengeId}`, {
            state: {
              completedTaskId: String(item.challengeTaskId),
              playBreakAnimation: true,
              animationNonce: Date.now()
            },
            replace: true
          });
          return;
        }

        if (!item.finishedAt) {
          timerId = window.setTimeout(load, 2000);
        }
      } catch (err) {
        if (isMounted) {
          setError(err instanceof Error ? err.message : "加载提交失败");
        }
      }
    }

    load();

    return () => {
      isMounted = false;
      if (timerId) {
        window.clearTimeout(timerId);
      }
    };
  }, [challengeId, id, navigate]);

  if (error && !submission) {
    return <div className="alert error">{error}</div>;
  }

  if (!submission) {
    return <div className="state-line">加载中...</div>;
  }

  return (
    <section className="page-section">
      <div className="page-header">
        <div>
          <h1>提交详情</h1>
          <p>{submission.problemTitle} · {submission.id}</p>
        </div>
        <div className="button-row">
          {challengeId && (
            <Link className="button" to={`/challenges/${challengeId}`}>
              返回挑战棋盘
            </Link>
          )}
          <Link className="button" to={`/problems/${submission.problemId}`}>
            返回题目
          </Link>
          <Link className="button" to={`/submissions/my?problemId=${submission.problemId}`}>
            我的提交
          </Link>
        </div>
      </div>

      {challengeId && submission.status === acceptedStatus && (
        <div className="quiet-note success">该小题已完成</div>
      )}

      <div className="detail-grid">
        <div>
          <span>状态</span>
          <strong className={getStatusClassName(submission.status)}>{statusLabel(submission.status)}</strong>
        </div>
        <div>
          <span>语言</span>
          <strong>{languageLabel(submission.language)}</strong>
        </div>
        <div>
          <span>用户</span>
          <strong>{submission.userName}</strong>
        </div>
        <div>
          <span>提交时间</span>
          <strong>{formatDate(submission.createdAt)}</strong>
        </div>
        <div>
          <span>耗时</span>
          <strong>{submission.timeUsedMs ?? "-"} ms</strong>
        </div>
        <div>
          <span>内存</span>
          <strong>{submission.memoryUsedKb ?? "-"} KB</strong>
        </div>
        <div>
          <span>完成时间</span>
          <strong>{formatDate(submission.finishedAt)}</strong>
        </div>
      </div>

      {submission.errorMessage && <div className="alert error pre-line">{submission.errorMessage}</div>}

      <div className="section-heading-row">
        <h2>源代码</h2>
        <button className="button" type="button" onClick={() => copySource(submission.sourceCode, setCopyNotice)}>
          复制代码
        </button>
      </div>
      {copyNotice && <div className="quiet-note success">{copyNotice}</div>}
      <pre className="source-code-block">{submission.sourceCode}</pre>

      <h2>测试点结果</h2>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>序号</th>
              <th>状态</th>
              <th>耗时</th>
              <th>内存</th>
              <th>实际输出</th>
              <th>期望输出</th>
              <th>错误信息</th>
            </tr>
          </thead>
          <tbody>
            {submission.caseResults.map((caseResult, index) => (
              <tr key={caseResult.id}>
                <td>{index + 1}</td>
                <td><span className={getStatusClassName(caseResult.status)}>{statusLabel(caseResult.status)}</span></td>
                <td>{caseResult.timeUsedMs ?? "-"} ms</td>
                <td>{caseResult.memoryUsedKb ?? "-"} KB</td>
                <td className="pre-line output-cell">{caseResult.isRedacted ? <span className="redacted-output">隐藏测试点已脱敏</span> : caseResult.actualOutput ?? "-"}</td>
                <td className="pre-line output-cell">{caseResult.isRedacted ? <span className="redacted-output">隐藏测试点已脱敏</span> : caseResult.expectedOutput ?? "-"}</td>
                <td className="pre-line output-cell">{caseResult.errorMessage ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {submission.caseResults.length === 0 && <div className="empty-state">暂无测试点结果</div>}
      </div>
    </section>
  );
}

function getStatusClassName(status: number) {
  return status === acceptedStatus ? "status-accepted" : undefined;
}

async function copySource(sourceCode: string, setCopyNotice: (value: string | null) => void) {
  try {
    await navigator.clipboard.writeText(sourceCode);
    setCopyNotice("代码已复制。");
    window.setTimeout(() => setCopyNotice(null), 2000);
  } catch {
    setCopyNotice("当前浏览器不支持自动复制，请手动选择代码。");
  }
}
