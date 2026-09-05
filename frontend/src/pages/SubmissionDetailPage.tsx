import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getSubmission, type SubmissionDto } from "../api/submissionsApi";
import { choiceOptionLabel, orderChoiceOptions } from "../utils/choiceOptions";
import { formatDate, languageLabel, statusLabel } from "../utils/labels";
import { MarkdownRenderer } from "../components/MarkdownRenderer";

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
    <section className="page-section ui-v2-page submission-detail-v2-page">
      <div className="page-header ui-v2-page-header">
        <div>
          <p className="eyebrow">SUBMISSION</p>
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
          <strong><span className={`submission-status-badge submission-status-${statusTone(submission.status)}`}>{statusLabel(submission.status)}</span></strong>
        </div>
        <div>
          <span>语言</span>
          <strong><span className="submission-language-badge">{submission.language ? languageLabel(submission.language) : "选择题"}</span></strong>
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
          <span>最大时间</span>
          <strong>{formatMetric(submission.evaluation.maxTimeUsedMs, "ms")}</strong>
        </div>
        <div>
          <span>用例平均时间</span>
          <strong>{formatMetric(submission.evaluation.averageCaseTimeUsedMs, "ms")}</strong>
        </div>
        <div>
          <span>最大内存</span>
          <strong>{formatMemory(submission.evaluation.maxMemoryUsedKb)}</strong>
        </div>
        <div>
          <span>用例平均内存</span>
          <strong>{formatMemory(submission.evaluation.averageCaseMemoryUsedKb)}</strong>
        </div>
        <div>
          <span>完成时间</span>
          <strong>{formatDate(submission.finishedAt)}</strong>
        </div>
      </div>

      {submission.status !== acceptedStatus && submission.caseResults.length > 0 && (
        <div className="quiet-note">资源评估基于本次实际执行的用例。</div>
      )}

      {submission.errorMessage && <div className="alert error pre-line">{submission.errorMessage}</div>}

      {submission.submissionKind === 2 && <div className="quiet-note success">选择题得分：{submission.choiceScore}/{submission.choiceTotalScore}{submission.answersRevealed === false ? "；答案尚未揭示" : ""}</div>}
      {submission.submissionKind === 2 && submission.choiceQuestionResults.map((result, index) => <section className="content-block" key={result.questionId}>
        <div className="section-heading-row"><h2>第 {index + 1} 题</h2><strong>{result.isCorrect ? `正确 · ${result.score} 分` : "错误 · 0 分"}</strong></div>
        <MarkdownRenderer value={result.stemMarkdown} />
        <div className="form-stack">{orderChoiceOptions(result.options).map((option) => <div className="choice-option choice-option-result" key={option.id}>
          <strong>{choiceOptionLabel(option.order)}{result.selectedOptionIds.includes(option.id) ? "（已选）" : ""}{result.correctOptionIds?.includes(option.id) ? "（正确）" : ""}</strong>
          <MarkdownRenderer value={option.contentMarkdown} />
        </div>)}</div>
        {result.explanationMarkdown && <div><h3>解析</h3><MarkdownRenderer value={result.explanationMarkdown} /></div>}
      </section>)}

      {submission.sourceCode && <><div className="section-heading-row">
        <h2>源代码</h2>
        <button className="button" type="button" onClick={() => copySource(submission.sourceCode!, setCopyNotice)}>
          复制代码
        </button>
      </div>
      {copyNotice && <div className="quiet-note success">{copyNotice}</div>}
      <pre className="source-code-block">{submission.sourceCode}</pre>

      </>}

      {submission.submissionKind === 1 && <><h2>测试点结果</h2>
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
                <td><span className={`submission-status-badge submission-status-${statusTone(caseResult.status)}`}>{statusLabel(caseResult.status)}</span></td>
                <td>{formatMetric(caseResult.timeUsedMs, "ms")}</td>
                <td>{formatMetric(caseResult.memoryUsedKb, "KB")}</td>
                <td className="pre-line output-cell">{caseResult.isRedacted ? <span className="redacted-output">隐藏测试点已脱敏</span> : caseResult.actualOutput ?? "-"}</td>
                <td className="pre-line output-cell">{caseResult.isRedacted ? <span className="redacted-output">隐藏测试点已脱敏</span> : caseResult.expectedOutput ?? "-"}</td>
                <td className="pre-line output-cell">{caseResult.errorMessage ?? "-"}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {submission.caseResults.length === 0 && <div className="empty-state">暂无测试点结果</div>}
      </div></>}
    </section>
  );
}

function statusTone(status: number) {
  switch (status) {
    case 1:
      return "pending";
    case 2:
      return "judging";
    case 3:
      return "accepted";
    case 4:
      return "wrong-answer";
    case 5:
    case 6:
      return "limit";
    case 7:
      return "runtime-error";
    case 8:
      return "compile-error";
    default:
      return "system-error";
  }
}

function formatMetric(value: number | null, unit: string) {
  return value === null ? "—" : `${formatNumber(value)} ${unit}`;
}

function formatMemory(valueKb: number | null) {
  return valueKb === null ? "—" : `${formatNumber(valueKb / 1024)} MB`;
}

function formatNumber(value: number) {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
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
